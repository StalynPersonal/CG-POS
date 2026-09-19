using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Xml;
using CgPos.Central.Aplicacion.Dgii;
using CgPos.Central.Aplicacion.Organizacion;
using CgPos.Dominio.Organizacion;
using CgPos.Dominio.Sincronizacion;
using CgPos.ECF.Firma;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using CgPos.Dominio.Comun;

namespace CgPos.Central.ECF;

/// <param name="Cliente">"Simulado" (solo desarrollo) o "Http" (DGII real, el predeterminado).</param>
/// <param name="RutaCertificado">Certificado .p12 del emisor con el que se firma la semilla de autenticación.</param>
/// <param name="PinCertificado">PIN del certificado; va en la configuración segura del servidor, nunca en la base de datos.</param>
internal sealed record OpcionesDgii(string? Cliente, string? RutaCertificado, string? PinCertificado)
{
    public bool UsaSimulador => string.Equals(Cliente, "Simulado", StringComparison.OrdinalIgnoreCase);
}

/// <summary>DGII simulada para desarrollo: recibe todo y lo acepta (los e-CF en la primera consulta, los resúmenes de consumo al recibirlos).</summary>
internal sealed class ClienteDgiiSimulado(ILogger<ClienteDgiiSimulado> registro) : IClienteDgii
{
    public Task<RespuestaDgii> EnviarAsync(ComprobanteParaDgii comprobante, CancellationToken cancelacion = default)
    {
        registro.LogInformation("DGII simulada: recibido el e-CF {Encf}", comprobante.Encf);
        return Task.FromResult(comprobante.EsResumenConsumo
            ? new RespuestaDgii(ResultadoRespuestaDgii.Aceptado, Mensaje: "Resumen aceptado por la DGII simulada.")
            : new RespuestaDgii(ResultadoRespuestaDgii.EnProceso, $"SIM-{Guid.CreateVersion7():N}"));
    }

    public Task<RespuestaDgii> ConsultarAsync(string trackId, CancellationToken cancelacion = default) =>
        Task.FromResult(new RespuestaDgii(ResultadoRespuestaDgii.Aceptado, trackId, "Aceptado por la DGII simulada."));

    public Task<RespuestaDgii?> RecuperarAsync(ComprobanteParaDgii comprobante, CancellationToken cancelacion = default) =>
        Task.FromResult<RespuestaDgii?>(null);

    public Task<RespuestaAnulacionDgii> AnularAsync(string xmlAnulacion, CancellationToken cancelacion = default)
    {
        registro.LogInformation("DGII simulada: anulación de e-NCF recibida");
        return Task.FromResult(new RespuestaAnulacionDgii(true, false, "Anulación aceptada por la DGII simulada.", xmlAnulacion));
    }
}

/// <summary>Token de la DGII y certificado del emisor, compartidos por todas las llamadas.</summary>
internal sealed class SesionDgii(OpcionesDgii opciones) : IDisposable
{
    private readonly Lazy<X509Certificate2> _certificado = new(() =>
        string.IsNullOrWhiteSpace(opciones.RutaCertificado) || string.IsNullOrEmpty(opciones.PinCertificado)
            ? throw new InvalidOperationException("Falta el certificado del emisor para la DGII (Dgii:Certificado:Ruta y Dgii:Certificado:Pin en la configuración segura).")
            : CertificadoFirma.CargarPkcs12DesdeArchivo(opciones.RutaCertificado, opciones.PinCertificado));

    public SemaphoreSlim Cerrojo { get; } = new(1, 1);
    public string? Token { get; set; }
    public DateTimeOffset VenceEn { get; set; }
    public X509Certificate2 Certificado => _certificado.Value;

    public void Dispose()
    {
        if (_certificado.IsValueCreated)
            _certificado.Value.Dispose();
        Cerrojo.Dispose();
    }
}

/// <summary>
/// Servicios web de e-CF de la DGII: autenticación con la semilla firmada por el certificado del emisor, recepción del XML, consulta del
/// resultado, búsqueda de envíos por e-NCF y anulación de rangos. Cada dirección es un parámetro del Central (define el ambiente); los formatos
/// siguen la documentación técnica de la DGII y se confirman durante la certificación (TesteCF).
/// </summary>
internal sealed class ClienteDgiiHttp(HttpClient http, SesionDgii sesion, IServiceScopeFactory fabricaAmbitos, TimeProvider reloj, ILogger<ClienteDgiiHttp> registro)
    : IClienteDgii
{
    private static readonly JsonSerializerOptions OpcionesLectura = new(JsonSerializerDefaults.Web);

    private sealed record RespuestaTokenDgii(string? Token, DateTimeOffset? Expira);

    private sealed record RespuestaRecepcionDgii(string? TrackId, string? Error, string? Mensaje);

    private sealed record MensajeDgii(string? Valor, int? Codigo);

    /// <summary>Resultado por trackId, del resumen de consumo (en su envío y en su consulta) y de cada envío encontrado por e-NCF.</summary>
    private sealed record RespuestaEstadoDgii(string? TrackId, string? Estado, IReadOnlyList<MensajeDgii>? Mensajes, string? FechaRecepcion = null);

    public async Task<RespuestaDgii> EnviarAsync(ComprobanteParaDgii comprobante, CancellationToken cancelacion = default)
    {
        var nombre = $"{ValorXml(comprobante.XmlFirmado, "RNCEmisor")}{comprobante.Encf}.xml";
        var clave = comprobante.EsResumenConsumo ? ClavesParametrosCentral.DgiiUrlRecepcionConsumo : ClavesParametrosCentral.DgiiUrlRecepcion;
        using var respuesta = await EnviarAutenticadoAsync(clave,
            url => new HttpRequestMessage(HttpMethod.Post, url) { Content = ContenidoXml(comprobante.XmlFirmado, nombre) }, cancelacion);
        var cuerpo = await respuesta.Content.ReadAsStringAsync(cancelacion);

        // El resumen de consumo se acepta o se rechaza en el mismo envío, sin trackId.
        if (comprobante.EsResumenConsumo)
        {
            return Leer<RespuestaEstadoDgii>(cuerpo) is { Estado.Length: > 0 } resumen
                ? Resultado(resumen)
                : RespuestaDgii.Fallo($"La DGII respondió {(int)respuesta.StatusCode} al recibir el resumen de consumo: {Texto(cuerpo)}");
        }

        if (respuesta.StatusCode == HttpStatusCode.BadRequest)
            return new RespuestaDgii(ResultadoRespuestaDgii.Rechazado, Mensaje: Texto(cuerpo));
        if (!respuesta.IsSuccessStatusCode)
            return RespuestaDgii.Fallo($"La DGII respondió {(int)respuesta.StatusCode} al recibir el e-CF: {Texto(cuerpo)}");

        var datos = Leer<RespuestaRecepcionDgii>(cuerpo);
        return string.IsNullOrWhiteSpace(datos?.TrackId)
            ? RespuestaDgii.Fallo($"La DGII no devolvió el trackId: {datos?.Mensaje ?? datos?.Error ?? Texto(cuerpo)}")
            : new RespuestaDgii(ResultadoRespuestaDgii.EnProceso, datos.TrackId);
    }

    public async Task<RespuestaDgii> ConsultarAsync(string trackId, CancellationToken cancelacion = default)
    {
        using var respuesta = await EnviarAutenticadoAsync(ClavesParametrosCentral.DgiiUrlConsultaResultado,
            url => new HttpRequestMessage(HttpMethod.Get, Consulta(url, ("trackid", trackId))), cancelacion);
        var cuerpo = await respuesta.Content.ReadAsStringAsync(cancelacion);
        if (!respuesta.IsSuccessStatusCode)
            return RespuestaDgii.Fallo($"La DGII respondió {(int)respuesta.StatusCode} al consultar el resultado: {Texto(cuerpo)}");

        return Resultado(Leer<RespuestaEstadoDgii>(cuerpo) ?? new RespuestaEstadoDgii(trackId, null, null)) with { TrackId = trackId };
    }

    public async Task<RespuestaDgii?> RecuperarAsync(ComprobanteParaDgii comprobante, CancellationToken cancelacion = default)
    {
        var rnc = ValorXml(comprobante.XmlFirmado, "RNCEmisor");
        if (comprobante.EsResumenConsumo)
        {
            using var consulta = await EnviarAutenticadoAsync(ClavesParametrosCentral.DgiiUrlConsultaConsumo,
                url => new HttpRequestMessage(HttpMethod.Get, Consulta(url, ("RNC_Emisor", rnc), ("ENCF", comprobante.Encf),
                    ("Cod_Seguridad_eCF", ValorXml(comprobante.XmlFirmado, "CodigoSeguridadeCF")))), cancelacion);
            var cuerpoConsumo = await consulta.Content.ReadAsStringAsync(cancelacion);
            if (consulta.StatusCode == HttpStatusCode.NotFound)
                return null;
            if (!consulta.IsSuccessStatusCode)
                return RespuestaDgii.Fallo($"La DGII respondió {(int)consulta.StatusCode} al consultar el resumen de consumo: {Texto(cuerpoConsumo)}");

            return Leer<RespuestaEstadoDgii>(cuerpoConsumo) is { Estado.Length: > 0 } resumen && !EsNoEncontrado(resumen.Estado)
                ? Resultado(resumen)
                : null;
        }

        using var respuesta = await EnviarAutenticadoAsync(ClavesParametrosCentral.DgiiUrlConsultaTrackIds,
            url => new HttpRequestMessage(HttpMethod.Get, Consulta(url, ("RncEmisor", rnc), ("Encf", comprobante.Encf))), cancelacion);
        var cuerpo = await respuesta.Content.ReadAsStringAsync(cancelacion);
        if (respuesta.StatusCode == HttpStatusCode.NotFound)
            return null;
        if (!respuesta.IsSuccessStatusCode)
            return RespuestaDgii.Fallo($"La DGII respondió {(int)respuesta.StatusCode} al buscar los envíos del e-NCF: {Texto(cuerpo)}");

        // Si hubo varios envíos manda el último: su trackId es el que se sigue consultando.
        var envios = Leer<List<RespuestaEstadoDgii>>(cuerpo) ?? [];
        return envios.Where(e => !string.IsNullOrWhiteSpace(e.TrackId)).OrderByDescending(e => e.FechaRecepcion, StringComparer.Ordinal).FirstOrDefault() is { } ultimo
            ? Resultado(ultimo) with { TrackId = ultimo.TrackId }
            : null;
    }

    public async Task<RespuestaAnulacionDgii> AnularAsync(string xmlAnulacion, CancellationToken cancelacion = default)
    {
        var firmado = new FirmadorEcf().Firmar(xmlAnulacion, sesion.Certificado);
        var nombre = $"{ValorXml(firmado, "RncEmisor")}ANECF.xml";
        using var respuesta = await EnviarAutenticadoAsync(ClavesParametrosCentral.DgiiUrlAnulacion,
            url => new HttpRequestMessage(HttpMethod.Post, url) { Content = ContenidoXml(firmado, nombre) }, cancelacion);
        var cuerpo = await respuesta.Content.ReadAsStringAsync(cancelacion);

        if (respuesta.IsSuccessStatusCode)
            return new RespuestaAnulacionDgii(true, false, Texto(cuerpo), firmado);

        // Un 400 es un rechazo de la DGII; otro código es un error del servicio que se puede reintentar.
        return new RespuestaAnulacionDgii(false, respuesta.StatusCode != HttpStatusCode.BadRequest,
            $"La DGII respondió {(int)respuesta.StatusCode}: {Texto(cuerpo)}", firmado);
    }

    private static RespuestaDgii Resultado(RespuestaEstadoDgii datos)
    {
        var mensajes = string.Join(" ", (datos.Mensajes ?? []).Select(m => m.Valor).Where(v => !string.IsNullOrWhiteSpace(v)));
        var resultado = CgPos.Dominio.Comun.TextoBusqueda.Normalizar(datos.Estado)?.Replace(" ", string.Empty) switch
        {
            "aceptado" => ResultadoRespuestaDgii.Aceptado,
            "aceptadocondicional" => ResultadoRespuestaDgii.AceptadoCondicional,
            "rechazado" => ResultadoRespuestaDgii.Rechazado,
            _ => ResultadoRespuestaDgii.EnProceso,
        };
        return new RespuestaDgii(resultado, datos.TrackId, mensajes.Length == 0 ? null : mensajes);
    }

    private static bool EsNoEncontrado(string? estado) =>
        CgPos.Dominio.Comun.TextoBusqueda.Normalizar(estado)?.Replace(" ", string.Empty) is "noencontrado" or "noexiste";

    /// <param name="clave">Parámetro con la dirección del servicio.</param>
    private async Task<HttpResponseMessage> EnviarAutenticadoAsync(string clave, Func<Uri, HttpRequestMessage> crear, CancellationToken cancelacion)
    {
        var url = await UrlAsync(clave, cancelacion);
        for (var intento = 1; ; intento++)
        {
            using var solicitud = crear(url);
            solicitud.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await TokenAsync(cancelacion));
            var respuesta = await http.SendAsync(solicitud, cancelacion);
            if (respuesta.StatusCode != HttpStatusCode.Unauthorized || intento > 1)
                return respuesta;

            // Token vencido o revocado antes de lo informado: se pide uno nuevo una sola vez.
            respuesta.Dispose();
            sesion.Token = null;
        }
    }

    private async Task<string> TokenAsync(CancellationToken cancelacion)
    {
        if (sesion.Token is { } vigente && sesion.VenceEn > reloj.Ahora().AddMinutes(1))
            return vigente;

        await sesion.Cerrojo.WaitAsync(cancelacion);
        try
        {
            if (sesion.Token is { } obtenido && sesion.VenceEn > reloj.Ahora().AddMinutes(1))
                return obtenido;

            var semilla = await http.GetStringAsync(await UrlAsync(ClavesParametrosCentral.DgiiUrlSemilla, cancelacion), cancelacion);
            var firmada = new FirmadorEcf().Firmar(semilla, sesion.Certificado);
            using var contenido = ContenidoXml(firmada, "semilla.xml");
            using var respuesta = await http.PostAsync(await UrlAsync(ClavesParametrosCentral.DgiiUrlValidarSemilla, cancelacion), contenido, cancelacion);
            var cuerpo = await respuesta.Content.ReadAsStringAsync(cancelacion);
            if (!respuesta.IsSuccessStatusCode)
                throw new InvalidOperationException($"La DGII rechazó la autenticación ({(int)respuesta.StatusCode}): {Texto(cuerpo)}");

            var datos = Leer<RespuestaTokenDgii>(cuerpo);
            if (string.IsNullOrWhiteSpace(datos?.Token))
                throw new InvalidOperationException("La DGII no devolvió el token de autenticación.");

            sesion.Token = datos.Token;
            sesion.VenceEn = datos.Expira ?? reloj.Ahora().AddMinutes(1);
            registro.LogInformation("Autenticado en la DGII hasta {Vence}", sesion.VenceEn);
            return datos.Token;
        }
        finally
        {
            sesion.Cerrojo.Release();
        }
    }

    /// <summary>Dirección completa (https) de un servicio de la DGII, leída de su parámetro.</summary>
    private async Task<Uri> UrlAsync(string clave, CancellationToken cancelacion)
    {
        await using var ambito = fabricaAmbitos.CreateAsyncScope();
        var texto = await ambito.ServiceProvider.GetRequiredService<IParametrosCentral>().ObtenerRequeridoAsync(clave, cancelacion);
        return Uri.TryCreate(texto.Trim(), UriKind.Absolute, out var url) && url.Scheme == Uri.UriSchemeHttps
            ? url
            : throw new ParametroNoConfiguradoExcepcion(clave, "debe ser una dirección https");
    }

    private static Uri Consulta(Uri url, params (string Nombre, string Valor)[] valores) =>
        new($"{url.GetLeftPart(UriPartial.Path)}?{string.Join("&", valores.Select(v => $"{v.Nombre}={Uri.EscapeDataString(v.Valor)}"))}");

    private static T? Leer<T>(string cuerpo)
    {
        try
        {
            return string.IsNullOrWhiteSpace(cuerpo) ? default : JsonSerializer.Deserialize<T>(cuerpo, OpcionesLectura);
        }
        catch (JsonException)
        {
            return default;
        }
    }

    private static MultipartFormDataContent ContenidoXml(string xml, string nombreArchivo)
    {
        var contenido = new MultipartFormDataContent();
        contenido.Add(new StringContent(xml, Encoding.UTF8, "text/xml"), "xml", nombreArchivo);
        return contenido;
    }

    /// <summary>Valor de un elemento del XML (ej. el RNC del emisor, que va en el nombre del archivo); sin DTD ni entidades externas.</summary>
    private static string ValorXml(string xml, string elemento)
    {
        var configuracion = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
        using var lector = XmlReader.Create(new StringReader(xml), configuracion);
        while (lector.Read())
        {
            if (lector.NodeType == XmlNodeType.Element && lector.LocalName == elemento)
                return lector.ReadElementContentAsString().Trim();
        }

        return string.Empty;
    }

    private static string Texto(string cuerpo) => cuerpo.Length > 500 ? cuerpo[..500] : cuerpo;
}
