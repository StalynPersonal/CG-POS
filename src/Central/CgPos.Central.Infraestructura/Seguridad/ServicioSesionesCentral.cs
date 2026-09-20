using System.Collections.Frozen;
using CgPos.Central.Aplicacion.Abstracciones;
using CgPos.Central.Aplicacion.Organizacion;
using CgPos.Central.Aplicacion.Seguridad;
using CgPos.Central.Infraestructura.Persistencia;
using CgPos.Dominio.Seguridad;
using Microsoft.EntityFrameworkCore;
using CgPos.Dominio.Comun;

namespace CgPos.Central.Infraestructura.Seguridad;

internal sealed class ServicioSesionesCentral(
    ContextoDatosCentral contexto,
    IHashContrasenas hashContrasenas,
    IParametrosCentral parametros,
    IAuditoriaCentral auditoria,
    TimeProvider reloj) : IServicioSesionesCentral
{
    private const string TipoEntidad = "UsuarioCentral";

    // Un usuario inexistente tarda lo mismo que una contraseña incorrecta: la respuesta no revela qué usuarios existen.
    private static string? _hashFicticio;

    public async Task<ResultadoSesionCentral> IngresarAsync(string codigo, string contrasena, OrigenSolicitud origen, CancellationToken cancelacion = default)
    {
        var codigoLimpio = codigo?.Trim() ?? string.Empty;
        contrasena ??= string.Empty;
        var ahora = reloj.Ahora();

        var usuario = codigoLimpio.Length == 0 ? null : await contexto.UsuariosCentral.SingleOrDefaultAsync(u => u.Codigo == codigoLimpio, cancelacion);
        if (usuario is null)
        {
            hashContrasenas.Verificar(contrasena, _hashFicticio ??= hashContrasenas.Hash(Guid.NewGuid().ToString()));
            return await RechazarIngresoAsync(null, MotivoRechazoCentral.CredencialesInvalidas, origen, cancelacion, codigoIntentado: codigoLimpio);
        }

        if (usuario.EstaBloqueado(ahora))
            return await RechazarIngresoAsync(usuario, MotivoRechazoCentral.UsuarioBloqueado, origen, cancelacion);

        if (!hashContrasenas.Verificar(contrasena, usuario.ContrasenaHash))
        {
            var bloqueado = await RegistrarFalloAsync(usuario, ahora, cancelacion);
            return await RechazarIngresoAsync(usuario, bloqueado ? MotivoRechazoCentral.UsuarioBloqueado : MotivoRechazoCentral.CredencialesInvalidas, origen, cancelacion);
        }

        if (!usuario.Activo)
            return await RechazarIngresoAsync(usuario, MotivoRechazoCentral.UsuarioInactivo, origen, cancelacion);

        var rol = await CargarRolAsync(usuario.RolId, cancelacion);
        if (rol is not { Activo: true })
            return await RechazarIngresoAsync(usuario, MotivoRechazoCentral.RolInactivo, origen, cancelacion);

        var (inactividad, duracion) = await DuracionesAsync(cancelacion);
        usuario.RegistrarIngresoExitoso(ahora);
        var (sesion, token) = IniciarSesion(usuario, ahora, inactividad, duracion, origen);

        Auditar("Seguridad.IngresoExitoso", usuario, new { Sesion = sesion.Familia, origen.DireccionIp, origen.AgenteUsuario });
        await contexto.SaveChangesAsync(cancelacion);

        return ResultadoSesionCentral.Exito(CrearSesion(usuario, rol, sesion.Familia), token, sesion.ExpiraEn);
    }

    public async Task<ResultadoSesionCentral> RenovarAsync(string tokenRenovacion, OrigenSolicitud origen, CancellationToken cancelacion = default)
    {
        if (string.IsNullOrWhiteSpace(tokenRenovacion))
            return ResultadoSesionCentral.Rechazo(MotivoRechazoCentral.SesionInvalida);

        var ahora = reloj.Ahora();
        var tokenHash = TokensSeguros.Hash(tokenRenovacion.Trim());
        var actual = await contexto.SesionesCentral.SingleOrDefaultAsync(s => s.TokenHash == tokenHash, cancelacion);
        if (actual is null)
            return ResultadoSesionCentral.Rechazo(MotivoRechazoCentral.SesionInvalida);

        if (actual.FueUsada)
        {
            // El token ya se había cambiado por otro: alguien tiene una copia. Se cierra la sesión completa.
            await RevocarAsync(s => s.Familia == actual.Familia, ahora, "Token de renovación reutilizado", cancelacion);
            auditoria.Registrar(new EntradaAuditoria("Seguridad.RenovacionReutilizada", TipoEntidad, actual.UsuarioId.ToString(),
                new { Sesion = actual.Familia, origen.DireccionIp, origen.AgenteUsuario }));
            await contexto.SaveChangesAsync(cancelacion);
            return ResultadoSesionCentral.Rechazo(MotivoRechazoCentral.SesionReutilizada);
        }

        if (!actual.EstaVigente(ahora))
            return ResultadoSesionCentral.Rechazo(MotivoRechazoCentral.SesionInvalida);

        var usuario = await contexto.UsuariosCentral.SingleAsync(u => u.Id == actual.UsuarioId, cancelacion);
        var rol = await CargarRolAsync(usuario.RolId, cancelacion);
        MotivoRechazoCentral? rechazo = !usuario.Activo ? MotivoRechazoCentral.UsuarioInactivo
            : usuario.EstaBloqueado(ahora) ? MotivoRechazoCentral.UsuarioBloqueado
            : rol is not { Activo: true } ? MotivoRechazoCentral.RolInactivo
            : null;

        if (rechazo is { } motivo)
        {
            await RevocarAsync(s => s.Familia == actual.Familia, ahora, $"Renovación rechazada: {motivo}", cancelacion);
            await contexto.SaveChangesAsync(cancelacion);
            return ResultadoSesionCentral.Rechazo(motivo, usuario.BloqueadoHasta);
        }

        var (inactividad, _) = await DuracionesAsync(cancelacion);
        var token = TokensSeguros.Generar();
        var nueva = actual.Rotar(TokensSeguros.Hash(token), ahora, inactividad, origen.DireccionIp, origen.AgenteUsuario);
        contexto.SesionesCentral.Add(nueva);

        try
        {
            await contexto.SaveChangesAsync(cancelacion);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Otra renovación simultánea con el mismo token ganó; esta no emite un segundo token.
            contexto.ChangeTracker.Clear();
            return ResultadoSesionCentral.Rechazo(MotivoRechazoCentral.SesionInvalida);
        }

        return ResultadoSesionCentral.Exito(CrearSesion(usuario, rol!, actual.Familia), token, nueva.ExpiraEn);
    }

    public async Task CerrarAsync(Guid sesionId, int usuarioId, CancellationToken cancelacion = default)
    {
        var ahora = reloj.Ahora();
        await RevocarAsync(s => s.Familia == sesionId && s.UsuarioId == usuarioId, ahora, "Sesión cerrada por el usuario", cancelacion);

        var nombre = await contexto.UsuariosCentral.Where(u => u.Id == usuarioId).Select(u => u.Nombre).SingleOrDefaultAsync(cancelacion);
        auditoria.Registrar(new EntradaAuditoria("Seguridad.SesionCerrada", TipoEntidad, usuarioId.ToString(), new { Sesion = sesionId },
            Usuario: nombre is null ? null : new UsuarioAuditoria(usuarioId, nombre)));
        await contexto.SaveChangesAsync(cancelacion);
    }

    public async Task<bool> EsSesionActivaAsync(Guid sesionId, int usuarioId, CancellationToken cancelacion = default)
    {
        var ahora = reloj.Ahora();
        return await contexto.SesionesCentral.AnyAsync(s => s.Familia == sesionId && s.UsuarioId == usuarioId
                && s.UsadaEn == null && s.RevocadaEn == null && s.FinSesion > ahora, cancelacion)
            && await contexto.UsuariosCentral.AnyAsync(u => u.Id == usuarioId && u.Activo, cancelacion);
    }

    public async Task<ResultadoSesionCentral> CambiarContrasenaAsync(int usuarioId, string actual, string nueva, OrigenSolicitud origen,
        CancellationToken cancelacion = default)
    {
        var ahora = reloj.Ahora();
        var usuario = await contexto.UsuariosCentral.SingleOrDefaultAsync(u => u.Id == usuarioId, cancelacion);
        if (usuario is not { Activo: true })
            return ResultadoSesionCentral.Rechazo(MotivoRechazoCentral.UsuarioInactivo);

        if (usuario.EstaBloqueado(ahora))
            return ResultadoSesionCentral.Rechazo(MotivoRechazoCentral.UsuarioBloqueado, usuario.BloqueadoHasta);

        if (!hashContrasenas.Verificar(actual ?? string.Empty, usuario.ContrasenaHash))
        {
            var bloqueado = await RegistrarFalloAsync(usuario, ahora, cancelacion);
            if (bloqueado)
                await RevocarAsync(s => s.UsuarioId == usuario.Id, ahora, "Usuario bloqueado por intentos fallidos", cancelacion);

            Auditar("Seguridad.CambioContrasenaRechazado", usuario, new { Bloqueado = bloqueado, origen.DireccionIp });
            await contexto.SaveChangesAsync(cancelacion);
            return bloqueado
                ? ResultadoSesionCentral.Rechazo(MotivoRechazoCentral.UsuarioBloqueado, usuario.BloqueadoHasta)
                : ResultadoSesionCentral.Rechazo(MotivoRechazoCentral.ContrasenaNoCumple, detalle: "La contraseña actual es incorrecta.");
        }

        var largoMinimo = await parametros.ObtenerEnteroPositivoAsync(ClavesParametrosCentral.LargoMinimoContrasena, cancelacion);
        var compleja = await parametros.ObtenerBooleanoOpcionalAsync(ClavesParametrosCentral.ContrasenaCompleja, cancelacion);
        var problema = ReglasContrasena.Validar(nueva, largoMinimo, compleja, usuario.Codigo)
            ?? (hashContrasenas.Verificar(nueva, usuario.ContrasenaHash) ? "La contraseña nueva debe ser distinta de la actual." : null);
        if (problema is not null)
            return ResultadoSesionCentral.Rechazo(MotivoRechazoCentral.ContrasenaNoCumple, detalle: problema);

        var rol = await CargarRolAsync(usuario.RolId, cancelacion);
        if (rol is not { Activo: true })
            return ResultadoSesionCentral.Rechazo(MotivoRechazoCentral.RolInactivo);

        var (inactividad, duracion) = await DuracionesAsync(cancelacion);
        usuario.CambiarContrasena(hashContrasenas.Hash(nueva), debeCambiar: false, ahora);
        usuario.RegistrarIngresoExitoso(ahora);

        // Las sesiones abiertas con la contraseña anterior se cierran (incluida la actual) y se inicia una nueva.
        await RevocarAsync(s => s.UsuarioId == usuario.Id, ahora, "Cambio de contraseña", cancelacion);
        var (sesion, token) = IniciarSesion(usuario, ahora, inactividad, duracion, origen);

        Auditar("Seguridad.ContrasenaCambiada", usuario, new { Sesion = sesion.Familia, origen.DireccionIp });
        await contexto.SaveChangesAsync(cancelacion);

        return ResultadoSesionCentral.Exito(CrearSesion(usuario, rol, sesion.Familia), token, sesion.ExpiraEn);
    }

    public async Task<ResultadoAutorizacionCentral> AutorizarConPermisoAsync(string codigo, string contrasena, string permiso, OrigenSolicitud origen,
        CancellationToken cancelacion = default)
    {
        var codigoLimpio = codigo?.Trim() ?? string.Empty;
        contrasena ??= string.Empty;
        var ahora = reloj.Ahora();

        var usuario = codigoLimpio.Length == 0 ? null : await contexto.UsuariosCentral.SingleOrDefaultAsync(u => u.Codigo == codigoLimpio, cancelacion);
        if (usuario is null)
        {
            // Un usuario inexistente tarda lo mismo que una contraseña incorrecta.
            hashContrasenas.Verificar(contrasena, _hashFicticio ??= hashContrasenas.Hash(Guid.NewGuid().ToString()));
            return await RechazarAutorizacionAsync(null, MotivoRechazoCentral.CredencialesInvalidas, permiso, origen, cancelacion, codigoLimpio);
        }

        if (usuario.EstaBloqueado(ahora))
            return await RechazarAutorizacionAsync(usuario, MotivoRechazoCentral.UsuarioBloqueado, permiso, origen, cancelacion);

        // El mismo contador de intentos y el mismo bloqueo del ingreso: aquí tampoco se pueden probar contraseñas sin límite.
        if (!hashContrasenas.Verificar(contrasena, usuario.ContrasenaHash))
        {
            var bloqueado = await RegistrarFalloAsync(usuario, ahora, cancelacion);
            return await RechazarAutorizacionAsync(usuario, bloqueado ? MotivoRechazoCentral.UsuarioBloqueado : MotivoRechazoCentral.CredencialesInvalidas,
                permiso, origen, cancelacion);
        }

        if (!usuario.Activo)
            return await RechazarAutorizacionAsync(usuario, MotivoRechazoCentral.UsuarioInactivo, permiso, origen, cancelacion);

        if (usuario.DebeCambiarContrasena)
            return await RechazarAutorizacionAsync(usuario, MotivoRechazoCentral.DebeCambiarContrasena, permiso, origen, cancelacion);

        var rol = await CargarRolAsync(usuario.RolId, cancelacion);
        if (rol is not { Activo: true })
            return await RechazarAutorizacionAsync(usuario, MotivoRechazoCentral.RolInactivo, permiso, origen, cancelacion);

        if (!rol.PermisosAsignados.Any(p => string.Equals(p.PermisoCodigo, permiso, StringComparison.Ordinal)))
            return await RechazarAutorizacionAsync(usuario, MotivoRechazoCentral.PermisoInsuficiente, permiso, origen, cancelacion);

        // La contraseña era buena: se limpian los intentos fallidos acumulados, como en un ingreso normal.
        usuario.RegistrarIngresoExitoso(ahora);
        await contexto.SaveChangesAsync(cancelacion);
        return ResultadoAutorizacionCentral.Exito(new UsuarioAuditoria(usuario.Id, usuario.Nombre));
    }

    private async Task<ResultadoAutorizacionCentral> RechazarAutorizacionAsync(UsuarioCentral? usuario, MotivoRechazoCentral motivo, string permiso,
        OrigenSolicitud origen, CancellationToken cancelacion, string? codigoIntentado = null)
    {
        const string Accion = "Seguridad.AutorizacionRechazada";
        var detalle = new { Motivo = motivo.ToString(), Permiso = permiso, CodigoIntentado = codigoIntentado, origen.DireccionIp, origen.AgenteUsuario };

        if (usuario is null)
            auditoria.Registrar(new EntradaAuditoria(Accion, TipoEntidad, Detalle: detalle));
        else
            Auditar(Accion, usuario, detalle);

        await contexto.SaveChangesAsync(cancelacion);
        return ResultadoAutorizacionCentral.Rechazo(motivo, motivo == MotivoRechazoCentral.UsuarioBloqueado ? usuario?.BloqueadoHasta : null);
    }

    private (SesionCentral Sesion, string Token) IniciarSesion(UsuarioCentral usuario, DateTimeOffset ahora, TimeSpan inactividad, TimeSpan duracion, OrigenSolicitud origen)
    {
        var token = TokensSeguros.Generar();
        var sesion = SesionCentral.Iniciar(usuario.Id, TokensSeguros.Hash(token), ahora, inactividad, duracion, origen.DireccionIp, origen.AgenteUsuario);
        contexto.SesionesCentral.Add(sesion);
        return (sesion, token);
    }

    private async Task<bool> RegistrarFalloAsync(UsuarioCentral usuario, DateTimeOffset ahora, CancellationToken cancelacion)
    {
        var intentos = await parametros.ObtenerEnteroPositivoAsync(ClavesParametrosCentral.IntentosMaximos, cancelacion);
        var minutos = await parametros.ObtenerEnteroPositivoAsync(ClavesParametrosCentral.MinutosBloqueo, cancelacion);
        return usuario.RegistrarIngresoFallido(ahora, intentos, TimeSpan.FromMinutes(minutos));
    }

    private async Task<(TimeSpan Inactividad, TimeSpan Duracion)> DuracionesAsync(CancellationToken cancelacion)
    {
        var minutosInactividad = await parametros.ObtenerEnteroPositivoAsync(ClavesParametrosCentral.MinutosInactividad, cancelacion);
        var horasSesion = await parametros.ObtenerEnteroPositivoAsync(ClavesParametrosCentral.HorasSesion, cancelacion);
        return (TimeSpan.FromMinutes(minutosInactividad), TimeSpan.FromHours(horasSesion));
    }

    private async Task RevocarAsync(System.Linq.Expressions.Expression<Func<SesionCentral, bool>> filtro, DateTimeOffset ahora, string motivo, CancellationToken cancelacion)
    {
        foreach (var sesion in await contexto.SesionesCentral.Where(filtro).Where(s => s.RevocadaEn == null).ToListAsync(cancelacion))
            sesion.Revocar(ahora, motivo);
    }

    private Task<RolCentral?> CargarRolAsync(int rolId, CancellationToken cancelacion) =>
        contexto.RolesCentral.Include(r => r.PermisosAsignados).AsNoTracking().SingleOrDefaultAsync(r => r.Id == rolId, cancelacion);

    private async Task<ResultadoSesionCentral> RechazarIngresoAsync(UsuarioCentral? usuario, MotivoRechazoCentral motivo, OrigenSolicitud origen,
        CancellationToken cancelacion, string? codigoIntentado = null)
    {
        var accion = motivo == MotivoRechazoCentral.UsuarioBloqueado ? "Seguridad.IngresoBloqueado" : "Seguridad.IngresoRechazado";
        var detalle = new { Motivo = motivo.ToString(), CodigoIntentado = codigoIntentado, origen.DireccionIp, origen.AgenteUsuario };

        if (usuario is null)
            auditoria.Registrar(new EntradaAuditoria(accion, TipoEntidad, Detalle: detalle));
        else
            Auditar(accion, usuario, detalle);

        await contexto.SaveChangesAsync(cancelacion);
        return ResultadoSesionCentral.Rechazo(motivo, motivo == MotivoRechazoCentral.UsuarioBloqueado ? usuario?.BloqueadoHasta : null);
    }

    private void Auditar(string accion, UsuarioCentral usuario, object detalle) =>
        auditoria.Registrar(new EntradaAuditoria(accion, TipoEntidad, usuario.Id.ToString(), detalle, Usuario: new UsuarioAuditoria(usuario.Id, usuario.Nombre)));

    private static SesionCentralUsuario CrearSesion(UsuarioCentral usuario, RolCentral rol, Guid sesionId) =>
        new(
            usuario.Id,
            usuario.Codigo,
            usuario.Nombre,
            rol.Id,
            rol.Codigo,
            rol.Nombre,
            rol.PermisosAsignados.Select(p => p.PermisoCodigo).ToFrozenSet(StringComparer.Ordinal),
            usuario.DebeCambiarContrasena,
            sesionId);
}
