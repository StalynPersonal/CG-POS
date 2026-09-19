namespace CgPos.Pos.Aplicacion.Sincronizacion;

/// <summary>Claves de configuración del mantenimiento de la caja (por instalación, en appsettings).</summary>
public static class ClavesMantenimiento
{
    public const string IntervaloMinutos = "Mantenimiento:IntervaloMinutos";

    /// <summary>Carpeta de los respaldos; vacía usa la carpeta de respaldos de la instancia de SQL Server.</summary>
    public const string CarpetaRespaldo = "Respaldo:Carpeta";

    /// <summary>Hora local (0-23) desde la que se toma el respaldo diario; vacía desactiva el respaldo automático.</summary>
    public const string HoraRespaldo = "Respaldo:Hora";

    /// <summary>Servidor de hora (NTP) contra el que se verifica el reloj de la caja; vacío desactiva la verificación.</summary>
    public const string ServidorHora = "Reloj:ServidorNtp";
}

public sealed record ResultadoRespaldo(bool Correcto, string? Ruta, string? Error, DateTimeOffset Fecha);

public sealed record ResultadoPurga(int XmlEliminados, int MensajesEliminados, int RespaldosEliminados);

/// <param name="Desfase">Cuánto va adelantado el servidor de hora respecto de la caja (negativo si la caja va adelantada).</param>
public sealed record ResultadoHora(bool Verificada, TimeSpan? Desfase, string? Servidor, string? Error, DateTimeOffset Fecha);

/// <summary>Resultados del último mantenimiento, en memoria del Agente, para las alertas.</summary>
public sealed class EstadoMantenimiento
{
    public ResultadoRespaldo? UltimoRespaldo { get; set; }
    public DateTimeOffset? UltimoRespaldoCorrecto { get; set; }
    public ResultadoHora? UltimaHora { get; set; }
}

/// <summary>
/// Mantenimiento de la caja (M14): respaldo diario de la base, purga controlada de lo ya confirmado por el Central, verificación de la hora y
/// alertas de tamaño de la base, documentos sin sincronizar y desfase del reloj.
/// </summary>
public interface IServicioMantenimiento
{
    Task<ResultadoRespaldo> RespaldarAsync(CancellationToken cancelacion = default);

    /// <summary>Borra los XML en Enviados y los mensajes confirmados más viejos que la retención configurada; lo pendiente nunca se purga (RN-19).</summary>
    Task<ResultadoPurga> PurgarAsync(CancellationToken cancelacion = default);

    Task<ResultadoHora> VerificarHoraAsync(CancellationToken cancelacion = default);

    Task<IReadOnlyList<string>> ObtenerAlertasAsync(CancellationToken cancelacion = default);

    /// <summary>Toca el respaldo diario: está activo, ya pasó la hora configurada y hoy no hay uno correcto.</summary>
    Task<bool> CorrespondeRespaldoAsync(DateTimeOffset ahoraLocal, CancellationToken cancelacion = default);
}
