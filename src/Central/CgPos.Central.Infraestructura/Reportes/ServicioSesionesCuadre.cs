using CgPos.Central.Aplicacion.Abstracciones;
using CgPos.Central.Aplicacion.Organizacion;
using CgPos.Central.Aplicacion.Reportes;
using CgPos.Central.Infraestructura.Persistencia;
using CgPos.Central.Infraestructura.Sincronizacion;
using CgPos.Contratos.Central;
using CgPos.Dominio.Comun;
using CgPos.Dominio.Seguridad;
using Microsoft.EntityFrameworkCore;

namespace CgPos.Central.Infraestructura.Reportes;

/// <summary>
/// Ingreso al módulo de cuadre con el usuario de caja. Es la misma clave que el supervisor usa en la terminal, con su
/// mismo bloqueo por intentos: no se le crea otro usuario ni se le da acceso al resto del Central.
/// </summary>
internal sealed class ServicioSesionesCuadre(
    ContextoDatosCentral contexto,
    IParametrosCentral parametros,
    IAuditoriaCentral auditoria,
    TimeProvider reloj) : IServicioSesionesCuadre
{
    private const string TipoEntidad = "UsuarioCaja";
    private const string Rechazo = "El usuario o la clave no son correctos.";

    public async Task<ResultadoSesionCuadre> IngresarAsync(string codigo, string clave, CancellationToken cancelacion = default)
    {
        var codigoLimpio = codigo?.Trim() ?? string.Empty;
        clave ??= string.Empty;
        var ahora = reloj.Ahora();

        var usuario = codigoLimpio.Length == 0
            ? null
            : await contexto.UsuariosCaja.Include(u => u.CajasAsignadas).SingleOrDefaultAsync(u => u.Codigo == codigoLimpio, cancelacion);

        if (usuario is null || usuario.ClaveHash is not { Length: > 0 } hash || !HashCredencialesCaja.VerificarClave(clave, hash))
            return await RechazarAsync(usuario, "Clave incorrecta", ahora, cancelacion);

        if (!usuario.Activo)
            return ResultadoSesionCuadre.Rechazado("El usuario está inactivo.");
        if (usuario.EstaBloqueado(ahora))
            return ResultadoSesionCuadre.Rechazado($"El usuario está bloqueado hasta las {usuario.BloqueadoHasta:HH:mm}.");

        // El permiso lo da su rol de caja, igual que en la terminal.
        var permisos = await PermisosAsync(usuario.RolId, cancelacion);
        if (!permisos.Contains(CatalogoPermisos.ConsultarCuadre))
            return ResultadoSesionCuadre.Rechazado("Su usuario no tiene acceso al módulo de cuadre.");

        // Solo ve las sucursales de las cajas que tiene asignadas.
        var cajas = usuario.CajasAsignadas.Select(c => c.CajaId).ToList();
        var suyas = await contexto.Cajas.AsNoTracking()
            .Where(c => cajas.Contains(c.Id))
            .Select(c => c.SucursalId)
            .Distinct()
            .ToListAsync(cancelacion);

        var sucursales = await contexto.Sucursales.AsNoTracking()
            .Where(s => suyas.Contains(s.Id))
            .OrderBy(s => s.Codigo)
            .Select(s => new DatosSucursalCuadre(s.Id, s.Codigo, s.Nombre))
            .ToListAsync(cancelacion);

        if (sucursales.Count == 0)
            return ResultadoSesionCuadre.Rechazado("Su usuario no tiene cajas asignadas: no hay nada que cuadrar.");

        usuario.RegistrarIngresoExitoso(ahora);
        auditoria.Registrar(new EntradaAuditoria("Cuadre.Ingreso", TipoEntidad, usuario.Codigo,
            Detalle: new { Sucursales = sucursales.Select(s => s.Codigo) },
            Usuario: new UsuarioAuditoria(usuario.Id, usuario.Nombre)));
        await contexto.SaveChangesAsync(cancelacion);

        return new ResultadoSesionCuadre(new DatosSesionCuadre(usuario.Id, usuario.Codigo, usuario.Nombre, await RolAsync(usuario.RolId, cancelacion),
            permisos.Where(p => p.StartsWith("Cuadre.", StringComparison.Ordinal)).ToList(), sucursales));
    }

    /// <summary>Los permisos del rol; el rol con «*» los tiene todos, como en la caja.</summary>
    private async Task<IReadOnlyCollection<string>> PermisosAsync(int rolId, CancellationToken cancelacion)
    {
        var rol = await contexto.RolesCaja.AsNoTracking().Include(r => r.PermisosAsignados).SingleOrDefaultAsync(r => r.Id == rolId && r.Activo, cancelacion);
        if (rol is null)
            return [];

        var codigos = rol.PermisosAsignados.Select(p => p.PermisoCodigo).ToList();
        return codigos.Contains("*") ? CatalogoPermisos.Todos.Select(p => p.Codigo).ToList() : codigos;
    }

    private async Task<string> RolAsync(int rolId, CancellationToken cancelacion) =>
        await contexto.RolesCaja.AsNoTracking().Where(r => r.Id == rolId).Select(r => r.Nombre).SingleOrDefaultAsync(cancelacion) ?? string.Empty;

    /// <summary>Un intento fallido cuenta igual que en la caja: al llegar al máximo, el usuario queda bloqueado.</summary>
    private async Task<ResultadoSesionCuadre> RechazarAsync(Usuario? usuario, string motivo, DateTimeOffset ahora, CancellationToken cancelacion)
    {
        if (usuario is null)
            return ResultadoSesionCuadre.Rechazado(Rechazo);

        // Los mismos límites que en la terminal: es el mismo usuario y la misma clave.
        var intentos = await parametros.ObtenerEnteroPositivoAsync("Seguridad.IntentosMaximosClave", cancelacion);
        var minutos = await parametros.ObtenerEnteroPositivoAsync("Seguridad.MinutosBloqueo", cancelacion);
        var bloqueado = usuario.RegistrarIngresoFallido(ahora, intentos, TimeSpan.FromMinutes(minutos));

        auditoria.Registrar(new EntradaAuditoria("Cuadre.IngresoRechazado", TipoEntidad, usuario.Codigo,
            Detalle: new { Motivo = motivo, Bloqueado = bloqueado },
            Usuario: new UsuarioAuditoria(usuario.Id, usuario.Nombre)));
        await contexto.SaveChangesAsync(cancelacion);

        return ResultadoSesionCuadre.Rechazado(bloqueado ? $"Demasiados intentos: el usuario queda bloqueado {minutos} minutos." : Rechazo);
    }
}
