using CgPos.Central.Aplicacion.Abstracciones;
using CgPos.Central.Aplicacion.Dispositivos;
using CgPos.Contratos.Central;
using CgPos.Central.Infraestructura.Persistencia;
using CgPos.Central.Aplicacion.Seguridad;
using CgPos.Central.Infraestructura.Seguridad;
using CgPos.Dominio.Seguridad;
using CgPos.Dominio.Organizacion;
using Microsoft.EntityFrameworkCore;
using CgPos.Dominio.Comun;

namespace CgPos.Central.Infraestructura.Dispositivos;

internal sealed class ServicioDispositivos(ContextoDatosCentral contexto, IAuditoriaCentral auditoria, IServicioSesionesCentral sesiones, TimeProvider reloj)
    : IServicioDispositivos
{
    private const string TipoEntidad = "Caja";

    public async Task<ResultadoEmisionCredencial> EmitirCredencialAsync(int cajaId, UsuarioAuditoria emisor, CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(emisor);

        var caja = await contexto.Cajas.AsNoTracking().SingleOrDefaultAsync(c => c.Id == cajaId, cancelacion);
        if (caja is null)
            return new ResultadoEmisionCredencial(null, CajaNoExiste: true);

        // Con una credencial vigente no se emite otra: hay que revocarla, que pide el motivo y queda auditado. Reemplazarla
        // en silencio dejaba a la caja sin sincronizar y sin nada escrito que dijera por qué.
        if (await contexto.CredencialesDispositivo.AsNoTracking().AnyAsync(c => c.CajaId == cajaId && c.RevocadaEn == null, cancelacion))
            return new ResultadoEmisionCredencial(null,
                Rechazo: $"La caja {caja.Codigo} ya tiene una credencial vigente. Revóquela antes de emitir otra.");

        var ahora = reloj.Ahora();
        var secreto = TokensSeguros.Generar();
        var credencial = CredencialDispositivo.Emitir(cajaId, TokensSeguros.Hash(secreto), ahora, emisor.Nombre);
        contexto.CredencialesDispositivo.Add(credencial);
        auditoria.Registrar(new EntradaAuditoria("Dispositivos.CredencialEmitida", TipoEntidad, cajaId.ToString(),
            new { Caja = caja.Codigo, Credencial = credencial.Id }, Usuario: emisor));
        await contexto.SaveChangesAsync(cancelacion);

        var sucursalCodigo = await contexto.Sucursales.AsNoTracking().Where(s => s.Id == caja.SucursalId).Select(s => s.Codigo).SingleAsync(cancelacion);
        return new ResultadoEmisionCredencial(new CredencialEmitida(caja.Id, sucursalCodigo, caja.Codigo, secreto, ahora));
    }

    public async Task<bool> RevocarCredencialAsync(int cajaId, string motivo, UsuarioAuditoria usuario, CancellationToken cancelacion = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(motivo);
        ArgumentNullException.ThrowIfNull(usuario);

        var activa = await contexto.CredencialesDispositivo.SingleOrDefaultAsync(c => c.CajaId == cajaId && c.RevocadaEn == null, cancelacion);
        if (activa is null)
            return false;

        activa.Revocar(reloj.Ahora(), motivo);
        auditoria.Registrar(new EntradaAuditoria("Dispositivos.CredencialRevocada", TipoEntidad, cajaId.ToString(), new { Credencial = activa.Id },
            motivo.Trim(), usuario));
        await contexto.SaveChangesAsync(cancelacion);
        return true;
    }

    public async Task<ResultadoDispositivo> AutenticarAsync(string sucursalCodigo, string cajaCodigo, string secreto, string? direccionIp,
        OrigenSolicitud origen, CancellationToken cancelacion = default)
    {
        var cajaId = await contexto.Cajas.AsNoTracking()
            .Where(c => c.Codigo == cajaCodigo && contexto.Sucursales.Any(s => s.Id == c.SucursalId && s.Codigo == sucursalCodigo))
            .Select(c => c.Id)
            .FirstOrDefaultAsync(cancelacion);
        var credencial = cajaId <= 0 || string.IsNullOrWhiteSpace(secreto)
            ? null
            : await contexto.CredencialesDispositivo.SingleOrDefaultAsync(c => c.CajaId == cajaId && c.RevocadaEn == null, cancelacion);

        if (credencial is null || !TokensSeguros.Coincide(secreto.Trim(), credencial.SecretoHash))
            return await RechazarAsync(cajaId, MotivoRechazoDispositivo.CredencialInvalida, origen, cancelacion);

        var datos = await (
                from caja in contexto.Cajas
                join sucursal in contexto.Sucursales on caja.SucursalId equals sucursal.Id
                where caja.Id == cajaId
                select new
                {
                    caja.Codigo,
                    caja.Nombre,
                    caja.Habilitada,
                    caja.DireccionIp,
                    SucursalId = sucursal.Id,
                    SucursalCodigo = sucursal.Codigo,
                    SucursalActiva = sucursal.Activa,
                })
            .AsNoTracking()
            .SingleAsync(cancelacion);

        if (!datos.Habilitada)
            return await RechazarAsync(cajaId, MotivoRechazoDispositivo.CajaDeshabilitada, origen, cancelacion);
        if (!datos.SucursalActiva)
            return await RechazarAsync(cajaId, MotivoRechazoDispositivo.SucursalInactiva, origen, cancelacion);

        // La caja dice cuál es su dirección de red y tiene que ser la que el Central tiene registrada para ella. Una caja sin
        // dirección configurada en el Central no se comprueba, para no dejar fuera a las que se instalaron antes.
        if (!CoincideIp(datos.DireccionIp, direccionIp))
            return await RechazarAsync(cajaId, MotivoRechazoDispositivo.IpNoAutorizada, origen, cancelacion);

        // La dirección desde la que llega de verdad no bloquea nada, pero si no es la suya queda anotado: así se ve si
        // alguien copió la configuración de una caja a otro equipo.
        if (origen.DireccionIp is { Length: > 0 } real && !CoincideIp(datos.DireccionIp, real))
            auditoria.Registrar(new EntradaAuditoria("Dispositivos.DireccionInesperada", TipoEntidad, cajaId.ToString(),
                new { Caja = datos.Codigo, Registrada = datos.DireccionIp, Declarada = direccionIp, Origen = real }));

        credencial.RegistrarUso(reloj.Ahora(), origen.DireccionIp);
        await contexto.SaveChangesAsync(cancelacion);

        return ResultadoDispositivo.Exito(new DispositivoAutenticado(cajaId, datos.Codigo, datos.Nombre, datos.SucursalId, datos.SucursalCodigo, credencial.Id));
    }


    public async Task<RespuestaValidarConfiguracionCaja> ValidarConfiguracionAsync(SolicitudValidarConfiguracionCaja solicitud, OrigenSolicitud origen,
        CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(solicitud);

        // Primero la persona: así este endpoint no sirve para probar credenciales de cajas sin ser nadie.
        var autorizacion = await sesiones.AutorizarConPermisoAsync(solicitud.Usuario ?? string.Empty, solicitud.Contrasena ?? string.Empty,
            CatalogoPermisosCentral.ConfigurarCajas, origen, cancelacion);
        if (autorizacion.Usuario is not { } autorizador)
            return new RespuestaValidarConfiguracionCaja(false, MensajesSeguridadCentral.Para(autorizacion.Motivo!.Value, autorizacion.BloqueadoHasta));

        // Después el equipo: la credencial, la caja, su sucursal y la dirección tienen que cuadrar, igual que en cada comunicación.
        var dispositivo = await AutenticarAsync(solicitud.SucursalCodigo ?? string.Empty, solicitud.CajaCodigo ?? string.Empty,
            solicitud.Secreto ?? string.Empty, solicitud.DireccionIp, origen, cancelacion);
        if (dispositivo.Dispositivo is not { } caja)
            return new RespuestaValidarConfiguracionCaja(false, MensajesDispositivos.Para(dispositivo.Motivo!.Value));

        var sucursalNombre = await contexto.Sucursales.AsNoTracking().Where(s => s.Id == caja.SucursalId).Select(s => s.Nombre).SingleAsync(cancelacion);
        auditoria.Registrar(new EntradaAuditoria("Dispositivos.CajaConfigurada", TipoEntidad, caja.CajaId.ToString(),
            new { Caja = $"{caja.SucursalCodigo}-{caja.CajaCodigo}", Declarada = solicitud.DireccionIp, Origen = origen.DireccionIp },
            Usuario: autorizador));
        await contexto.SaveChangesAsync(cancelacion);

        return new RespuestaValidarConfiguracionCaja(true, SucursalNombre: sucursalNombre, CajaNombre: caja.CajaNombre);
    }

    public Task<bool> EsCredencialActivaAsync(int credencialId, int cajaId, CancellationToken cancelacion = default) =>
        contexto.CredencialesDispositivo.AnyAsync(c => c.Id == credencialId && c.CajaId == cajaId && c.RevocadaEn == null
            && contexto.Cajas.Any(caja => caja.Id == cajaId && caja.Habilitada), cancelacion);

    /// <summary>Una caja sin dirección registrada no se comprueba; con ella registrada, la que diga tiene que ser esa.</summary>
    private static bool CoincideIp(string? registrada, string? declarada) =>
        registrada is not { Length: > 0 } || string.Equals(registrada, declarada?.Trim(), StringComparison.OrdinalIgnoreCase);

    private async Task<ResultadoDispositivo> RechazarAsync(int cajaId, MotivoRechazoDispositivo motivo, OrigenSolicitud origen, CancellationToken cancelacion)
    {
        auditoria.Registrar(new EntradaAuditoria("Dispositivos.AutenticacionRechazada", TipoEntidad, cajaId <= 0 ? null : cajaId.ToString(),
            new { Motivo = motivo.ToString(), origen.DireccionIp, origen.AgenteUsuario }));
        await contexto.SaveChangesAsync(cancelacion);
        return ResultadoDispositivo.Rechazo(motivo);
    }
}
