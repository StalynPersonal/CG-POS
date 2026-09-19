using CgPos.Dominio.Comun;

namespace CgPos.Dominio.Organizacion;

/// <summary>
/// Lo que este equipo necesita saber para trabajar como una caja concreta: qué caja es, cuál es su dirección de red, dónde
/// está el Central y con qué credencial se identifica. Se llena una sola vez, en la propia pantalla de la caja.
/// </summary>
/// <remarks>
/// Vive en la base de la caja y no en un archivo de configuración: así no anda un secreto en texto plano por el equipo, y el
/// técnico no tiene que editar archivos para instalar una caja. El secreto se guarda cifrado; aquí llega ya cifrado.
/// </remarks>
public sealed class ConfiguracionCaja : Entidad
{
    public const int LargoMaximoUrl = 250;
    public const int LargoMaximoIp = 45;
    public const int LargoMaximoSecreto = 500;
    public const int LargoMaximoMotivo = 250;

    private ConfiguracionCaja()
    {
    }

    public string SucursalCodigo { get; private set; } = string.Empty;
    public string CajaCodigo { get; private set; } = string.Empty;

    /// <summary>Dirección de red fija de este equipo; tiene que ser la misma que el Central tiene registrada para esta caja.</summary>
    public string DireccionIp { get; private set; } = string.Empty;

    public string UrlCentral { get; private set; } = string.Empty;

    /// <summary>Credencial emitida por el Central, cifrada con la protección del equipo. Nunca se guarda en claro.</summary>
    public string SecretoCifrado { get; private set; } = string.Empty;

    public DateTimeOffset ConfiguradaEn { get; private set; }

    /// <summary>Quién la configuró desde la pantalla de la caja, si lo indicó.</summary>
    public string? ConfiguradaPor { get; private set; }

    /// <summary>
    /// El Central rechazó estos datos: credencial revocada, caja eliminada o dirección que ya no cuadra. La caja sigue
    /// vendiendo con lo que tiene, pero hay que volver a configurarla para que se comunique.
    /// </summary>
    public DateTimeOffset? RechazadaEn { get; private set; }

    public string? MotivoRechazo { get; private set; }

    /// <summary>Sirve para comunicarse: está completa y el Central no la ha rechazado.</summary>
    public bool Valida => RechazadaEn is null;

    public static ConfiguracionCaja Crear(string sucursalCodigo, string cajaCodigo, string direccionIp, string urlCentral, string secretoCifrado,
        DateTimeOffset ahora, string? configuradaPor = null) =>
        new()
        {
            SucursalCodigo = Validar.CodigoDosDigitos(sucursalCodigo, "Código de sucursal"),
            CajaCodigo = Validar.CodigoDosDigitos(cajaCodigo, "Código de caja"),
            DireccionIp = ValidarIp(direccionIp),
            UrlCentral = ValidarUrl(urlCentral),
            SecretoCifrado = Validar.Texto(secretoCifrado, "Credencial", LargoMaximoSecreto),
            ConfiguradaEn = ahora,
            ConfiguradaPor = configuradaPor is { Length: > 0 } ? Validar.Texto(configuradaPor, "Configurada por", LargoMaximoMotivo) : null,
        };

    /// <summary>El Central no acepta estos datos. No se borran: sirven para mostrar qué había configurado y qué pasó.</summary>
    public void Rechazar(string motivo, DateTimeOffset ahora)
    {
        RechazadaEn = ahora;
        MotivoRechazo = Validar.Texto(motivo, "Motivo del rechazo", LargoMaximoMotivo);
    }

    /// <summary>El Central volvió a aceptarla: por ejemplo, le devolvieron su dirección en el Central.</summary>
    public void Aceptar()
    {
        RechazadaEn = null;
        MotivoRechazo = null;
    }

    /// <exception cref="ArgumentException">No es una dirección de red válida.</exception>
    private static string ValidarIp(string direccionIp)
    {
        var texto = (direccionIp ?? string.Empty).Trim();
        if (!System.Net.IPAddress.TryParse(texto, out var direccion))
            throw new ArgumentException($"«{texto}» no es una dirección IP válida.", nameof(direccionIp));

        return direccion.ToString();
    }

    /// <exception cref="ArgumentException">No es una dirección de servidor válida.</exception>
    private static string ValidarUrl(string urlCentral)
    {
        var texto = (urlCentral ?? string.Empty).Trim();
        if (!Uri.TryCreate(texto, UriKind.Absolute, out var url) || (url.Scheme != Uri.UriSchemeHttp && url.Scheme != Uri.UriSchemeHttps))
            throw new ArgumentException($"«{texto}» no es una dirección de servidor válida: empiece con http:// o https://.", nameof(urlCentral));

        return Validar.Texto(url.ToString(), "Dirección del Central", LargoMaximoUrl);
    }
}
