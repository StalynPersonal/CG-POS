using CgPos.Contratos.Sincronizacion;

namespace CgPos.Pos.Aplicacion.Sincronizacion;

/// <summary>Claves de configuración de la conexión con el Central (por instalación, en appsettings).</summary>
public static class ClavesSincronizacion
{
    /// <summary>"Simulado" usa el Central simulado en carpeta local; vacío usa <see cref="UrlCentral"/> si está configurada.</summary>
    public const string ModoCentral = "Central:Modo";

    public const string UrlCentral = "Central:Url";

    /// <summary>Secreto de la credencial de dispositivo que el Central emitió para esta caja (se configura al instalarla).</summary>
    public const string SecretoCaja = "Central:Secreto";

    /// <summary>Id de la caja en esta instalación; con el secreto forma la credencial ante el Central.</summary>
    public const string CajaId = "Caja:Id";

    /// <summary>Carpeta donde el Central simulado guarda los mensajes recibidos.</summary>
    public const string CarpetaSimulada = "Central:CarpetaSimulada";

    public const string IntervaloSegundos = "Sincronizacion:IntervaloSegundos";
    public const string TamanoLote = "Sincronizacion:TamanoLote";
    public const string EsperaInicialSegundos = "Sincronizacion:EsperaInicialSegundos";
    public const string EsperaMaximaSegundos = "Sincronizacion:EsperaMaximaSegundos";
    public const string TiempoEsperaSegundos = "Sincronizacion:TiempoEsperaSegundos";

    public const string ModoSimulado = "Simulado";
}

/// <summary>Resultado de enviar un mensaje al Central.</summary>
/// <param name="CentralRespondio">Falso si no hubo comunicación (red, tiempo de espera, error del servidor): se reintenta con espera progresiva.</param>
public sealed record ResultadoEnvioCentral(bool Confirmado, bool CentralRespondio, string? Error)
{
    public static ResultadoEnvioCentral Recibido() => new(true, true, null);

    public static ResultadoEnvioCentral Rechazado(string error) => new(false, true, error);

    public static ResultadoEnvioCentral SinConexion(string error) => new(false, false, error);
}

/// <summary>Canal hacia el Central: HTTP en producción o simulado mientras no exista (Fase C11, H2).</summary>
public interface IClienteCentral
{
    bool Configurado { get; }

    Task<ResultadoEnvioCentral> EnviarAsync(MensajeSincronizacion mensaje, CancellationToken cancelacion = default);
}

/// <summary>Último contacto con el Central, para el indicador de conexión (RF-192).</summary>
public interface IEstadoConexionCentral
{
    DateTimeOffset? UltimoContacto { get; }
    DateTimeOffset? UltimoFallo { get; }
    string? UltimoError { get; }

    void RegistrarContacto(DateTimeOffset momento);

    void RegistrarFallo(DateTimeOffset momento, string? error);
}

public sealed record ResultadoProcesoBandeja(int Tomados, int Confirmados, int Fallidos);

/// <summary>Envía al Central los mensajes pendientes de la bandeja de salida con reintentos y espera progresiva (RF-270).</summary>
public interface IProcesadorBandejaSalida
{
    Task<ResultadoProcesoBandeja> ProcesarAsync(CancellationToken cancelacion = default);
}
