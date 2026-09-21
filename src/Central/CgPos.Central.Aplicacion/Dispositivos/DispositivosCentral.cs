using CgPos.Central.Aplicacion.Abstracciones;
using CgPos.Contratos.Central;

namespace CgPos.Central.Aplicacion.Dispositivos;

/// <summary>Caja autenticada con su credencial de dispositivo.</summary>
public sealed record DispositivoAutenticado(int CajaId, string CajaCodigo, string CajaNombre, int SucursalId, string SucursalCodigo, int CredencialId);

public enum MotivoRechazoDispositivo
{
    CredencialInvalida,
    CajaDeshabilitada,
    SucursalInactiva,

    /// <summary>La credencial es buena, pero la caja dice tener otra dirección de red que la registrada.</summary>
    IpNoAutorizada,
}

public sealed record ResultadoDispositivo(DispositivoAutenticado? Dispositivo, MotivoRechazoDispositivo? Motivo)
{
    public bool Exitoso => Dispositivo is not null;

    public static ResultadoDispositivo Exito(DispositivoAutenticado dispositivo) => new(dispositivo, null);

    public static ResultadoDispositivo Rechazo(MotivoRechazoDispositivo motivo) => new(null, motivo);
}

public sealed record CredencialEmitida(int CajaId, string SucursalCodigo, string CajaCodigo, string Secreto, DateTimeOffset EmitidaEn);

/// <summary>Lo que dejó el intento de emitir una credencial.</summary>
/// <param name="Credencial">La credencial recién emitida; nula si no se emitió.</param>
/// <param name="CajaNoExiste">El Id no corresponde a ninguna caja del Central.</param>
/// <param name="Rechazo">Por qué no se emitió, cuando la caja sí existe.</param>
public sealed record ResultadoEmisionCredencial(CredencialEmitida? Credencial, bool CajaNoExiste = false, string? Rechazo = null);

public interface IServicioDispositivos
{
    /// <summary>
    /// Emite la credencial de una caja que no tiene. El secreto solo se devuelve aquí, y no se puede volver a ver.
    /// Si la caja ya tiene una vigente hay que revocarla primero: así queda el motivo de por qué se cambió.
    /// </summary>
    Task<ResultadoEmisionCredencial> EmitirCredencialAsync(int cajaId, UsuarioAuditoria emisor, CancellationToken cancelacion = default);

    /// <returns><c>false</c> si la caja no tiene una credencial activa.</returns>
    Task<bool> RevocarCredencialAsync(int cajaId, string motivo, UsuarioAuditoria usuario, CancellationToken cancelacion = default);

    /// <summary>Valida la credencial de la caja y que la caja y su sucursal estén habilitadas. Los rechazos quedan en auditoría.</summary>
    Task<ResultadoDispositivo> AutenticarAsync(string sucursalCodigo, string cajaCodigo, string secreto, string? direccionIp, OrigenSolicitud origen,
        CancellationToken cancelacion = default);

    /// <summary>La credencial sigue activa y la caja habilitada: se consulta con cada token de dispositivo.</summary>
    Task<bool> EsCredencialActivaAsync(int credencialId, int cajaId, CancellationToken cancelacion = default);

    /// <summary>
    /// Autoriza que se configure una caja desde su propio equipo: primero el usuario del Central que lo pide (con el permiso
    /// de configurar cajas) y después la credencial, la caja y la dirección que trae. Queda en la auditoría quién la configuró.
    /// </summary>
    Task<RespuestaValidarConfiguracionCaja> ValidarConfiguracionAsync(SolicitudValidarConfiguracionCaja solicitud, OrigenSolicitud origen,
        CancellationToken cancelacion = default);
}

public static class MensajesDispositivos
{
    public static string Para(MotivoRechazoDispositivo motivo) => motivo switch
    {
        MotivoRechazoDispositivo.CredencialInvalida => "Credencial de caja no válida.",
        MotivoRechazoDispositivo.CajaDeshabilitada => "La caja está deshabilitada en el Central.",
        MotivoRechazoDispositivo.SucursalInactiva => "La sucursal de la caja está inactiva.",
        MotivoRechazoDispositivo.IpNoAutorizada => "La dirección de red de esta caja no es la registrada en el Central.",
        _ => "No se pudo autenticar la caja.",
    };
}
