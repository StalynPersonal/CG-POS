using CgPos.Central.Aplicacion.Abstracciones;
using CgPos.Central.Aplicacion.Organizacion;
using CgPos.Central.Aplicacion.Seguridad;
using CgPos.Central.Infraestructura.Persistencia;
using CgPos.Contratos.Central;
using CgPos.Dominio.Seguridad;
using Microsoft.EntityFrameworkCore;
using CgPos.Dominio.Comun;

namespace CgPos.Central.Infraestructura.Seguridad;

internal sealed class ServicioAdministracionSeguridad(
    ContextoDatosCentral contexto,
    IHashContrasenas hashContrasenas,
    IParametrosCentral parametros,
    IAuditoriaCentral auditoria,
    TimeProvider reloj) : IServicioAdministracionSeguridad
{
    private const string TipoRol = "RolCentral";
    private const string TipoUsuario = "UsuarioCentral";
    private const string SinAdministrador = "El cambio dejaría al Central sin ningún usuario activo que pueda administrar la seguridad.";

    public async Task<IReadOnlyList<DatosRolCentral>> ListarRolesAsync(CancellationToken cancelacion = default)
    {
        var roles = await contexto.RolesCentral.AsNoTracking().Include(r => r.PermisosAsignados).OrderBy(r => r.Nombre).ToListAsync(cancelacion);
        var usuariosPorRol = await contexto.UsuariosCentral.AsNoTracking()
            .GroupBy(u => u.RolId)
            .Select(g => new { RolId = g.Key, Cantidad = g.Count() })
            .ToDictionaryAsync(g => g.RolId, g => g.Cantidad, cancelacion);

        return roles
            .Select(r => new DatosRolCentral(r.Id, r.Codigo, r.Nombre, r.Activo, r.PermisosAsignados.Select(p => p.PermisoCodigo).Order(StringComparer.Ordinal).ToList(),
                usuariosPorRol.GetValueOrDefault(r.Id)))
            .ToList();
    }

    public async Task<ResultadoAdministracion> CrearRolAsync(SolicitudRolCentral solicitud, UsuarioAuditoria actor, CancellationToken cancelacion = default)
    {
        var codigo = solicitud.Codigo?.Trim() ?? string.Empty;
        if (codigo.Length > 0 && await contexto.RolesCentral.AnyAsync(r => r.Codigo == codigo, cancelacion))
            return ResultadoAdministracion.Error($"Ya existe un rol con el código '{codigo}'.");

        RolCentral rol;
        try
        {
            rol = RolCentral.Crear(codigo, solicitud.Nombre);
            AsignarPermisos(rol, solicitud.Permisos);
        }
        catch (ArgumentException excepcion)
        {
            return ResultadoAdministracion.Error(excepcion.Message);
        }

        contexto.RolesCentral.Add(rol);
        Auditar("Seguridad.RolCreado", TipoRol, rol.Id, actor, new { rol.Codigo, rol.Nombre, Permisos = solicitud.Permisos });
        await contexto.SaveChangesAsync(cancelacion);
        return ResultadoAdministracion.Correcto(rol.Id);
    }

    public async Task<ResultadoAdministracion> ActualizarRolAsync(int rolId, SolicitudRolCentral solicitud, UsuarioAuditoria actor, CancellationToken cancelacion = default)
    {
        var (roles, usuarios) = await CargarSeguridadAsync(cancelacion);
        var rol = roles.SingleOrDefault(r => r.Id == rolId);
        if (rol is null)
            return ResultadoAdministracion.Inexistente("El rol no existe.");
        if (!string.Equals(rol.Codigo, solicitud.Codigo?.Trim(), StringComparison.OrdinalIgnoreCase))
            return ResultadoAdministracion.Error("El código del rol no se puede cambiar.");

        var anteriores = rol.PermisosAsignados.Select(p => p.PermisoCodigo).ToList();
        try
        {
            rol.CambiarNombre(solicitud.Nombre);
            AsignarPermisos(rol, solicitud.Permisos);
        }
        catch (ArgumentException excepcion)
        {
            contexto.ChangeTracker.Clear();
            return ResultadoAdministracion.Error(excepcion.Message);
        }

        if (!QuedaAdministrador(roles, usuarios))
        {
            contexto.ChangeTracker.Clear();
            return ResultadoAdministracion.Error(SinAdministrador);
        }

        Auditar("Seguridad.RolActualizado", TipoRol, rol.Id, actor, new { rol.Codigo, rol.Nombre, PermisosAnteriores = anteriores, Permisos = solicitud.Permisos });
        await contexto.SaveChangesAsync(cancelacion);
        return ResultadoAdministracion.Correcto(rol.Id);
    }

    public async Task<ResultadoAdministracion> CambiarEstadoRolAsync(int rolId, bool activo, UsuarioAuditoria actor, CancellationToken cancelacion = default)
    {
        var (roles, usuarios) = await CargarSeguridadAsync(cancelacion);
        var rol = roles.SingleOrDefault(r => r.Id == rolId);
        if (rol is null)
            return ResultadoAdministracion.Inexistente("El rol no existe.");

        if (activo) rol.Activar(); else rol.Desactivar();

        if (!activo && !QuedaAdministrador(roles, usuarios))
        {
            contexto.ChangeTracker.Clear();
            return ResultadoAdministracion.Error(SinAdministrador);
        }

        Auditar(activo ? "Seguridad.RolActivado" : "Seguridad.RolDesactivado", TipoRol, rol.Id, actor, new { rol.Codigo });
        await contexto.SaveChangesAsync(cancelacion);
        return ResultadoAdministracion.Correcto(rol.Id);
    }

    public async Task<IReadOnlyList<DatosUsuarioCentral>> ListarUsuariosAsync(CancellationToken cancelacion = default) =>
        await (
                from usuario in contexto.UsuariosCentral
                join rol in contexto.RolesCentral on usuario.RolId equals rol.Id
                orderby usuario.Codigo
                select new DatosUsuarioCentral(usuario.Id, usuario.Codigo, usuario.Nombre, usuario.Correo, usuario.RolId, rol.Nombre, usuario.Activo, usuario.BloqueadoHasta,
                    usuario.DebeCambiarContrasena, usuario.UltimoIngresoEn))
            .AsNoTracking()
            .ToListAsync(cancelacion);

    public async Task<ResultadoAdministracion> CrearUsuarioAsync(SolicitudUsuarioCentral solicitud, UsuarioAuditoria actor, CancellationToken cancelacion = default)
    {
        var codigo = solicitud.Codigo?.Trim() ?? string.Empty;
        if (codigo.Length > 0 && await contexto.UsuariosCentral.AnyAsync(u => u.Codigo == codigo, cancelacion))
            return ResultadoAdministracion.Error($"Ya existe el usuario '{codigo}'.");

        if (!await contexto.RolesCentral.AnyAsync(r => r.Id == solicitud.RolId && r.Activo, cancelacion))
            return ResultadoAdministracion.Error("Seleccione un rol activo.");

        if (await ValidarContrasenaAsync(solicitud.ContrasenaTemporal, codigo, cancelacion) is { } problema)
            return ResultadoAdministracion.Error(problema);

        UsuarioCentral usuario;
        try
        {
            usuario = UsuarioCentral.Crear(codigo, solicitud.Nombre, solicitud.Correo, solicitud.RolId, hashContrasenas.Hash(solicitud.ContrasenaTemporal), debeCambiarContrasena: true);
        }
        catch (ArgumentException excepcion)
        {
            return ResultadoAdministracion.Error(excepcion.Message);
        }

        contexto.UsuariosCentral.Add(usuario);
        Auditar("Seguridad.UsuarioCreado", TipoUsuario, usuario.Id, actor, new { usuario.Codigo, usuario.Nombre, usuario.RolId });
        await contexto.SaveChangesAsync(cancelacion);
        return ResultadoAdministracion.Correcto(usuario.Id);
    }

    public async Task<ResultadoAdministracion> ActualizarUsuarioAsync(int usuarioId, SolicitudActualizarUsuarioCentral solicitud, UsuarioAuditoria actor,
        CancellationToken cancelacion = default)
    {
        var (roles, usuarios) = await CargarSeguridadAsync(cancelacion);
        var usuario = usuarios.SingleOrDefault(u => u.Id == usuarioId);
        if (usuario is null)
            return ResultadoAdministracion.Inexistente("El usuario no existe.");

        var rol = roles.SingleOrDefault(r => r.Id == solicitud.RolId);
        if (rol is null || (!rol.Activo && rol.Id != usuario.RolId))
            return ResultadoAdministracion.Error("Seleccione un rol activo.");

        var rolAnterior = usuario.RolId;
        try
        {
            usuario.ActualizarDatos(solicitud.Nombre, solicitud.Correo);
            usuario.CambiarRol(solicitud.RolId);
        }
        catch (ArgumentException excepcion)
        {
            contexto.ChangeTracker.Clear();
            return ResultadoAdministracion.Error(excepcion.Message);
        }

        if (!QuedaAdministrador(roles, usuarios))
        {
            contexto.ChangeTracker.Clear();
            return ResultadoAdministracion.Error(SinAdministrador);
        }

        if (rolAnterior != usuario.RolId)
            await RevocarSesionesAsync(usuario.Id, "Cambio de rol", cancelacion);

        Auditar("Seguridad.UsuarioActualizado", TipoUsuario, usuario.Id, actor, new { usuario.Codigo, usuario.Nombre, usuario.Correo, RolAnterior = rolAnterior, usuario.RolId });
        await contexto.SaveChangesAsync(cancelacion);
        return ResultadoAdministracion.Correcto(usuario.Id);
    }

    public async Task<ResultadoAdministracion> RestablecerContrasenaAsync(int usuarioId, string contrasenaTemporal, UsuarioAuditoria actor, CancellationToken cancelacion = default)
    {
        var usuario = await contexto.UsuariosCentral.SingleOrDefaultAsync(u => u.Id == usuarioId, cancelacion);
        if (usuario is null)
            return ResultadoAdministracion.Inexistente("El usuario no existe.");

        if (await ValidarContrasenaAsync(contrasenaTemporal, usuario.Codigo, cancelacion) is { } problema)
            return ResultadoAdministracion.Error(problema);

        usuario.CambiarContrasena(hashContrasenas.Hash(contrasenaTemporal), debeCambiar: true, reloj.Ahora());
        usuario.Desbloquear();
        await RevocarSesionesAsync(usuario.Id, "Contraseña restablecida por un administrador", cancelacion);

        Auditar("Seguridad.ContrasenaRestablecida", TipoUsuario, usuario.Id, actor, new { usuario.Codigo });
        await contexto.SaveChangesAsync(cancelacion);
        return ResultadoAdministracion.Correcto(usuario.Id);
    }

    public async Task<ResultadoAdministracion> DesbloquearAsync(int usuarioId, UsuarioAuditoria actor, CancellationToken cancelacion = default)
    {
        var usuario = await contexto.UsuariosCentral.SingleOrDefaultAsync(u => u.Id == usuarioId, cancelacion);
        if (usuario is null)
            return ResultadoAdministracion.Inexistente("El usuario no existe.");

        usuario.Desbloquear();
        Auditar("Seguridad.UsuarioDesbloqueado", TipoUsuario, usuario.Id, actor, new { usuario.Codigo });
        await contexto.SaveChangesAsync(cancelacion);
        return ResultadoAdministracion.Correcto(usuario.Id);
    }

    public async Task<ResultadoAdministracion> CambiarEstadoUsuarioAsync(int usuarioId, bool activo, UsuarioAuditoria actor, CancellationToken cancelacion = default)
    {
        if (!activo && usuarioId == actor.Id)
            return ResultadoAdministracion.Error("No puede desactivar su propio usuario.");

        var (roles, usuarios) = await CargarSeguridadAsync(cancelacion);
        var usuario = usuarios.SingleOrDefault(u => u.Id == usuarioId);
        if (usuario is null)
            return ResultadoAdministracion.Inexistente("El usuario no existe.");

        if (activo) usuario.Activar(); else usuario.Desactivar();

        if (!activo)
        {
            if (!QuedaAdministrador(roles, usuarios))
            {
                contexto.ChangeTracker.Clear();
                return ResultadoAdministracion.Error(SinAdministrador);
            }

            await RevocarSesionesAsync(usuario.Id, "Usuario desactivado", cancelacion);
        }

        Auditar(activo ? "Seguridad.UsuarioActivado" : "Seguridad.UsuarioDesactivado", TipoUsuario, usuario.Id, actor, new { usuario.Codigo });
        await contexto.SaveChangesAsync(cancelacion);
        return ResultadoAdministracion.Correcto(usuario.Id);
    }

    /// <summary>Roles con permisos y usuarios cargados antes de cambiar nada: la verificación del último administrador se hace sobre los mismos objetos modificados.</summary>
    private async Task<(List<RolCentral> Roles, List<UsuarioCentral> Usuarios)> CargarSeguridadAsync(CancellationToken cancelacion) =>
        (await contexto.RolesCentral.Include(r => r.PermisosAsignados).ToListAsync(cancelacion), await contexto.UsuariosCentral.ToListAsync(cancelacion));

    private static bool QuedaAdministrador(IEnumerable<RolCentral> roles, IEnumerable<UsuarioCentral> usuarios)
    {
        var rolesAdministradores = roles.Where(r => r.TienePermiso(CatalogoPermisosCentral.AdministrarSeguridad)).Select(r => r.Id).ToHashSet();
        return usuarios.Any(u => u.Activo && rolesAdministradores.Contains(u.RolId));
    }

    private static void AsignarPermisos(RolCentral rol, IReadOnlyList<string>? permisos)
    {
        var deseados = (permisos ?? []).Select(p => p.Trim()).ToHashSet(StringComparer.Ordinal);
        foreach (var sobrante in rol.PermisosAsignados.Select(p => p.PermisoCodigo).Where(c => !deseados.Contains(c)).ToList())
            rol.QuitarPermiso(sobrante);
        foreach (var codigo in deseados)
            rol.AsignarPermiso(codigo);
    }

    private async Task<string?> ValidarContrasenaAsync(string? contrasena, string codigoUsuario, CancellationToken cancelacion)
    {
        var largoMinimo = await parametros.ObtenerEnteroPositivoAsync(ClavesParametrosCentral.LargoMinimoContrasena, cancelacion);
        var largoMaximo = await parametros.ObtenerEnteroPositivoOpcionalAsync(ClavesParametrosCentral.LargoMaximoContrasena, cancelacion);
        var compleja = await parametros.ObtenerBooleanoOpcionalAsync(ClavesParametrosCentral.ContrasenaCompleja, cancelacion);
        return ReglasContrasena.Validar(contrasena, largoMinimo, largoMaximo, compleja, codigoUsuario);
    }

    private async Task RevocarSesionesAsync(int usuarioId, string motivo, CancellationToken cancelacion)
    {
        var ahora = reloj.Ahora();
        foreach (var sesion in await contexto.SesionesCentral.Where(s => s.UsuarioId == usuarioId && s.RevocadaEn == null).ToListAsync(cancelacion))
            sesion.Revocar(ahora, motivo);
    }

    private void Auditar(string accion, string tipo, int entidadId, UsuarioAuditoria actor, object detalle) =>
        auditoria.Registrar(new EntradaAuditoria(accion, tipo, entidadId.ToString(), detalle, Usuario: actor));
}
