using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using CgPos.Contratos.Central;
using CgPos.Contratos.Serializacion;
using CgPos.Contratos.Sincronizacion;
using CgPos.Pos.Aplicacion.Sincronizacion;
using Microsoft.Extensions.Configuration;

namespace CgPos.Pos.Infraestructura.Sincronizacion;

/// <summary>Valores técnicos de la sincronización; cada instalación los ajusta en appsettings.</summary>
/// <param name="CarpetaXml">Carpeta base de los XML de e-CF (Pendientes y Enviados).</param>
internal sealed record OpcionesSincronizacion(TimeSpan Intervalo, int TamanoLote, TimeSpan EsperaInicial, TimeSpan EsperaMaxima, TimeSpan TiempoEspera, string CarpetaXml)
{
    public static OpcionesSincronizacion Leer(IConfiguration configuracion) => new(
        TimeSpan.FromSeconds(Math.Max(1, Entero(configuracion, ClavesSincronizacion.IntervaloSegundos, 30))),
        Math.Clamp(Entero(configuracion, ClavesSincronizacion.TamanoLote, 50), 1, 500),
        TimeSpan.FromSeconds(Math.Max(1, Entero(configuracion, ClavesSincronizacion.EsperaInicialSegundos, 30))),
        TimeSpan.FromSeconds(Math.Max(1, Entero(configuracion, ClavesSincronizacion.EsperaMaximaSegundos, 3600))),
        TimeSpan.FromSeconds(Math.Max(1, Entero(configuracion, ClavesSincronizacion.TiempoEsperaSegundos, 30))),
        configuracion[CgPos.Pos.Aplicacion.Ecf.ClavesEcf.CarpetaXml] is { Length: > 0 } carpeta ? carpeta : CgPos.Pos.Aplicacion.Ecf.ClavesEcf.CarpetaXmlPredeterminada);

    private static int Entero(IConfiguration configuracion, string clave, int predeterminado) =>
        int.TryParse(configuracion[clave], NumberStyles.Integer, CultureInfo.InvariantCulture, out var valor) ? valor : predeterminado;

    /// <summary>
    /// Espera antes del próximo intento: se duplica con cada fallo sin conexión hasta el máximo. Un rechazo explícito del Central espera el máximo:
    /// no se resuelve reintentando enseguida.
    /// </summary>
    public TimeSpan Espera(int intentos, bool rechazado)
    {
        if (rechazado)
            return EsperaMaxima;

        var segundos = EsperaInicial.TotalSeconds * Math.Pow(2, Math.Max(0, intentos - 1));
        return TimeSpan.FromSeconds(Math.Min(segundos, EsperaMaxima.TotalSeconds));
    }
}

internal static class FabricaClienteCentral
{
    public const string CarpetaSimuladaPredeterminada = @"C:\CGPOS\CentralSimulado";

    public static IClienteCentral Crear(IConfiguration configuracion)
    {
        if (string.Equals(configuracion[ClavesSincronizacion.ModoCentral], ClavesSincronizacion.ModoSimulado, StringComparison.OrdinalIgnoreCase))
            return new CentralSimulado(configuracion[ClavesSincronizacion.CarpetaSimulada] is { Length: > 0 } carpeta ? carpeta : CarpetaSimuladaPredeterminada);

        if (!Uri.TryCreate(configuracion[ClavesSincronizacion.UrlCentral], UriKind.Absolute, out var url))
            return new CentralNoConfigurado();

        var cajaId = Guid.TryParse(configuracion[ClavesSincronizacion.CajaId], out var id) ? id : Guid.Empty;
        return new ClienteCentralHttp(url, OpcionesSincronizacion.Leer(configuracion).TiempoEspera, cajaId, configuracion[ClavesSincronizacion.SecretoCaja]);
    }
}

internal sealed class CentralNoConfigurado : IClienteCentral
{
    public bool Configurado => false;

    public Task<ResultadoEnvioCentral> EnviarAsync(MensajeSincronizacion mensaje, CancellationToken cancelacion = default) =>
        Task.FromResult(ResultadoEnvioCentral.SinConexion("La caja no tiene un Central configurado."));
}

/// <summary>
/// Central simulado hasta la Etapa 2 (H2): guarda cada mensaje en una carpeta local aplicando las mismas reglas que tendrá el Central:
/// valida el hash del contenido y confirma sin duplicar un Id ya recibido con el mismo contenido (RN-25).
/// </summary>
internal sealed class CentralSimulado(string carpeta) : IClienteCentral
{
    public string Carpeta { get; } = carpeta;

    public bool Configurado => true;

    public async Task<ResultadoEnvioCentral> EnviarAsync(MensajeSincronizacion mensaje, CancellationToken cancelacion = default)
    {
        var respuesta = await RecibirAsync(mensaje, cancelacion);
        return respuesta.Estado is EstadoRecepcion.Recibido or EstadoRecepcion.Duplicado
            ? ResultadoEnvioCentral.Recibido()
            : ResultadoEnvioCentral.Rechazado(respuesta.Error ?? "El Central simulado rechazó el mensaje.");
    }

    public async Task<RespuestaRecepcionCentral> RecibirAsync(MensajeSincronizacion mensaje, CancellationToken cancelacion = default)
    {
        if (!string.Equals(MensajeSalida.CalcularHash(mensaje.Contenido), mensaje.HashContenido, StringComparison.OrdinalIgnoreCase))
            return new RespuestaRecepcionCentral(EstadoRecepcion.Rechazado, "El hash del contenido no coincide: el mensaje se alteró o llegó incompleto.");

        Directory.CreateDirectory(Carpeta);
        var ruta = Path.Combine(Carpeta, $"{mensaje.Id:N}.json");
        if (File.Exists(ruta))
        {
            var recibido = JsonSerializer.Deserialize<MensajeSincronizacion>(await File.ReadAllTextAsync(ruta, cancelacion), OpcionesJson.Predeterminadas);
            return string.Equals(recibido?.HashContenido, mensaje.HashContenido, StringComparison.OrdinalIgnoreCase)
                ? new RespuestaRecepcionCentral(EstadoRecepcion.Duplicado)
                : new RespuestaRecepcionCentral(EstadoRecepcion.Rechazado, "El Id del mensaje ya se recibió con otro contenido.");
        }

        await File.WriteAllTextAsync(ruta, JsonSerializer.Serialize(mensaje, OpcionesJson.Predeterminadas), new UTF8Encoding(false), cancelacion);
        return new RespuestaRecepcionCentral(EstadoRecepcion.Recibido);
    }
}

/// <summary>
/// Envío al Central por HTTPS (RF-275): la caja cambia su credencial de dispositivo por un token de pocos minutos, que guarda en memoria, y hace POST a
/// <see cref="RutaRecepcion"/> con la clave de idempotencia y el hash del contenido en los encabezados.
/// Errores de red, tiempo de espera o respuestas 5xx cuentan como sin conexión y se reintentan.
/// </summary>
internal sealed class ClienteCentralHttp : IClienteCentral
{
    public const string RutaRecepcion = "api/sincronizacion/mensajes";
    public const string RutaToken = "api/dispositivos/token";

    private static readonly SocketsHttpHandler Manejador = new() { PooledConnectionLifetime = TimeSpan.FromMinutes(5) };

    // El token se renueva un poco antes de vencer para no usarlo en el límite.
    private static readonly TimeSpan MargenRenovacion = TimeSpan.FromMinutes(1);

    private readonly HttpClient _http;
    private readonly Guid _cajaId;
    private readonly string? _secreto;
    private readonly TimeProvider _reloj;
    private readonly SemaphoreSlim _bloqueoToken = new(1, 1);
    private string? _token;
    private DateTimeOffset _tokenVence;

    public ClienteCentralHttp(Uri url, TimeSpan tiempoEspera, Guid cajaId, string? secreto)
        : this(new HttpClient(Manejador, disposeHandler: false) { BaseAddress = url, Timeout = tiempoEspera }, cajaId, secreto, TimeProvider.System)
    {
    }

    internal ClienteCentralHttp(HttpClient http, Guid cajaId, string? secreto, TimeProvider reloj)
    {
        _http = http;
        _cajaId = cajaId;
        _secreto = string.IsNullOrWhiteSpace(secreto) ? null : secreto.Trim();
        _reloj = reloj;
    }

    public bool Configurado => true;

    public async Task<ResultadoEnvioCentral> EnviarAsync(MensajeSincronizacion mensaje, CancellationToken cancelacion = default)
    {
        if (_cajaId == Guid.Empty || _secreto is null)
            return ResultadoEnvioCentral.SinConexion($"La caja no tiene credencial del Central ({ClavesSincronizacion.CajaId} y {ClavesSincronizacion.SecretoCaja}).");

        try
        {
            for (var intento = 1; ; intento++)
            {
                var (token, fallo) = await ObtenerTokenAsync(renovar: intento > 1, cancelacion);
                if (fallo is not null)
                    return fallo;

                using var solicitud = new HttpRequestMessage(HttpMethod.Post, RutaRecepcion) { Content = JsonContent.Create(mensaje, options: OpcionesJson.Predeterminadas) };
                solicitud.Headers.Add("Idempotency-Key", mensaje.Id.ToString());
                solicitud.Headers.Add("X-Contenido-Sha256", mensaje.HashContenido);
                solicitud.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

                using var respuesta = await _http.SendAsync(solicitud, cancelacion);

                // El token venció o se revocó desde que se obtuvo: se pide otro una sola vez.
                if (respuesta.StatusCode == HttpStatusCode.Unauthorized && intento == 1)
                    continue;

                return await InterpretarAsync(respuesta, cancelacion);
            }
        }
        catch (HttpRequestException excepcion)
        {
            return ResultadoEnvioCentral.SinConexion($"Sin comunicación con el Central: {excepcion.Message}");
        }
        catch (TaskCanceledException) when (!cancelacion.IsCancellationRequested)
        {
            return ResultadoEnvioCentral.SinConexion("Se agotó el tiempo de espera con el Central.");
        }
    }

    private async Task<(string? Token, ResultadoEnvioCentral? Fallo)> ObtenerTokenAsync(bool renovar, CancellationToken cancelacion)
    {
        await _bloqueoToken.WaitAsync(cancelacion);
        try
        {
            if (!renovar && _token is not null && _reloj.GetUtcNow() < _tokenVence - MargenRenovacion)
                return (_token, null);

            _token = null;
            using var respuesta = await _http.PostAsJsonAsync(RutaToken, new SolicitudTokenDispositivo(_cajaId, _secreto!), OpcionesJson.Predeterminadas, cancelacion);
            if ((int)respuesta.StatusCode >= 500)
                return (null, ResultadoEnvioCentral.SinConexion($"El Central respondió {(int)respuesta.StatusCode} al autenticar la caja."));

            var cuerpo = await LeerJsonAsync<RespuestaTokenDispositivo>(respuesta, cancelacion);
            if (!respuesta.IsSuccessStatusCode || cuerpo is not { Exitoso: true, Token: { Length: > 0 } token })
            {
                var motivo = cuerpo?.Mensaje ?? await respuesta.Content.ReadAsStringAsync(cancelacion);
                return (null, ResultadoEnvioCentral.Rechazado($"El Central no autenticó la caja: {(string.IsNullOrWhiteSpace(motivo) ? ((int)respuesta.StatusCode).ToString(CultureInfo.InvariantCulture) : motivo)}"));
            }

            _token = token;
            _tokenVence = cuerpo.ExpiraEn ?? _reloj.GetUtcNow() + MargenRenovacion;
            return (token, null);
        }
        finally
        {
            _bloqueoToken.Release();
        }
    }

    private static async Task<ResultadoEnvioCentral> InterpretarAsync(HttpResponseMessage respuesta, CancellationToken cancelacion)
    {
        if ((int)respuesta.StatusCode >= 500)
            return ResultadoEnvioCentral.SinConexion($"El Central respondió {(int)respuesta.StatusCode} ({respuesta.ReasonPhrase}).");

        var cuerpo = await LeerJsonAsync<RespuestaRecepcionCentral>(respuesta, cancelacion);
        if (respuesta.IsSuccessStatusCode || respuesta.StatusCode == HttpStatusCode.Conflict)
            return cuerpo is null or { Estado: EstadoRecepcion.Recibido or EstadoRecepcion.Duplicado }
                ? ResultadoEnvioCentral.Recibido()
                : ResultadoEnvioCentral.Rechazado(cuerpo.Error ?? "El Central rechazó el mensaje.");

        if (respuesta.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            return ResultadoEnvioCentral.Rechazado("El Central no aceptó la credencial de la caja.");

        return ResultadoEnvioCentral.Rechazado(cuerpo?.Error ?? $"El Central rechazó el mensaje ({(int)respuesta.StatusCode}).");
    }

    private static async Task<T?> LeerJsonAsync<T>(HttpResponseMessage respuesta, CancellationToken cancelacion) where T : class
    {
        if (respuesta.Content.Headers.ContentType?.MediaType != "application/json")
            return null;

        try
        {
            return await respuesta.Content.ReadFromJsonAsync<T>(OpcionesJson.Predeterminadas, cancelacion);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

internal sealed class EstadoConexionCentral : IEstadoConexionCentral
{
    private readonly Lock _bloqueo = new();

    public DateTimeOffset? UltimoContacto { get; private set; }
    public DateTimeOffset? UltimoFallo { get; private set; }
    public string? UltimoError { get; private set; }

    public void RegistrarContacto(DateTimeOffset momento)
    {
        lock (_bloqueo)
        {
            UltimoContacto = momento;
            UltimoError = null;
        }
    }

    public void RegistrarFallo(DateTimeOffset momento, string? error)
    {
        lock (_bloqueo)
        {
            UltimoFallo = momento;
            UltimoError = error;
        }
    }
}
