using System.Net;
using System.Text;
using CgPos.Contratos.Central;
using CgPos.Contratos.Sincronizacion;
using CgPos.Pos.Aplicacion.Sincronizacion;
using CgPos.Pos.Infraestructura.Sincronizacion;
using Microsoft.Extensions.Configuration;

namespace CgPos.Pos.Pruebas.Sincronizacion;

/// <summary>Central simulado, cliente HTTP y espera progresiva de la sincronización, sin base de datos.</summary>
public class CentralPruebas : IDisposable
{
    private readonly string _carpeta = Path.Combine(Path.GetTempPath(), "CgPosPruebas", "central-" + Guid.NewGuid().ToString("N"));

    private static MensajeSincronizacion Mensaje(string contenido, Guid? id = null) =>
        new(id ?? Guid.CreateVersion7(), "Venta.Cobrada", "010110000001", contenido, MensajeSalida.CalcularHash(contenido), "01", "01",
            DateTimeOffset.UtcNow);

    [Fact]
    public async Task Central_simulado_recibe_confirma_duplicados_y_rechaza_hash_o_contenido_distinto()
    {
        var central = new CentralSimulado(_carpeta);
        var mensaje = Mensaje("""{"total":850}""");

        Assert.Equal(EstadoRecepcion.Recibido, (await central.RecibirAsync(mensaje)).Estado);
        Assert.Equal(EstadoRecepcion.Duplicado, (await central.RecibirAsync(mensaje)).Estado);
        Assert.True((await central.EnviarAsync(mensaje)).Confirmado);
        Assert.Single(Directory.GetFiles(_carpeta));

        var otroContenido = Mensaje("""{"total":900}""", mensaje.Id);
        Assert.Equal(EstadoRecepcion.Rechazado, (await central.RecibirAsync(otroContenido)).Estado);

        var alterado = mensaje with { Id = Guid.CreateVersion7(), Contenido = """{"total":1}""" };
        var rechazo = await central.EnviarAsync(alterado);
        Assert.False(rechazo.Confirmado);
        Assert.True(rechazo.CentralRespondio);
        Assert.Contains("hash", rechazo.Error);
    }

    [Fact]
    public async Task Cliente_http_se_autentica_envia_la_clave_de_idempotencia_y_distingue_rechazo_de_falta_de_conexion()
    {
        var mensaje = Mensaje("""{"total":850}""");
        HttpRequestMessage? recibida = null;
        var tokensPedidos = 0;

        ClienteCentralHttp Cliente(Func<HttpResponseMessage> respuesta, string? secreto = "secreto-caja") =>
            new(new HttpClient(new ManejadorPrueba(solicitud =>
            {
                if (solicitud.RequestUri!.AbsolutePath.EndsWith(ClienteCentralHttp.RutaToken, StringComparison.Ordinal))
                {
                    tokensPedidos++;
                    return Json(HttpStatusCode.OK, $$"""{"exitoso":true,"token":"token-{{tokensPedidos}}","expiraEn":"{{DateTimeOffset.UtcNow.AddMinutes(30):O}}"}""");
                }

                recibida = solicitud;
                return respuesta();
            })), ConfiguracionDePrueba(secreto), TimeProvider.System);

        var cliente = Cliente(() => Json(HttpStatusCode.OK, """{"estado":"Duplicado"}"""));
        Assert.True((await cliente.EnviarAsync(mensaje)).Confirmado);
        Assert.Equal(mensaje.Id.ToString(), recibida!.Headers.GetValues("Idempotency-Key").Single());
        Assert.Equal(mensaje.HashContenido, recibida.Headers.GetValues("X-Contenido-Sha256").Single());
        Assert.Equal("Bearer token-1", recibida.Headers.Authorization!.ToString());
        Assert.EndsWith(ClienteCentralHttp.RutaRecepcion, recibida.RequestUri!.AbsolutePath);

        // El token vigente se reutiliza en el siguiente envío.
        Assert.True((await cliente.EnviarAsync(mensaje)).Confirmado);
        Assert.Equal(1, tokensPedidos);

        var rechazado = await Cliente(() => Json(HttpStatusCode.UnprocessableEntity, """{"estado":"Rechazado","error":"Caja no registrada"}""")).EnviarAsync(mensaje);
        Assert.Equal((false, true, "Caja no registrada"), (rechazado.Confirmado, rechazado.CentralRespondio, rechazado.Error));

        // Un 401 con el token guardado pide otro una vez; si sigue sin aceptarse es un rechazo.
        var antes = tokensPedidos;
        var noAutorizado = await Cliente(() => new HttpResponseMessage(HttpStatusCode.Unauthorized)).EnviarAsync(mensaje);
        Assert.Equal((false, true), (noAutorizado.Confirmado, noAutorizado.CentralRespondio));
        Assert.Equal(antes + 2, tokensPedidos);

        var caido = await Cliente(() => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)).EnviarAsync(mensaje);
        Assert.Equal((false, false), (caido.Confirmado, caido.CentralRespondio));

        var sinConfigurar = await Cliente(() => Json(HttpStatusCode.OK, """{"estado":"Recibido"}"""), secreto: null).EnviarAsync(mensaje);
        Assert.Equal((false, false), (sinConfigurar.Confirmado, sinConfigurar.CentralRespondio));
        Assert.Contains("no está configurada", sinConfigurar.Error);

        var credencialRechazada = await new ClienteCentralHttp(new HttpClient(new ManejadorPrueba(_ =>
                Json(HttpStatusCode.Unauthorized, """{"exitoso":false,"mensaje":"Credencial de caja no válida."}""")))
            , ConfiguracionDePrueba("otro"), TimeProvider.System).EnviarAsync(mensaje);
        Assert.Equal((false, true), (credencialRechazada.Confirmado, credencialRechazada.CentralRespondio));
        Assert.Contains("Credencial de caja no válida.", credencialRechazada.Error);

        var sinRed = await new ClienteCentralHttp(new HttpClient(new ManejadorPrueba(_ => throw new HttpRequestException("sin red")))
            , ConfiguracionDePrueba("secreto"), TimeProvider.System).EnviarAsync(mensaje);
        Assert.False(sinRed.CentralRespondio);
    }

    [Fact]
    public void Espera_se_duplica_sin_conexion_hasta_el_maximo_y_un_rechazo_espera_el_maximo()
    {
        var opciones = OpcionesSincronizacion.Leer(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            [ClavesSincronizacion.EsperaInicialSegundos] = "30",
            [ClavesSincronizacion.EsperaMaximaSegundos] = "300",
        }).Build());

        var esperas = new OpcionesEspera(opciones.EsperaInicial, opciones.EsperaMaxima);
        Assert.Equal(TimeSpan.FromSeconds(30), esperas.Para(1, rechazado: false));
        Assert.Equal(TimeSpan.FromSeconds(120), esperas.Para(3, rechazado: false));
        Assert.Equal(TimeSpan.FromSeconds(300), esperas.Para(10, rechazado: false));
        Assert.Equal(TimeSpan.FromSeconds(300), esperas.Para(1, rechazado: true));
    }

    [Fact]
    public void La_fabrica_elige_simulado_http_o_sin_central_segun_la_configuracion()
    {
        IClienteCentral Crear(params (string Clave, string Valor)[] valores) =>
            FabricaClienteCentral.Crear(new ConfigurationBuilder().AddInMemoryCollection(valores.ToDictionary(v => v.Clave, v => (string?)v.Valor)).Build(),
                ConfiguracionDePrueba("secreto-caja"));

        // Solo el modo simulado cambia de cliente: la dirección del Central vive en la configuración de la caja, no aquí.
        Assert.IsType<CentralSimulado>(Crear((ClavesSincronizacion.ModoCentral, "Simulado"), (ClavesSincronizacion.CarpetaSimulada, _carpeta)));
        Assert.IsType<ClienteCentralHttp>(Crear());
    }

    public void Dispose()
    {
        if (Directory.Exists(_carpeta))
            Directory.Delete(_carpeta, recursive: true);
        GC.SuppressFinalize(this);
    }

    private static ConfiguracionCajaEnMemoria ConfiguracionDePrueba(string? secreto) => new(secreto);

    private static HttpResponseMessage Json(HttpStatusCode codigo, string cuerpo) =>
        new(codigo) { Content = new StringContent(cuerpo, Encoding.UTF8, "application/json") };

    private sealed class ManejadorPrueba(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(responder(request));
    }
}

/// <summary>Central de prueba con una respuesta fija, para el procesador de la bandeja de salida.</summary>
public sealed class CentralDePrueba(ResultadoEnvioCentral resultado, PaqueteBajadaMaestros? bajada = null) : IClienteCentral
{
    public int Recibidos { get; private set; }

    public bool Configurado => true;

    public Task<ResultadoEnvioCentral> EnviarAsync(MensajeSincronizacion mensaje, CancellationToken cancelacion = default)
    {
        Recibidos++;
        return Task.FromResult(resultado);
    }

    public Task<ResultadoBajadaCentral> DescargarMaestrosAsync(long desde, bool conTotales = false, CancellationToken cancelacion = default) =>
        Task.FromResult(ResultadoBajadaCentral.Recibido(bajada ?? new PaqueteBajadaMaestros(desde, desde, null, null)));

    /// <summary>Notas de crédito de otras sucursales que este Central conoce, por código consultado (RF-43).</summary>
    public Dictionary<string, DatosNotaCreditoParaCaja> NotasCredito { get; } = [];

    public List<(string NotaCreditoNumero, string VentaNumero, decimal Monto)> Reservas { get; } = [];

    public List<(string NotaCreditoNumero, string VentaNumero)> ReservasLiberadas { get; } = [];

    /// <summary>Listas de boda que el Central de prueba responde, por su número.</summary>
    public Dictionary<string, DatosListaBodaParaCaja> ListasBoda { get; } = [];

    public Task<ResultadoListaBodaCentral> ConsultarListaBodaAsync(string numero, CancellationToken cancelacion = default) =>
        Task.FromResult(ListasBoda.TryGetValue(numero, out var lista)
            ? ResultadoListaBodaCentral.Encontrada(lista)
            : ResultadoListaBodaCentral.NoExiste($"El Central no tiene la lista {numero}."));

    /// <summary>Cotizaciones que el Central de prueba responde, por su número.</summary>
    public Dictionary<string, DatosCotizacionParaCaja> Cotizaciones { get; } = [];

    public Task<ResultadoCotizacionCentral> ConsultarCotizacionAsync(string numero, CancellationToken cancelacion = default) =>
        Task.FromResult(Cotizaciones.TryGetValue(numero, out var cotizacion)
            ? ResultadoCotizacionCentral.Encontrada(cotizacion)
            : ResultadoCotizacionCentral.NoExiste($"El Central no tiene la cotización {numero}."));

    public Task<ResultadoNotaCreditoCentral> ConsultarNotaCreditoAsync(string codigo, CancellationToken cancelacion = default) =>
        Task.FromResult(NotasCredito.TryGetValue(codigo, out var nota)
            ? ResultadoNotaCreditoCentral.Encontrada(nota)
            : ResultadoNotaCreditoCentral.NoExiste("La nota de crédito no existe en el Central."));

    public Task<ResultadoReservaNotaCredito> ReservarNotaCreditoAsync(string notaCreditoNumero, string ventaNumero, decimal monto,
        CancellationToken cancelacion = default)
    {
        var nota = NotasCredito.Values.SingleOrDefault(n => n.Numero == notaCreditoNumero);
        if (nota is null || nota.Disponible <= 0m)
            return Task.FromResult(ResultadoReservaNotaCredito.Rechazada("La nota de crédito no tiene saldo disponible."));

        var reservado = Math.Min(monto, nota.Disponible);
        Reservas.Add((notaCreditoNumero, ventaNumero, reservado));
        NotasCredito[NotasCredito.First(p => p.Value.Numero == notaCreditoNumero).Key] = nota with { Disponible = nota.Disponible - reservado };
        return Task.FromResult(ResultadoReservaNotaCredito.Reservada(reservado));
    }

    public Task LiberarReservaNotaCreditoAsync(string notaCreditoNumero, string ventaNumero, CancellationToken cancelacion = default)
    {
        ReservasLiberadas.Add((notaCreditoNumero, ventaNumero));
        return Task.CompletedTask;
    }

    /// <summary>Facturas de otras tiendas que este Central entrega para devolver, por número y también por e-NCF.</summary>
    public Dictionary<string, DatosFacturaParaCaja> Facturas { get; } = [];

    /// <summary>Cuando es falso, el Central no responde: sirve para probar la caja sin red.</summary>
    public bool Responde { get; set; } = true;

    public List<(string FacturaNumero, IReadOnlyDictionary<int, decimal> Lineas)> ReservasFactura { get; } = [];

    public List<string> ReservasFacturaLiberadas { get; } = [];

    /// <summary>Rechaza la reserva de esa factura, como si otra caja se le hubiera adelantado.</summary>
    public string? RechazaReservaDe { get; set; }

    public Task<ResultadoFacturaCentral> ConsultarFacturaAsync(string numeroOEncf, CancellationToken cancelacion = default) =>
        Task.FromResult(!Responde
            ? ResultadoFacturaCentral.SinConexion("No hay comunicación con el Central.")
            : Facturas.TryGetValue(numeroOEncf, out var factura)
                ? ResultadoFacturaCentral.Encontrada(factura)
                : ResultadoFacturaCentral.NoExiste($"El Central no tiene la factura {numeroOEncf}."));

    public Task<ResultadoReservaFactura> ReservarFacturaAsync(string facturaNumero, IReadOnlyDictionary<int, decimal> lineas,
        CancellationToken cancelacion = default)
    {
        if (!Responde)
            return Task.FromResult(ResultadoReservaFactura.SinConexion("No hay comunicación con el Central."));
        if (RechazaReservaDe == facturaNumero)
            return Task.FromResult(ResultadoReservaFactura.Rechazada($"La factura {facturaNumero} la está devolviendo otra caja ahora mismo."));

        ReservasFactura.Add((facturaNumero, lineas));
        return Task.FromResult(ResultadoReservaFactura.Reservada());
    }

    public Task LiberarReservaFacturaAsync(string facturaNumero, CancellationToken cancelacion = default)
    {
        ReservasFacturaLiberadas.Add(facturaNumero);
        return Task.CompletedTask;
    }

}
