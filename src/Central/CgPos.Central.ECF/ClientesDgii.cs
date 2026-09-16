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

namespace CgPos.Central.ECF;

/// <param name="Cliente">"Simulado" (solo desarrollo) o "Http" (DGII real, el predeterminado).</param>
/// <param name="RutaCertificado">Certificado .p12 del emisor con el que se firma la semilla de autenticación.</param>
/// <param name="PinCertificado">PIN del certificado; va en la configuración segura del servidor, nunca en la base de datos.</param>
internal sealed record OpcionesDgii(string? Cliente, string? RutaCertificado, string? PinCertificado)
{
    public bool UsaSimulador => string.Equals(Cliente, "Simulado", StringComparison.OrdinalIgnoreCase);
}

/// <summary>DGII simulada para desarrollo: recibe todo y lo acepta en la primera consulta.</summary>
internal sealed class ClienteDgiiSimulado(ILogger<ClienteDgiiSimulado> registro) : IClienteDgii
{
    public Task<RespuestaDgii> EnviarAsync(ComprobanteParaDgii comprobante, CancellationToken cancelacion = default)
    {
        registro.LogInformation("DGII simulada: recibido el e-CF {Encf}", comprobante.Encf);
        return Task.FromResult(new RespuestaDgii(ResultadoRespuestaDgii.EnProceso, $"SIM-{Guid.CreateVersion7():N}"));
    }

    public Task<RespuestaDgii> ConsultarAsync(string trackId, CancellationToken cancelacion = default) =>
        Task.FromResult(new RespuestaDgii(ResultadoRespuestaDgii.Aceptado, trackId, "Aceptado por la DGII simulada."));
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
/// Servicios web de e-CF de la DGII: autenticación con la semilla firmada por el certificado del emisor, recepción del XML y consulta del
/// resultado por trackId. Rutas y formatos de la documentación técnica de la DGII; por confirmar durante la certificación (TesteCF).
/// </summary>
internal sealed class ClienteDgiiHttp(HttpClient http, SesionDgii sesion, IServiceScopeFactory fabricaAmbitos, TimeProvider reloj, ILogger<ClienteDgiiHttp> registro)
    : IClienteDgii
{
    private const string RutaSemilla = "autenticacion/api/Autenticacion/Semilla";
    private const string RutaValidarSemilla = "autenticacion/api/Autenticacion/ValidarSemilla";
    private const string RutaRecepcion = "recepcion/api/FacturasElectronicas";
    private const string RutaEstado = "consultaresultado/api/Consultas/Estado";

    private static readonly JsonSerializerOptions OpcionesLectura = new(JsonSerializerDefaults.Web);

    private sealed record RespuestaTokenDgii(string? Token, DateTimeOffset? Expira);

    private sealed record RespuestaRecepcionDgii(string? TrackId, string? Error, string? Mensaje);

    private sealed record MensajeDgii(string? Valor, int? Codigo);

    private sealed record RespuestaEstadoDgii(string? TrackId, string? Estado, IReadOnlyList<MensajeDgii>? Mensajes);

    public async Task<RespuestaDgii> EnviarAsync(ComprobanteParaDgii comprobante, CancellationToken cancelacion = default)
    {
        var nombre = $"{RncEmisor(comprobante.XmlFirmado)}{comprobante.Encf}.xml";
        using var respuesta = await EnviarAutenticadoAsync(
            urlBase => new HttpRequestMessage(HttpMethod.Post, new Uri(urlBase, RutaRecepcion)) { Content = ContenidoXml(comprobante.XmlFirmado, nombre) }, cancelacion);
        var cuerpo = await respuesta.Content.ReadAsStringAsync(cancelacion);

        if (respuesta.StatusCode == HttpStatusCode.BadRequest)
            return new RespuestaDgii(ResultadoRespuestaDgii.Rechazado, Mensaje: Texto(cuerpo));
        if (!respuesta.IsSuccessStatusCode)
            return RespuestaDgii.Fallo($"La DGII respondió {(int)respuesta.StatusCode} al recibir el e-CF: {Texto(cuerpo)}");

        var datos = JsonSerializer.Deserialize<RespuestaRecepcionDgii>(cuerpo, OpcionesLectura);
        return string.IsNullOrWhiteSpace(datos?.TrackId)
            ? RespuestaDgii.Fallo($"La DGII no devolvió el trackId: {datos?.Mensaje ?? datos?.Error ?? Texto(cuerpo)}")
            : new RespuestaDgii(ResultadoRespuestaDgii.EnProceso, datos.TrackId);
    }

    public async Task<RespuestaDgii> ConsultarAsync(string trackId, CancellationToken cancelacion = default)
    {
        using var respuesta = await EnviarAutenticadoAsync(
            urlBase => new HttpRequestMessage(HttpMethod.Get, new Uri(urlBase, $"{RutaEstado}?trackid={Uri.EscapeDataString(trackId)}")), cancelacion);
        var cuerpo = await respuesta.Content.ReadAsStringAsync(cancelacion);
        if (!respuesta.IsSuccessStatusCode)
            return RespuestaDgii.Fallo($"La DGII respondió {(int)respuesta.StatusCode} al consultar el resultado: {Texto(cuerpo)}");

        var datos = JsonSerializer.Deserialize<RespuestaEstadoDgii>(cuerpo, OpcionesLectura);
        var mensajes = string.Join(" ", (datos?.Mensajes ?? []).Select(m => m.Valor).Where(v => !string.IsNullOrWhiteSpace(v)));
        var resultado = MaestroCentral.NormalizarBusqueda(datos?.Estado)?.Replace(" ", string.Empty) switch
        {
            "aceptado" => ResultadoRespuestaDgii.Aceptado,
            "aceptadocondicional" => ResultadoRespuestaDgii.AceptadoCondicional,
            "rechazado" => ResultadoRespuestaDgii.Rechazado,
            _ => ResultadoRespuestaDgii.EnProceso,
        };
        return new RespuestaDgii(resultado, trackId, mensajes.Length == 0 ? null : mensajes);
    }

    private async Task<HttpResponseMessage> EnviarAutenticadoAsync(Func<Uri, HttpRequestMessage> crear, CancellationToken cancelacion)
    {
        var urlBase = await UrlBaseAsync(cancelacion);
        for (var intento = 1; ; intento++)
        {
            using var solicitud = crear(urlBase);
            solicitud.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await TokenAsync(urlBase, cancelacion));
            var respuesta = await http.SendAsync(solicitud, cancelacion);
            if (respuesta.StatusCode != HttpStatusCode.Unauthorized || intento > 1)
                return respuesta;

            // Token vencido o revocado antes de lo informado: se pide uno nuevo una sola vez.
            respuesta.Dispose();
            sesion.Token = null;
        }
    }

    private async Task<string> TokenAsync(Uri urlBase, CancellationToken cancelacion)
    {
        if (sesion.Token is { } vigente && sesion.VenceEn > reloj.GetUtcNow().AddMinutes(1))
            return vigente;

        await sesion.Cerrojo.WaitAsync(cancelacion);
        try
        {
            if (sesion.Token is { } obtenido && sesion.VenceEn > reloj.GetUtcNow().AddMinutes(1))
                return obtenido;

            var semilla = await http.GetStringAsync(new Uri(urlBase, RutaSemilla), cancelacion);
            var firmada = new FirmadorEcf().Firmar(semilla, sesion.Certificado);
            using var contenido = ContenidoXml(firmada, "semilla.xml");
            using var respuesta = await http.PostAsync(new Uri(urlBase, RutaValidarSemilla), contenido, cancelacion);
            var cuerpo = await respuesta.Content.ReadAsStringAsync(cancelacion);
            if (!respuesta.IsSuccessStatusCode)
                throw new InvalidOperationException($"La DGII rechazó la autenticación ({(int)respuesta.StatusCode}): {Texto(cuerpo)}");

            var datos = JsonSerializer.Deserialize<RespuestaTokenDgii>(cuerpo, OpcionesLectura);
            if (string.IsNullOrWhiteSpace(datos?.Token))
                throw new InvalidOperationException("La DGII no devolvió el token de autenticación.");

            sesion.Token = datos.Token;
            sesion.VenceEn = datos.Expira ?? reloj.GetUtcNow().AddMinutes(1);
            registro.LogInformation("Autenticado en la DGII hasta {Vence}", sesion.VenceEn);
            return datos.Token;
        }
        finally
        {
            sesion.Cerrojo.Release();
        }
    }

    private async Task<Uri> UrlBaseAsync(CancellationToken cancelacion)
    {
        await using var ambito = fabricaAmbitos.CreateAsyncScope();
        var texto = await ambito.ServiceProvider.GetRequiredService<IParametrosCentral>().ObtenerRequeridoAsync(ClavesParametrosCentral.DgiiUrlBase, cancelacion);
        return Uri.TryCreate(texto.EndsWith('/') ? texto : texto + "/", UriKind.Absolute, out var url) && url.Scheme == Uri.UriSchemeHttps
            ? url
            : throw new ParametroNoConfiguradoExcepcion(ClavesParametrosCentral.DgiiUrlBase, "debe ser una dirección https");
    }

    private static MultipartFormDataContent ContenidoXml(string xml, string nombreArchivo)
    {
        var contenido = new MultipartFormDataContent();
        contenido.Add(new StringContent(xml, Encoding.UTF8, "text/xml"), "xml", nombreArchivo);
        return contenido;
    }

    /// <summary>RNC del emisor dentro del XML (el nombre del archivo lo lleva); sin DTD ni entidades externas.</summary>
    private static string RncEmisor(string xml)
    {
        var configuracion = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
        using var lector = XmlReader.Create(new StringReader(xml), configuracion);
        while (lector.Read())
        {
            if (lector.NodeType == XmlNodeType.Element && lector.LocalName == "RNCEmisor")
                return lector.ReadElementContentAsString().Trim();
        }

        return string.Empty;
    }

    private static string Texto(string cuerpo) => cuerpo.Length > 500 ? cuerpo[..500] : cuerpo;
}
