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
using CgPos.Dominio.Comun;

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

    public static IClienteCentral Crear(IConfiguration configuracion, ICredencialCaja credencial)
    {
        if (string.Equals(configuracion[ClavesSincronizacion.ModoCentral], ClavesSincronizacion.ModoSimulado, StringComparison.OrdinalIgnoreCase))
            return new CentralSimulado(configuracion[ClavesSincronizacion.CarpetaSimulada] is { Length: > 0 } carpeta ? carpeta : CarpetaSimuladaPredeterminada);

        if (!Uri.TryCreate(configuracion[ClavesSincronizacion.UrlCentral], UriKind.Absolute, out var url))
            return new CentralNoConfigurado();

        // Los códigos van con sus dos dígitos: así los espera el Central.
        var sucursal = CodigoConfigurado.Codigo(configuracion[ClavesSincronizacion.CajaSucursal]);
        var caja = CodigoConfigurado.Codigo(configuracion[ClavesSincronizacion.CajaCodigo]);
        return new ClienteCentralHttp(url, OpcionesSincronizacion.Leer(configuracion).TiempoEspera, sucursal, caja, credencial);
    }
}

internal static class CodigoConfigurado
{
    public static string Codigo(string? valor) =>
        int.TryParse(valor, out var numero) && numero is >= 1 and <= CgPos.Dominio.Comun.CodigosCatalogo.MaximoSucursalCaja
            ? numero.ToString("00", System.Globalization.CultureInfo.InvariantCulture)
            : string.Empty;
}

internal sealed class CentralNoConfigurado : IClienteCentral
{
    private const string Motivo = "La caja no tiene un Central configurado.";

    public bool Configurado => false;

    public Task<EstadoCredencialCaja> AsegurarCredencialAsync(CancellationToken cancelacion = default) =>
        Task.FromResult(new EstadoCredencialCaja(false, Motivo));

    public Task<ResultadoEnvioCentral> EnviarAsync(MensajeSincronizacion mensaje, CancellationToken cancelacion = default) =>
        Task.FromResult(ResultadoEnvioCentral.SinConexion(Motivo));

    public Task<ResultadoBajadaCentral> DescargarMaestrosAsync(long desde, CancellationToken cancelacion = default) =>
        Task.FromResult(ResultadoBajadaCentral.SinConexion(Motivo));

    public Task<ResultadoNotaCreditoCentral> ConsultarNotaCreditoAsync(string codigo, CancellationToken cancelacion = default) =>
        Task.FromResult(ResultadoNotaCreditoCentral.SinConexion(Motivo));

    public Task<ResultadoListaBodaCentral> ConsultarListaBodaAsync(string numero, CancellationToken cancelacion = default) =>
        Task.FromResult(ResultadoListaBodaCentral.SinConexion(Motivo));

    public Task<ResultadoReservaNotaCredito> ReservarNotaCreditoAsync(string notaCreditoNumero, string ventaNumero, decimal monto,
        CancellationToken cancelacion = default) =>
        Task.FromResult(ResultadoReservaNotaCredito.SinConexion(Motivo));

    public Task LiberarReservaNotaCreditoAsync(string notaCreditoNumero, string ventaNumero, CancellationToken cancelacion = default) => Task.CompletedTask;
}

/// <summary>
/// Central simulado sin servidor: guarda cada mensaje en una carpeta local aplicando las mismas reglas que el Central (hash del contenido y un Id
/// recibido una sola vez, RN-25). No publica maestros: la caja se carga desde archivos.
/// </summary>
internal sealed class CentralSimulado(string carpeta) : IClienteCentral
{
    private const string SinNotasCredito = "El Central simulado no valida notas de crédito de otras sucursales.";

    public string Carpeta { get; } = carpeta;

    public bool Configurado => true;

    public Task<EstadoCredencialCaja> AsegurarCredencialAsync(CancellationToken cancelacion = default) =>
        Task.FromResult(new EstadoCredencialCaja(true));

    public async Task<ResultadoEnvioCentral> EnviarAsync(MensajeSincronizacion mensaje, CancellationToken cancelacion = default)
    {
        var respuesta = await RecibirAsync(mensaje, cancelacion);
        return respuesta.Estado is EstadoRecepcion.Recibido or EstadoRecepcion.Duplicado
            ? ResultadoEnvioCentral.Recibido()
            : ResultadoEnvioCentral.Rechazado(respuesta.Error ?? "El Central simulado rechazó el mensaje.");
    }

    public Task<ResultadoBajadaCentral> DescargarMaestrosAsync(long desde, CancellationToken cancelacion = default) =>
        Task.FromResult(ResultadoBajadaCentral.Recibido(new PaqueteBajadaMaestros(desde, desde, null, null)));

    public Task<ResultadoNotaCreditoCentral> ConsultarNotaCreditoAsync(string codigo, CancellationToken cancelacion = default) =>
        Task.FromResult(ResultadoNotaCreditoCentral.SinConexion(SinNotasCredito));

    public Task<ResultadoListaBodaCentral> ConsultarListaBodaAsync(string numero, CancellationToken cancelacion = default) =>
        Task.FromResult(ResultadoListaBodaCentral.SinConexion("El Central simulado no tiene listas de boda."));

    public Task<ResultadoReservaNotaCredito> ReservarNotaCreditoAsync(string notaCreditoNumero, string ventaNumero, decimal monto,
        CancellationToken cancelacion = default) =>
        Task.FromResult(ResultadoReservaNotaCredito.SinConexion(SinNotasCredito));

    public Task LiberarReservaNotaCreditoAsync(string notaCreditoNumero, string ventaNumero, CancellationToken cancelacion = default) => Task.CompletedTask;

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
/// Comunicación con el Central por HTTPS (RF-275): la caja cambia su credencial de dispositivo por un token de pocos minutos, que guarda en memoria.
/// Sube la bandeja de salida a <see cref="RutaRecepcion"/> con la clave de idempotencia y el hash del contenido, y baja los maestros de
/// <see cref="RutaMaestros"/>. Errores de red, tiempo de espera o respuestas 5xx cuentan como sin conexión y se reintentan.
/// </summary>
internal sealed class ClienteCentralHttp : IClienteCentral
{
    public const string RutaRecepcion = "api/sincronizacion/mensajes";
    public const string RutaMaestros = "api/sincronizacion/maestros";
    public const string RutaToken = "api/dispositivos/token";
    public const string RutaEnrolamiento = "api/dispositivos/solicitud";
    public const string RutaNotasCredito = "api/notas-credito";
    public const string RutaListasBoda = "api/listas-boda";

    private static readonly SocketsHttpHandler Manejador = new()
    {
        PooledConnectionLifetime = TimeSpan.FromMinutes(5),
        AutomaticDecompression = DecompressionMethods.All,
    };

    // El token se renueva un poco antes de vencer para no usarlo en el límite.
    private static readonly TimeSpan MargenRenovacion = TimeSpan.FromMinutes(1);

    private readonly HttpClient _http;
    private readonly string _sucursalCodigo;
    private readonly string _cajaCodigo;
    private readonly ICredencialCaja _credencial;
    private readonly TimeProvider _reloj;
    private readonly SemaphoreSlim _bloqueoToken = new(1, 1);
    private string? _token;
    private DateTimeOffset _tokenVence;

    public ClienteCentralHttp(Uri url, TimeSpan tiempoEspera, string sucursalCodigo, string cajaCodigo, ICredencialCaja credencial)
        : this(new HttpClient(Manejador, disposeHandler: false) { BaseAddress = url, Timeout = tiempoEspera }, sucursalCodigo, cajaCodigo, credencial,
            TimeProvider.System)
    {
    }

    internal ClienteCentralHttp(HttpClient http, string sucursalCodigo, string cajaCodigo, ICredencialCaja credencial, TimeProvider reloj)
    {
        _http = http;
        _sucursalCodigo = sucursalCodigo;
        _cajaCodigo = cajaCodigo;
        _credencial = credencial;
        _reloj = reloj;
    }

    public bool Configurado => true;

    public async Task<EstadoCredencialCaja> AsegurarCredencialAsync(CancellationToken cancelacion = default)
    {
        if (_credencial.Secreto is { Length: > 0 })
            return new EstadoCredencialCaja(true);

        var solicitud = new SolicitudEnrolamientoCaja(_sucursalCodigo, _cajaCodigo, _credencial.HuellaEquipo, _credencial.NombreEquipo,
            _credencial.TokenSolicitud);

        HttpResponseMessage respuesta;
        try
        {
            respuesta = await _http.PostAsJsonAsync(RutaEnrolamiento, solicitud, OpcionesJson.Predeterminadas, cancelacion);
        }
        catch (Exception excepcion) when (excepcion is HttpRequestException or TaskCanceledException && !cancelacion.IsCancellationRequested)
        {
            return new EstadoCredencialCaja(false, $"No hay comunicación con el Central para pedir la credencial: {excepcion.Message}");
        }

        using (respuesta)
        {
            var cuerpo = await LeerJsonAsync<RespuestaEnrolamientoCaja>(respuesta, cancelacion);
            if (cuerpo is { Estado: EstadoEnrolamientoCaja.Entregada, Secreto: { Length: > 0 } secreto })
            {
                _credencial.Guardar(secreto);
                return new EstadoCredencialCaja(true, "El Central aceptó esta caja y le entregó su credencial.");
            }

            var mensaje = cuerpo?.Mensaje ?? await respuesta.Content.ReadAsStringAsync(cancelacion);
            return new EstadoCredencialCaja(false, string.IsNullOrWhiteSpace(mensaje)
                ? $"El Central respondió {(int)respuesta.StatusCode} a la solicitud de la caja."
                : mensaje);
        }
    }

    public async Task<ResultadoEnvioCentral> EnviarAsync(MensajeSincronizacion mensaje, CancellationToken cancelacion = default)
    {
        var (respuesta, fallo) = await SolicitarAsync(() =>
        {
            var solicitud = new HttpRequestMessage(HttpMethod.Post, RutaRecepcion) { Content = JsonContent.Create(mensaje, options: OpcionesJson.Predeterminadas) };
            solicitud.Headers.Add("Idempotency-Key", mensaje.Id.ToString());
            solicitud.Headers.Add("X-Contenido-Sha256", mensaje.HashContenido);
            return solicitud;
        }, cancelacion);

        if (fallo is not null)
            return fallo;

        // Sin fallo, SolicitarAsync siempre devuelve la respuesta.
        ArgumentNullException.ThrowIfNull(respuesta);
        using (respuesta)
        {
            try
            {
                return await InterpretarRecepcionAsync(respuesta, cancelacion);
            }
            catch (Exception excepcion) when (EsFallaDeComunicacion(excepcion, cancelacion))
            {
                return SinConexionPor(excepcion);
            }
        }
    }

    public async Task<ResultadoBajadaCentral> DescargarMaestrosAsync(long desde, CancellationToken cancelacion = default)
    {
        var (respuesta, fallo) = await SolicitarAsync(
            () => new HttpRequestMessage(HttpMethod.Get, $"{RutaMaestros}?desde={desde.ToString(CultureInfo.InvariantCulture)}"), cancelacion);

        if (fallo is not null)
            return new ResultadoBajadaCentral(null, fallo.CentralRespondio, fallo.Error);

        // Sin fallo, SolicitarAsync siempre devuelve la respuesta.
        ArgumentNullException.ThrowIfNull(respuesta);
        using (respuesta)
        {
            try
            {
                if ((int)respuesta.StatusCode >= 500)
                    return ResultadoBajadaCentral.SinConexion($"El Central respondió {(int)respuesta.StatusCode} ({respuesta.ReasonPhrase}).");

                if (!respuesta.IsSuccessStatusCode)
                    return ResultadoBajadaCentral.Rechazado(
                        $"El Central rechazó la descarga de maestros ({(int)respuesta.StatusCode}): {await respuesta.Content.ReadAsStringAsync(cancelacion)}");

                var paquete = await respuesta.Content.ReadFromJsonAsync<PaqueteBajadaMaestros>(OpcionesJson.Predeterminadas, cancelacion);
                return paquete is null ? ResultadoBajadaCentral.Rechazado("El Central respondió la descarga sin datos.") : ResultadoBajadaCentral.Recibido(paquete);
            }
            catch (JsonException excepcion)
            {
                return ResultadoBajadaCentral.Rechazado($"La descarga de maestros del Central no es válida: {excepcion.Message}");
            }
            catch (Exception excepcion) when (EsFallaDeComunicacion(excepcion, cancelacion))
            {
                return ResultadoBajadaCentral.SinConexion(SinConexionPor(excepcion).Error!);
            }
        }
    }

    /// <summary>Envía la solicitud con el token de la caja; si el Central no lo acepta (venció o se revocó) pide otro una sola vez.</summary>
    /// <summary>Retiene saldo de la nota mientras esta caja cobra; el Central la libera sola si no se confirma.</summary>
    public async Task<ResultadoReservaNotaCredito> ReservarNotaCreditoAsync(string notaCreditoNumero, string ventaNumero, decimal monto,
        CancellationToken cancelacion = default)
    {
        var (respuesta, fallo) = await SolicitarAsync(
            () => new HttpRequestMessage(HttpMethod.Post, $"{RutaNotasCredito}/{Uri.EscapeDataString(notaCreditoNumero)}/reservas")
            {
                Content = JsonContent.Create(new SolicitudReservaNotaCredito(ventaNumero, monto), options: OpcionesJson.Predeterminadas),
            }, cancelacion);

        if (fallo is not null)
            return ResultadoReservaNotaCredito.SinConexion(fallo.Error!);

        ArgumentNullException.ThrowIfNull(respuesta);
        using (respuesta)
        {
            try
            {
                if (!respuesta.IsSuccessStatusCode)
                    return ResultadoReservaNotaCredito.SinConexion($"El Central respondió {(int)respuesta.StatusCode} al reservar la nota de crédito.");

                var reserva = await respuesta.Content.ReadFromJsonAsync<RespuestaReservaNotaCredito>(OpcionesJson.Predeterminadas, cancelacion);
                return reserva is { Exitosa: true }
                    ? ResultadoReservaNotaCredito.Reservada(reserva.Monto)
                    : ResultadoReservaNotaCredito.Rechazada(reserva?.Mensaje ?? "El Central no reservó el saldo de la nota de crédito.");
            }
            catch (Exception excepcion) when (EsFallaDeComunicacion(excepcion, cancelacion))
            {
                return ResultadoReservaNotaCredito.SinConexion(SinConexionPor(excepcion).Error!);
            }
        }
    }

    public async Task LiberarReservaNotaCreditoAsync(string notaCreditoNumero, string ventaNumero, CancellationToken cancelacion = default)
    {
        var (respuesta, _) = await SolicitarAsync(() => new HttpRequestMessage(HttpMethod.Delete,
            $"{RutaNotasCredito}/{Uri.EscapeDataString(notaCreditoNumero)}/reservas/{Uri.EscapeDataString(ventaNumero)}"), cancelacion);
        respuesta?.Dispose();
    }

    /// <summary>Lista de boda del Central por su número (RF-73); la caja necesita conexión para consultarla.</summary>
    public async Task<ResultadoListaBodaCentral> ConsultarListaBodaAsync(string numero, CancellationToken cancelacion = default)
    {
        var (respuesta, fallo) = await SolicitarAsync(
            () => new HttpRequestMessage(HttpMethod.Get, $"{RutaListasBoda}/{Uri.EscapeDataString(numero)}"), cancelacion);

        if (fallo is not null)
            return ResultadoListaBodaCentral.SinConexion(fallo.Error!);

        ArgumentNullException.ThrowIfNull(respuesta);
        using (respuesta)
        {
            try
            {
                if (respuesta.StatusCode == HttpStatusCode.NotFound)
                    return ResultadoListaBodaCentral.NoExiste($"El Central no tiene la lista {numero}.");
                if (!respuesta.IsSuccessStatusCode)
                    return ResultadoListaBodaCentral.SinConexion($"El Central respondió {(int)respuesta.StatusCode} al consultar la lista de boda.");

                var lista = await respuesta.Content.ReadFromJsonAsync<DatosListaBodaParaCaja>(OpcionesJson.Predeterminadas, cancelacion);
                return lista is null
                    ? ResultadoListaBodaCentral.SinConexion("El Central devolvió una respuesta vacía.")
                    : ResultadoListaBodaCentral.Encontrada(lista);
            }
            catch (JsonException excepcion)
            {
                return ResultadoListaBodaCentral.SinConexion($"El Central devolvió una lista de boda ilegible: {excepcion.Message}");
            }
        }
    }

    /// <summary>Saldo de una nota de crédito emitida en cualquier sucursal (RF-43).</summary>
    public async Task<ResultadoNotaCreditoCentral> ConsultarNotaCreditoAsync(string codigo, CancellationToken cancelacion = default)
    {
        var (respuesta, fallo) = await SolicitarAsync(
            () => new HttpRequestMessage(HttpMethod.Get, $"{RutaNotasCredito}/{Uri.EscapeDataString(codigo)}"), cancelacion);

        if (fallo is not null)
            return ResultadoNotaCreditoCentral.SinConexion(fallo.Error!);

        ArgumentNullException.ThrowIfNull(respuesta);
        using (respuesta)
        {
            try
            {
                if (respuesta.StatusCode == HttpStatusCode.NotFound)
                    return ResultadoNotaCreditoCentral.NoExiste($"El Central no tiene la nota de crédito {codigo}.");
                if (!respuesta.IsSuccessStatusCode)
                    return ResultadoNotaCreditoCentral.SinConexion($"El Central respondió {(int)respuesta.StatusCode} al consultar la nota de crédito.");

                var nota = await respuesta.Content.ReadFromJsonAsync<DatosNotaCreditoParaCaja>(OpcionesJson.Predeterminadas, cancelacion);
                return nota is null
                    ? ResultadoNotaCreditoCentral.SinConexion("El Central devolvió una respuesta vacía.")
                    : ResultadoNotaCreditoCentral.Encontrada(nota);
            }
            catch (Exception excepcion) when (EsFallaDeComunicacion(excepcion, cancelacion))
            {
                return ResultadoNotaCreditoCentral.SinConexion(SinConexionPor(excepcion).Error!);
            }
        }
    }


    private async Task<(HttpResponseMessage? Respuesta, ResultadoEnvioCentral? Fallo)> SolicitarAsync(Func<HttpRequestMessage> crearSolicitud, CancellationToken cancelacion)
    {
        if (_sucursalCodigo.Length == 0 || _cajaCodigo.Length == 0)
            return (null, ResultadoEnvioCentral.SinConexion(
                $"La caja no sabe qué caja es ({ClavesSincronizacion.CajaSucursal} y {ClavesSincronizacion.CajaCodigo})."));
        if (_credencial.Secreto is null)
            return (null, ResultadoEnvioCentral.SinConexion("La caja todavía no tiene credencial: acepte su solicitud en el Central."));

        try
        {
            for (var intento = 1; ; intento++)
            {
                var (token, fallo) = await ObtenerTokenAsync(renovar: intento > 1, cancelacion);
                if (fallo is not null)
                    return (null, fallo);

                HttpResponseMessage respuesta;
                using (var solicitud = crearSolicitud())
                {
                    solicitud.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                    respuesta = await _http.SendAsync(solicitud, cancelacion);
                }

                if (respuesta.StatusCode == HttpStatusCode.Unauthorized && intento == 1)
                {
                    respuesta.Dispose();
                    continue;
                }

                return (respuesta, null);
            }
        }
        catch (Exception excepcion) when (EsFallaDeComunicacion(excepcion, cancelacion))
        {
            return (null, SinConexionPor(excepcion));
        }
    }

    private async Task<(string? Token, ResultadoEnvioCentral? Fallo)> ObtenerTokenAsync(bool renovar, CancellationToken cancelacion)
    {
        await _bloqueoToken.WaitAsync(cancelacion);
        try
        {
            if (!renovar && _token is not null && _reloj.Ahora() < _tokenVence - MargenRenovacion)
                return (_token, null);

            _token = null;
            var credencial = new SolicitudTokenDispositivo(_sucursalCodigo, _cajaCodigo, _credencial.Secreto ?? string.Empty, _credencial.HuellaEquipo);
            using var respuesta = await _http.PostAsJsonAsync(RutaToken, credencial, OpcionesJson.Predeterminadas, cancelacion);
            if ((int)respuesta.StatusCode >= 500)
                return (null, ResultadoEnvioCentral.SinConexion($"El Central respondió {(int)respuesta.StatusCode} al autenticar la caja."));

            var cuerpo = await LeerJsonAsync<RespuestaTokenDispositivo>(respuesta, cancelacion);
            if (!respuesta.IsSuccessStatusCode || cuerpo is not { Exitoso: true, Token: { Length: > 0 } token })
            {
                var motivo = cuerpo?.Mensaje ?? await respuesta.Content.ReadAsStringAsync(cancelacion);

                // El Central no reconoce esta credencial: se descarta para que la caja vuelva a pedir una. Si el equipo
                // cambió, aparecerá como una solicitud nueva y bastará con aceptarla.
                if (respuesta.StatusCode == HttpStatusCode.Unauthorized)
                    _credencial.Olvidar();

                return (null, ResultadoEnvioCentral.Rechazado(
                    $"El Central no autenticó la caja: {(string.IsNullOrWhiteSpace(motivo) ? ((int)respuesta.StatusCode).ToString(CultureInfo.InvariantCulture) : motivo)}"));
            }

            _token = token;
            _tokenVence = cuerpo.ExpiraEn ?? _reloj.Ahora() + MargenRenovacion;
            return (token, null);
        }
        finally
        {
            _bloqueoToken.Release();
        }
    }

    private static async Task<ResultadoEnvioCentral> InterpretarRecepcionAsync(HttpResponseMessage respuesta, CancellationToken cancelacion)
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

    private static bool EsFallaDeComunicacion(Exception excepcion, CancellationToken cancelacion) =>
        excepcion is HttpRequestException || (excepcion is TaskCanceledException && !cancelacion.IsCancellationRequested);

    private static ResultadoEnvioCentral SinConexionPor(Exception excepcion) =>
        excepcion is TaskCanceledException
            ? ResultadoEnvioCentral.SinConexion("Se agotó el tiempo de espera con el Central.")
            : ResultadoEnvioCentral.SinConexion($"Sin comunicación con el Central: {excepcion.Message}");

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
