using CgPos.Central.Aplicacion.Abstracciones;

namespace CgPos.Central.Aplicacion.Dispositivos;

/// <summary>Caja autenticada con su credencial de dispositivo.</summary>
public sealed record DispositivoAutenticado(Guid CajaId, int CajaCodigo, string CajaNombre, Guid SucursalId, int SucursalCodigo, Guid CredencialId);

public enum MotivoRechazoDispositivo
{
    CredencialInvalida,
    CajaDeshabilitada,
    SucursalInactiva,
}

public sealed record ResultadoDispositivo(DispositivoAutenticado? Dispositivo, MotivoRechazoDispositivo? Motivo)
{
    public bool Exitoso => Dispositivo is not null;

    public static ResultadoDispositivo Exito(DispositivoAutenticado dispositivo) => new(dispositivo, null);

    public static ResultadoDispositivo Rechazo(MotivoRechazoDispositivo motivo) => new(null, motivo);
}

public sealed record CredencialEmitida(Guid CajaId, int SucursalCodigo, int CajaCodigo, string Secreto, DateTimeOffset EmitidaEn);

public interface IServicioDispositivos
{
    /// <summary>Emite una credencial nueva para la caja y revoca la anterior. El secreto solo se devuelve aquí.</summary>
    /// <returns>Nulo si la caja no existe.</returns>
    Task<CredencialEmitida?> EmitirCredencialAsync(Guid cajaId, UsuarioAuditoria emisor, CancellationToken cancelacion = default);

    /// <returns><c>false</c> si la caja no tiene una credencial activa.</returns>
    Task<bool> RevocarCredencialAsync(Guid cajaId, string motivo, UsuarioAuditoria usuario, CancellationToken cancelacion = default);

    /// <summary>Valida la credencial de la caja y que la caja y su sucursal estén habilitadas. Los rechazos quedan en auditoría.</summary>
    Task<ResultadoDispositivo> AutenticarAsync(int sucursalCodigo, int cajaCodigo, string secreto, OrigenSolicitud origen, CancellationToken cancelacion = default);

    /// <summary>La credencial sigue activa y la caja habilitada: se consulta con cada token de dispositivo.</summary>
    Task<bool> EsCredencialActivaAsync(Guid credencialId, Guid cajaId, CancellationToken cancelacion = default);
}

public static class MensajesDispositivos
{
    public static string Para(MotivoRechazoDispositivo motivo) => motivo switch
    {
        MotivoRechazoDispositivo.CredencialInvalida => "Credencial de caja no válida.",
        MotivoRechazoDispositivo.CajaDeshabilitada => "La caja está deshabilitada en el Central.",
        MotivoRechazoDispositivo.SucursalInactiva => "La sucursal de la caja está inactiva.",
        _ => "No se pudo autenticar la caja.",
    };
}
