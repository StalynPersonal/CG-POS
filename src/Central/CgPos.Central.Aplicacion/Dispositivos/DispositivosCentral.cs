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

    /// <summary>La credencial es buena, pero viene de un equipo distinto al que está atada: alguien la copió.</summary>
    EquipoNoAutorizado,
}

public sealed record ResultadoDispositivo(DispositivoAutenticado? Dispositivo, MotivoRechazoDispositivo? Motivo)
{
    public bool Exitoso => Dispositivo is not null;

    public static ResultadoDispositivo Exito(DispositivoAutenticado dispositivo) => new(dispositivo, null);

    public static ResultadoDispositivo Rechazo(MotivoRechazoDispositivo motivo) => new(null, motivo);
}

public sealed record CredencialEmitida(int CajaId, string SucursalCodigo, string CajaCodigo, string Secreto, DateTimeOffset EmitidaEn);

/// <summary>Resultado de una solicitud de enrolamiento; el secreto solo viene cuando la caja recoge su credencial.</summary>
public sealed record ResultadoEnrolamiento(EstadoEnrolamientoCaja Estado, string? Secreto = null, string? Mensaje = null);

public interface IServicioDispositivos
{
    /// <summary>
    /// La caja pide entrar. Si no hay solicitud, se registra y queda pendiente; si ya fue aceptada en el Central, se le emite
    /// la credencial y se le entrega aquí, una sola vez.
    /// </summary>
    Task<ResultadoEnrolamiento> SolicitarEnrolamientoAsync(SolicitudEnrolamientoCaja solicitud, OrigenSolicitud origen, CancellationToken cancelacion = default);

    /// <summary>Solicitudes para decidir en el Central, las pendientes primero.</summary>
    Task<IReadOnlyList<DatosSolicitudEnrolamiento>> ListarSolicitudesAsync(bool soloPendientes, CancellationToken cancelacion = default);

    /// <returns>El motivo por el que no se pudo aceptar, o nulo si se aceptó.</returns>
    Task<string?> AprobarSolicitudAsync(int solicitudId, UsuarioAuditoria usuario, CancellationToken cancelacion = default);

    Task<bool> RechazarSolicitudAsync(int solicitudId, string motivo, UsuarioAuditoria usuario, CancellationToken cancelacion = default);

    /// <summary>Suelta la credencial de la caja del equipo al que estaba atada, para instalarla en otro.</summary>
    /// <returns><c>false</c> si la caja no tiene credencial activa.</returns>
    Task<bool> LiberarEquipoAsync(int cajaId, string motivo, UsuarioAuditoria usuario, CancellationToken cancelacion = default);

    /// <summary>Emite una credencial nueva para la caja y revoca la anterior. El secreto solo se devuelve aquí.</summary>
    /// <returns>Nulo si la caja no existe.</returns>
    Task<CredencialEmitida?> EmitirCredencialAsync(int cajaId, UsuarioAuditoria emisor, CancellationToken cancelacion = default);

    /// <returns><c>false</c> si la caja no tiene una credencial activa.</returns>
    Task<bool> RevocarCredencialAsync(int cajaId, string motivo, UsuarioAuditoria usuario, CancellationToken cancelacion = default);

    /// <summary>Valida la credencial de la caja y que la caja y su sucursal estén habilitadas. Los rechazos quedan en auditoría.</summary>
    Task<ResultadoDispositivo> AutenticarAsync(string sucursalCodigo, string cajaCodigo, string secreto, string? huellaEquipo, OrigenSolicitud origen,
        CancellationToken cancelacion = default);

    /// <summary>La credencial sigue activa y la caja habilitada: se consulta con cada token de dispositivo.</summary>
    Task<bool> EsCredencialActivaAsync(int credencialId, int cajaId, CancellationToken cancelacion = default);
}

public static class MensajesDispositivos
{
    public static string Para(MotivoRechazoDispositivo motivo) => motivo switch
    {
        MotivoRechazoDispositivo.CredencialInvalida => "Credencial de caja no válida.",
        MotivoRechazoDispositivo.CajaDeshabilitada => "La caja está deshabilitada en el Central.",
        MotivoRechazoDispositivo.SucursalInactiva => "La sucursal de la caja está inactiva.",
        MotivoRechazoDispositivo.EquipoNoAutorizado => "La credencial de esta caja está atada a otro equipo: libérela en el Central si cambió de máquina.",
        _ => "No se pudo autenticar la caja.",
    };
}
