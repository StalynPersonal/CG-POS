namespace CgPos.Contratos.Central;

/// <summary>Versión del Agente publicada por el Central para las cajas (H7).</summary>
/// <param name="Hash">SHA-256 (hex, mayúsculas) del paquete: la caja lo verifica antes de instalar.</param>
public sealed record DatosActualizacionCaja(string Version, string Archivo, string Hash, long Tamano, DateTimeOffset PublicadaEn);

/// <summary>La caja informa qué versión del Agente quedó instalada tras actualizarse.</summary>
public sealed record SolicitudVersionCaja(string Version);

/// <param name="AlDia">La caja ya tiene la versión que el Central publicó.</param>
public sealed record DatosVersionCaja(
    int CajaId,
    string SucursalCodigo,
    string SucursalNombre,
    string CajaCodigo,
    string CajaNombre,
    bool Habilitada,
    string? Version,
    DateTimeOffset? ReportadaEn,
    bool AlDia);

/// <summary>Padrón de la DGII publicado en el Central para que lo bajen todas las cajas.</summary>
/// <param name="Hash">SHA-256 (hex, mayúsculas) del archivo: la caja lo verifica y evita reimportar lo mismo.</param>
public sealed record DatosPadronPublicado(string Version, string Archivo, string Hash, long Tamano, DateTimeOffset PublicadoEn);
