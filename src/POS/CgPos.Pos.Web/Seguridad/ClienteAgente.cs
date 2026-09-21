using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CgPos.Contratos.Catalogo;
using CgPos.Contratos.Fidelidad;
using CgPos.Contratos.Pantallas;
using CgPos.Contratos.Seguridad;
using CgPos.Contratos.Serializacion;
using CgPos.Contratos.Ventas;
using CgPos.Dominio.Entregas;
using CgPos.Dominio.Fiscal;
using CgPos.Dominio.Ventas;

namespace CgPos.Pos.Web.Seguridad;

/// <summary>Llamadas al servicio local CG-POS Agente (mismo origen que la pantalla).</summary>
public sealed class ClienteAgente(IHttpClientFactory fabricaHttp, AlmacenSesion almacen)
{
    public const string NombreHttp = "Agente";
    private const string SinComunicacion = "No hay comunicación con el servicio de la caja (CG-POS Agente).";

    private HttpClient Http => fabricaHttp.CreateClient(NombreHttp);

    public async Task<DatosEstadoCaja?> ObtenerEstadoCajaAsync(CancellationToken cancelacion = default)
    {
        try
        {
            return await Http.GetFromJsonAsync<DatosEstadoCaja>("api/caja/estado", OpcionesJson.Predeterminadas, cancelacion);
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    /// <summary>Qué caja es este equipo. Nulo si el Agente no responde.</summary>
    public async Task<DatosConfiguracionPantalla?> ObtenerConfiguracionAsync(CancellationToken cancelacion = default)
    {
        try
        {
            return await Http.GetFromJsonAsync<DatosConfiguracionPantalla>("api/configuracion", OpcionesJson.Predeterminadas, cancelacion);
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    public async Task<RespuestaConfiguracion> ConfigurarCajaAsync(SolicitudConfigurarCajaPantalla solicitud, CancellationToken cancelacion = default)
    {
        try
        {
            using var respuesta = await Http.PostAsJsonAsync("api/configuracion", solicitud, OpcionesJson.Predeterminadas, cancelacion);
            return await respuesta.Content.ReadFromJsonAsync<RespuestaConfiguracion>(OpcionesJson.Predeterminadas, cancelacion)
                ?? new RespuestaConfiguracion(false, SinComunicacion);
        }
        catch (Exception excepcion) when (excepcion is HttpRequestException or System.Text.Json.JsonException)
        {
            return new RespuestaConfiguracion(false, SinComunicacion);
        }
    }

    public Task<RespuestaIngreso> IngresarAsync(string codigoUsuario, string clave, CancellationToken cancelacion = default) =>
        IngresarAsync("api/sesion/ingreso", new SolicitudIngreso(codigoUsuario, clave), cancelacion);

    public async Task<RespuestaAutorizacion> SolicitarAutorizacionAsync(SolicitudAutorizacion solicitud, CancellationToken cancelacion = default)
    {
        try
        {
            using var respuesta = await Http.PostAsJsonAsync("api/autorizaciones", solicitud, OpcionesJson.Predeterminadas, cancelacion);
            if (respuesta.StatusCode == HttpStatusCode.Unauthorized)
                return new RespuestaAutorizacion(false, "La sesión expiró. Vuelva a iniciar sesión.");

            return await LeerAsync<RespuestaAutorizacion>(respuesta, cancelacion)
                ?? new RespuestaAutorizacion(false, $"Respuesta inesperada del servicio ({(int)respuesta.StatusCode}).");
        }
        catch (HttpRequestException)
        {
            return new RespuestaAutorizacion(false, SinComunicacion);
        }
    }

    // ---------- Turno y venta (C3) ----------

    public async Task<DatosEstadoTurno?> ObtenerEstadoTurnoAsync(CancellationToken cancelacion = default)
    {
        try
        {
            return await Http.GetFromJsonAsync<DatosEstadoTurno>("api/turnos/actual", OpcionesJson.Predeterminadas, cancelacion);
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    public Task<RespuestaTurno> AbrirTurnoAsync(decimal? fondoInicial, CancellationToken cancelacion = default) =>
        EnviarAsync(HttpMethod.Post, "api/turnos", new SolicitudAbrirTurno(fondoInicial),
            mensaje => new RespuestaTurno(CodigoResultadoTurno.CajaNoOperativa, mensaje, null), cancelacion);

    public Task<RespuestaVenta> ObtenerVentaActualAsync(CancellationToken cancelacion = default) =>
        EnviarAsync<object?, RespuestaVenta>(HttpMethod.Get, "api/ventas/actual", null, ErrorVenta, cancelacion);

    public Task<RespuestaVenta> AgregarArticuloAsync(int ventaId, string codigo, decimal? cantidad = null, string? serial = null, bool serialEnDespacho = false,
        CancellationToken cancelacion = default) =>
        EnviarAsync(HttpMethod.Post, $"api/ventas/{ventaId}/lineas", new SolicitudAgregarArticulo(codigo, cantidad, serial, serialEnDespacho), ErrorVenta, cancelacion);

    // ---------- Pendientes de entrega y envíos (C10) ----------

    public Task<RespuestaVenta> MarcarEntregaAsync(int ventaId, SolicitudMarcarEntrega solicitud, CancellationToken cancelacion = default) =>
        EnviarAsync(HttpMethod.Post, $"api/ventas/{ventaId}/entregas", solicitud, ErrorVenta, cancelacion);

    public Task<RespuestaVenta> QuitarEntregaAsync(int ventaId, int numeroDestino, CancellationToken cancelacion = default) =>
        EnviarAsync<object?, RespuestaVenta>(HttpMethod.Delete, $"api/ventas/{ventaId}/entregas/{numeroDestino}", null, ErrorVenta, cancelacion);

    public async Task<IReadOnlyList<DatosSucursalRetiro>> ListarSucursalesRetiroAsync(CancellationToken cancelacion = default)
    {
        try
        {
            return await Http.GetFromJsonAsync<List<DatosSucursalRetiro>>("api/entregas/sucursales", OpcionesJson.Predeterminadas, cancelacion) ?? [];
        }
        catch (Exception excepcion) when (excepcion is HttpRequestException or JsonException)
        {
            return [];
        }
    }

    public Task<RespuestaVenta> AgregarDesdeBalanzaAsync(int ventaId, string codigo, CancellationToken cancelacion = default) =>
        EnviarAsync(HttpMethod.Post, $"api/ventas/{ventaId}/lineas/balanza", new SolicitudPesarArticulo(codigo), ErrorVenta, cancelacion);

    public Task<RespuestaVenta> CambiarCantidadAsync(int ventaId, int numeroLinea, decimal cantidad, CancellationToken cancelacion = default) =>
        EnviarAsync(HttpMethod.Put, $"api/ventas/{ventaId}/lineas/{numeroLinea}/cantidad", new SolicitudCambiarCantidad(cantidad), ErrorVenta, cancelacion);

    public Task<RespuestaVenta> EliminarLineaAsync(int ventaId, int numeroLinea, Guid? autorizacionId, CancellationToken cancelacion = default) =>
        EnviarAsync(HttpMethod.Post, $"api/ventas/{ventaId}/lineas/{numeroLinea}/eliminar", new SolicitudConAutorizacion(autorizacionId), ErrorVenta, cancelacion);

    public Task<RespuestaVenta> EliminarPorCodigoAsync(int ventaId, string codigo, Guid? autorizacionId, CancellationToken cancelacion = default) =>
        EnviarAsync(HttpMethod.Post, $"api/ventas/{ventaId}/eliminar-por-codigo", new SolicitudEliminarPorCodigo(codigo, autorizacionId), ErrorVenta, cancelacion);

    public Task<RespuestaVenta> LimpiarVentaAsync(int ventaId, Guid? autorizacionId, CancellationToken cancelacion = default) =>
        EnviarAsync(HttpMethod.Post, $"api/ventas/{ventaId}/limpiar", new SolicitudConAutorizacion(autorizacionId), ErrorVenta, cancelacion);

    // ---------- Cliente, comprobante, límite, espera, anular y suspender (C4) ----------

    public Task<RespuestaVenta> AsignarClienteAsync(int ventaId, string documento, string? nombre, CancellationToken cancelacion = default) =>
        EnviarAsync(HttpMethod.Post, $"api/ventas/{ventaId}/cliente", new SolicitudAsignarCliente(documento, nombre), ErrorVenta, cancelacion);

    public Task<RespuestaVenta> QuitarClienteAsync(int ventaId, CancellationToken cancelacion = default) =>
        EnviarAsync<object?, RespuestaVenta>(HttpMethod.Delete, $"api/ventas/{ventaId}/cliente", null, ErrorVenta, cancelacion);

    // ---------- Programa de fidelidad (C10) ----------

    public Task<RespuestaVenta> AsignarFidelidadAsync(int ventaId, string cedula, CancellationToken cancelacion = default) =>
        EnviarAsync(HttpMethod.Post, $"api/ventas/{ventaId}/fidelidad", new SolicitudAsignarFidelidad(cedula), ErrorVenta, cancelacion);

    public Task<RespuestaVenta> QuitarFidelidadAsync(int ventaId, CancellationToken cancelacion = default) =>
        EnviarAsync<object?, RespuestaVenta>(HttpMethod.Delete, $"api/ventas/{ventaId}/fidelidad", null, ErrorVenta, cancelacion);

    public Task<RespuestaFidelidad> ConsultarFidelidadAsync(string cedula, CancellationToken cancelacion = default) =>
        EnviarAsync<object?, RespuestaFidelidad>(HttpMethod.Get, $"api/fidelidad/miembros/{Uri.EscapeDataString(cedula)}", null,
            mensaje => new RespuestaFidelidad(CodigoResultadoFidelidad.NoInscrito, mensaje, null), cancelacion);

    public Task<RespuestaFidelidad> InscribirFidelidadAsync(SolicitudInscripcionFidelidad solicitud, CancellationToken cancelacion = default) =>
        EnviarAsync(HttpMethod.Post, "api/fidelidad/miembros", solicitud, mensaje => new RespuestaFidelidad(CodigoResultadoFidelidad.DatosInvalidos, mensaje, null), cancelacion);

    public Task<RespuestaCotizacion> FacturarCotizacionAsync(int ventaId, string numero, Guid? autorizacionId, CancellationToken cancelacion = default) =>
        EnviarAsync(HttpMethod.Put, $"api/ventas/{ventaId}/cotizacion", new SolicitudFacturarCotizacion(numero, autorizacionId),
            mensaje => new RespuestaCotizacion(CodigoResultadoVenta.SinConexionCentral, mensaje, null, null), cancelacion);

    public Task<RespuestaVenta> RegistrarCertificacionExencionAsync(int ventaId, string? certificacion, CancellationToken cancelacion = default) =>
        EnviarAsync(HttpMethod.Put, $"api/ventas/{ventaId}/certificacion-exencion", new SolicitudCertificacionExencion(certificacion), ErrorVenta, cancelacion);

    public Task<RespuestaVenta> CambiarComprobanteAsync(int ventaId, TipoComprobante tipo, Guid? autorizacionId, CancellationToken cancelacion = default) =>
        EnviarAsync(HttpMethod.Put, $"api/ventas/{ventaId}/comprobante", new SolicitudCambiarComprobante(tipo, autorizacionId), ErrorVenta, cancelacion);

    /// <summary>Asocia o quita la lista de boda de la venta; la lista la valida el Central (RF-73).</summary>
    public Task<RespuestaListaBoda> AsignarListaBodaAsync(int ventaId, string? numero, CancellationToken cancelacion = default) =>
        EnviarAsync(HttpMethod.Put, $"api/ventas/{ventaId}/lista-boda", new SolicitudListaBodaVenta(numero),
            mensaje => new RespuestaListaBoda(CodigoResultadoVenta.SinConexionCentral, mensaje, null, null), cancelacion);

    public Task<RespuestaVenta> EstablecerLimiteCompraAsync(int ventaId, decimal? limite, CancellationToken cancelacion = default) =>
        EnviarAsync(HttpMethod.Put, $"api/ventas/{ventaId}/limite", new SolicitudLimiteCompra(limite), ErrorVenta, cancelacion);

    public Task<RespuestaVenta> PonerEnEsperaAsync(int ventaId, string referencia, CancellationToken cancelacion = default) =>
        EnviarAsync(HttpMethod.Post, $"api/ventas/{ventaId}/espera", new SolicitudPonerEnEspera(referencia), ErrorVenta, cancelacion);

    public async Task<IReadOnlyList<DatosVentaEnEspera>> ListarEnEsperaAsync(CancellationToken cancelacion = default)
    {
        try
        {
            return await Http.GetFromJsonAsync<List<DatosVentaEnEspera>>("api/ventas/espera", OpcionesJson.Predeterminadas, cancelacion) ?? [];
        }
        catch (HttpRequestException)
        {
            return [];
        }
    }

    public Task<RespuestaVenta> RetomarVentaAsync(int ventaId, string? referenciaActual, CancellationToken cancelacion = default) =>
        EnviarAsync(HttpMethod.Post, $"api/ventas/{ventaId}/retomar", new SolicitudRetomarVenta(referenciaActual), ErrorVenta, cancelacion);

    public Task<RespuestaVenta> AnularVentaAsync(int ventaId, string? motivo, Guid? autorizacionId, CancellationToken cancelacion = default) =>
        EnviarAsync(HttpMethod.Post, $"api/ventas/{ventaId}/anular", new SolicitudAnularVenta(motivo, autorizacionId), ErrorVenta, cancelacion);

    public Task<RespuestaVenta> SuspenderCajaAsync(Guid? autorizacionId, CancellationToken cancelacion = default) =>
        EnviarAsync(HttpMethod.Post, "api/caja/suspender", new SolicitudConAutorizacion(autorizacionId), ErrorVenta, cancelacion);

    // ---------- Cobro y periféricos (C6) ----------

    public async Task<DatosCatalogoCobro?> ObtenerCatalogoCobroAsync(CancellationToken cancelacion = default)
    {
        try
        {
            return await Http.GetFromJsonAsync<DatosCatalogoCobro>("api/catalogos/cobro", OpcionesJson.Predeterminadas, cancelacion);
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    public Task<RespuestaOperacionTerminal> CobrarConTerminalAsync(int ventaId, decimal monto, bool pagaSaldo, CancellationToken cancelacion = default) =>
        EnviarAsync(HttpMethod.Post, $"api/ventas/{ventaId}/terminal", new SolicitudCobroTarjeta(monto, pagaSaldo),
            mensaje => new RespuestaOperacionTerminal(CodigoResultadoVenta.TerminalSinConexion, mensaje, null), cancelacion);

    public Task<RespuestaOperacionTerminal> AnularUltimaTarjetaAsync(int ventaId, CancellationToken cancelacion = default) =>
        EnviarAsync<object?, RespuestaOperacionTerminal>(HttpMethod.Post, $"api/ventas/{ventaId}/terminal/anular-ultima", null,
            mensaje => new RespuestaOperacionTerminal(CodigoResultadoVenta.TerminalSinConexion, mensaje, null), cancelacion);

    public Task<RespuestaCobro> CobrarAsync(int ventaId, IReadOnlyList<SolicitudPago> pagos, Guid? autorizacionId, CancellationToken cancelacion = default) =>
        EnviarAsync(HttpMethod.Post, $"api/ventas/{ventaId}/cobrar", new SolicitudCobro(pagos, autorizacionId),
            mensaje => new RespuestaCobro(CodigoResultadoVenta.VentaNoEditable, mensaje, null, null), cancelacion);

    public Task<RespuestaImpresion> ReimprimirUltimoAsync(CancellationToken cancelacion = default) =>
        EnviarAsync<object?, RespuestaImpresion>(HttpMethod.Post, "api/impresion/reimprimir-ultimo", null, mensaje => new RespuestaImpresion(false, mensaje), cancelacion);

    public Task<RespuestaVenta> AbrirGavetaAsync(Guid? autorizacionId, CancellationToken cancelacion = default) =>
        EnviarAsync(HttpMethod.Post, "api/caja/gaveta", new SolicitudConAutorizacion(autorizacionId), ErrorVenta, cancelacion);

    // ---------- Descuentos y ofertas (C5) ----------

    public Task<RespuestaVenta> AplicarDescuentoLineaAsync(int ventaId, int numeroLinea, TipoDescuento tipo, decimal valor, string? motivo, Guid? autorizacionId,
        CancellationToken cancelacion = default) =>
        EnviarAsync(HttpMethod.Post, $"api/ventas/{ventaId}/lineas/{numeroLinea}/descuento", new SolicitudDescuentoLinea(tipo, valor, motivo, autorizacionId), ErrorVenta, cancelacion);

    public Task<RespuestaVenta> QuitarDescuentoLineaAsync(int ventaId, int numeroLinea, CancellationToken cancelacion = default) =>
        EnviarAsync<object?, RespuestaVenta>(HttpMethod.Delete, $"api/ventas/{ventaId}/lineas/{numeroLinea}/descuento", null, ErrorVenta, cancelacion);

    public Task<RespuestaVenta> AplicarDescuentoFacturaAsync(int ventaId, TipoDescuento tipo, decimal valor, IReadOnlyList<int>? lineas, string? motivo,
        Guid? autorizacionId, CancellationToken cancelacion = default) =>
        EnviarAsync(HttpMethod.Post, $"api/ventas/{ventaId}/descuento", new SolicitudDescuentoFactura(tipo, valor, lineas, motivo, autorizacionId), ErrorVenta, cancelacion);

    /// <summary>Descuento del banco por el BIN de la tarjeta (RF-98), antes de cobrar.</summary>
    public Task<RespuestaVenta> AplicarDescuentoTarjetaAsync(int ventaId, string bin, CancellationToken cancelacion = default) =>
        EnviarAsync(HttpMethod.Post, $"api/ventas/{ventaId}/descuento-tarjeta", new SolicitudDescuentoTarjeta(bin), ErrorVenta, cancelacion);

    public Task<RespuestaVenta> QuitarDescuentoFacturaAsync(int ventaId, CancellationToken cancelacion = default) =>
        EnviarAsync<object?, RespuestaVenta>(HttpMethod.Delete, $"api/ventas/{ventaId}/descuento", null, ErrorVenta, cancelacion);

    public Task<RespuestaVenta> DesactivarOfertaAsync(int ventaId, int numeroLinea, Guid? autorizacionId, CancellationToken cancelacion = default) =>
        EnviarAsync(HttpMethod.Post, $"api/ventas/{ventaId}/lineas/{numeroLinea}/desactivar-oferta", new SolicitudConAutorizacion(autorizacionId), ErrorVenta, cancelacion);

    public async Task<IReadOnlyList<DatosMotivoDescuento>> ListarMotivosDescuentoAsync(CancellationToken cancelacion = default)
    {
        try
        {
            return await Http.GetFromJsonAsync<List<DatosMotivoDescuento>>("api/descuentos/motivos", OpcionesJson.Predeterminadas, cancelacion) ?? [];
        }
        catch (HttpRequestException)
        {
            return [];
        }
    }

    public async Task<IReadOnlyList<DatosPromocionVigente>> ListarPromocionesVigentesAsync(int articuloId, CancellationToken cancelacion = default)
    {
        try
        {
            return await Http.GetFromJsonAsync<List<DatosPromocionVigente>>($"api/articulos/{articuloId}/promociones", OpcionesJson.Predeterminadas, cancelacion) ?? [];
        }
        catch (HttpRequestException)
        {
            return [];
        }
    }

    public async Task<DatosConsultaDocumento?> ConsultarDocumentoAsync(string documento, CancellationToken cancelacion = default)
    {
        try
        {
            return await Http.GetFromJsonAsync<DatosConsultaDocumento>($"api/documentos/{Uri.EscapeDataString(documento)}", OpcionesJson.Predeterminadas, cancelacion);
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    public async Task<DatosEstadoSincronizacion?> ObtenerEstadoSincronizacionAsync(CancellationToken cancelacion = default)
    {
        try
        {
            return await Http.GetFromJsonAsync<DatosEstadoSincronizacion>("api/sincronizacion/estado", OpcionesJson.Predeterminadas, cancelacion);
        }
        catch (Exception excepcion) when (excepcion is HttpRequestException or JsonException)
        {
            return null;
        }
    }

    // ---------- Turno: retiros, relevo y cierre (C8) ----------

    public Task<RespuestaCaja> ObtenerResumenTurnoAsync(CancellationToken cancelacion = default) =>
        EnviarAsync<object?, RespuestaCaja>(HttpMethod.Get, "api/caja/turno/resumen", null, ErrorCaja, cancelacion);

    public Task<RespuestaCaja> PreCierreAsync(Guid? autorizacionId, CancellationToken cancelacion = default) =>
        EnviarAsync(HttpMethod.Post, "api/caja/turno/precierre", new SolicitudConAutorizacion(autorizacionId), ErrorCaja, cancelacion);

    /// <summary>Cierra el lote del terminal y cuadra las tarjetas del turno (RF-215); nulo si el Agente no respondió.</summary>
    public async Task<DatosConciliacionTarjetas?> ConciliarTarjetasAsync(CancellationToken cancelacion = default)
    {
        try
        {
            using var respuesta = await Http.PostAsync("api/caja/turno/conciliacion-tarjetas", null, cancelacion);
            return respuesta.IsSuccessStatusCode
                ? await respuesta.Content.ReadFromJsonAsync<DatosConciliacionTarjetas>(OpcionesJson.Predeterminadas, cancelacion)
                : null;
        }
        catch (Exception excepcion) when (excepcion is HttpRequestException or JsonException or TaskCanceledException)
        {
            return null;
        }
    }

    public Task<RespuestaCaja> RetirarEfectivoAsync(decimal monto, string? motivo, Guid? autorizacionId, CancellationToken cancelacion = default) =>
        EnviarAsync(HttpMethod.Post, "api/caja/turno/retiros", new SolicitudRetiroEfectivo(monto, motivo, autorizacionId), ErrorCaja, cancelacion);

    public Task<RespuestaCaja> RelevarTurnoAsync(Guid? autorizacionId, CancellationToken cancelacion = default) =>
        EnviarAsync(HttpMethod.Post, "api/caja/turno/relevo", new SolicitudConAutorizacion(autorizacionId), ErrorCaja, cancelacion);

    public Task<RespuestaCaja> CerrarTurnoAsync(IReadOnlyList<SolicitudDeclaracionFormaPago> declaraciones, IReadOnlyList<SolicitudConteoDenominacion> conteo,
        Guid? autorizacionId, CancellationToken cancelacion = default) =>
        EnviarAsync(HttpMethod.Post, "api/caja/turno/cierre", new SolicitudCierreTurno(declaraciones, conteo, autorizacionId), ErrorCaja, cancelacion);

    public async Task<IReadOnlyList<DatosCierre>> ListarCierresAsync(int maximo = 10, CancellationToken cancelacion = default)
    {
        try
        {
            return await Http.GetFromJsonAsync<List<DatosCierre>>($"api/caja/cierres/?maximo={maximo}", OpcionesJson.Predeterminadas, cancelacion) ?? [];
        }
        catch (Exception excepcion) when (excepcion is HttpRequestException or JsonException)
        {
            return [];
        }
    }

    public Task<RespuestaCaja> ReimprimirCierreAsync(int cierreId, CancellationToken cancelacion = default) =>
        EnviarAsync<object?, RespuestaCaja>(HttpMethod.Post, $"api/caja/cierres/{cierreId}/reimprimir", null, ErrorCaja, cancelacion);

    private static RespuestaCaja ErrorCaja(string mensaje) => new(CodigoResultadoCaja.TurnoNoAbierto, mensaje);

    // ---------- Devoluciones y notas de crédito (C9) ----------

    public Task<RespuestaFacturaDevolucion> BuscarFacturaDevolucionAsync(string numero, CancellationToken cancelacion = default) =>
        EnviarAsync<object?, RespuestaFacturaDevolucion>(HttpMethod.Get, $"api/devoluciones/factura/{Uri.EscapeDataString(numero)}", null,
            mensaje => new RespuestaFacturaDevolucion(CodigoResultadoDevolucion.FacturaNoEncontrada, mensaje, null), cancelacion);

    public Task<RespuestaDevolucion> RegistrarDevolucionAsync(SolicitudDevolucion solicitud, CancellationToken cancelacion = default) =>
        EnviarAsync(HttpMethod.Post, "api/devoluciones", solicitud, mensaje => new RespuestaDevolucion(CodigoResultadoDevolucion.DevolucionInvalida, mensaje), cancelacion);

    public Task<RespuestaSaldoNotaCredito> ConsultarNotaCreditoAsync(string codigo, CancellationToken cancelacion = default) =>
        EnviarAsync<object?, RespuestaSaldoNotaCredito>(HttpMethod.Get, $"api/devoluciones/notas-credito/{Uri.EscapeDataString(codigo)}", null,
            mensaje => new RespuestaSaldoNotaCredito(CodigoResultadoDevolucion.NotaCreditoNoEncontrada, mensaje, null), cancelacion);

    public Task<RespuestaDevolucion> ReimprimirNotaCreditoAsync(int devolucionId, CancellationToken cancelacion = default) =>
        EnviarAsync<object?, RespuestaDevolucion>(HttpMethod.Post, $"api/devoluciones/{devolucionId}/reimprimir", null,
            mensaje => new RespuestaDevolucion(CodigoResultadoDevolucion.NotaCreditoNoEncontrada, mensaje), cancelacion);

    // ---------- Facturación electrónica (C7) ----------

    public async Task<DatosEstadoEcf?> ObtenerEstadoEcfAsync(CancellationToken cancelacion = default)
    {
        try
        {
            return await Http.GetFromJsonAsync<DatosEstadoEcf>("api/ecf/estado", OpcionesJson.Predeterminadas, cancelacion);
        }
        catch (Exception excepcion) when (excepcion is HttpRequestException or JsonException)
        {
            return null;
        }
    }

    public Task<RespuestaCertificado> CargarCertificadoAsync(string pin, CancellationToken cancelacion = default) =>
        EnviarAsync(HttpMethod.Post, "api/ecf/certificado", new SolicitudCargarCertificado(pin), mensaje => new RespuestaCertificado(false, mensaje, null), cancelacion);

    public async Task<IReadOnlyList<DatosArticuloResumen>> BuscarArticulosAsync(string texto, CancellationToken cancelacion = default)
    {
        try
        {
            return await Http.GetFromJsonAsync<List<DatosArticuloResumen>>($"api/articulos?texto={Uri.EscapeDataString(texto)}&maximo=50", OpcionesJson.Predeterminadas, cancelacion) ?? [];
        }
        catch (HttpRequestException)
        {
            return [];
        }
    }

    public async Task<IReadOnlyList<DatosArticuloResumen>> ListarCatalogoAsync(CancellationToken cancelacion = default)
    {
        try
        {
            return await Http.GetFromJsonAsync<List<DatosArticuloResumen>>("api/articulos/catalogo", OpcionesJson.Predeterminadas, cancelacion) ?? [];
        }
        catch (HttpRequestException)
        {
            return [];
        }
    }

    /// <summary>Imágenes y mensaje para la pantalla del cliente (no requiere sesión).</summary>
    public async Task<DatosPublicidad?> ObtenerPublicidadAsync(CancellationToken cancelacion = default)
    {
        try
        {
            return await Http.GetFromJsonAsync<DatosPublicidad>("api/pantallas/publicidad", OpcionesJson.Predeterminadas, cancelacion);
        }
        catch (Exception excepcion) when (excepcion is HttpRequestException or JsonException)
        {
            return null;
        }
    }

    public async Task<DatosArticuloVenta?> ConsultarArticuloAsync(string codigo, CancellationToken cancelacion = default)
    {
        try
        {
            using var respuesta = await Http.GetAsync($"api/articulos/codigo/{Uri.EscapeDataString(codigo)}", cancelacion);
            return respuesta.IsSuccessStatusCode ? await LeerAsync<DatosArticuloVenta>(respuesta, cancelacion) : null;
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    private static RespuestaVenta ErrorVenta(string mensaje) => new(CodigoResultadoVenta.VentaNoEditable, mensaje, null);

    /// <summary>Envía la solicitud y lee la respuesta de negocio aunque el código HTTP sea de rechazo (409/422).</summary>
    private async Task<TResultado> EnviarAsync<TCuerpo, TResultado>(HttpMethod metodo, string ruta, TCuerpo cuerpo, Func<string, TResultado> error, CancellationToken cancelacion)
        where TResultado : class
    {
        try
        {
            using var solicitud = new HttpRequestMessage(metodo, ruta);
            if (cuerpo is not null)
                solicitud.Content = JsonContent.Create(cuerpo, options: OpcionesJson.Predeterminadas);

            using var respuesta = await Http.SendAsync(solicitud, cancelacion);
            if (respuesta.StatusCode == HttpStatusCode.Unauthorized)
                return error("La sesión expiró. Vuelva a iniciar sesión.");
            if (respuesta.StatusCode == HttpStatusCode.Forbidden)
                return error("No tiene permiso para esta operación.");

            return await LeerAsync<TResultado>(respuesta, cancelacion)
                ?? error(await TextoAsync(respuesta, cancelacion) ?? $"Respuesta inesperada del servicio ({(int)respuesta.StatusCode}).");
        }
        catch (HttpRequestException)
        {
            return error(SinComunicacion);
        }
    }

    public async Task CerrarSesionAsync(CancellationToken cancelacion = default)
    {
        try
        {
            using var respuesta = await Http.PostAsync("api/sesion/cerrar", null, cancelacion);
        }
        catch (HttpRequestException)
        {
            // Sin comunicación: la sesión igual se cierra en esta pantalla.
        }
        finally
        {
            almacen.Limpiar();
        }
    }

    private async Task<RespuestaIngreso> IngresarAsync<TCuerpo>(string ruta, TCuerpo cuerpo, CancellationToken cancelacion)
    {
        try
        {
            using var respuesta = await Http.PostAsJsonAsync(ruta, cuerpo, OpcionesJson.Predeterminadas, cancelacion);
            var resultado = await LeerAsync<RespuestaIngreso>(respuesta, cancelacion)
                ?? new RespuestaIngreso(false, await TextoAsync(respuesta, cancelacion) ?? $"Respuesta inesperada del servicio ({(int)respuesta.StatusCode}).");

            if (resultado is { Exitoso: true, Token: { } token, ExpiraEn: { } expiraEn, Sesion: { } sesion })
                almacen.Establecer(token, expiraEn, sesion);

            return resultado;
        }
        catch (HttpRequestException)
        {
            return new RespuestaIngreso(false, SinComunicacion);
        }
    }

    /// <summary>Motivo en texto plano que envía el Agente, por ejemplo un parámetro de negocio sin configurar.</summary>
    private static async Task<string?> TextoAsync(HttpResponseMessage respuesta, CancellationToken cancelacion) =>
        respuesta.Content.Headers.ContentType?.MediaType == "text/plain" && await respuesta.Content.ReadAsStringAsync(cancelacion) is { Length: > 0 } texto
            ? texto
            : null;

    private static async Task<TResultado?> LeerAsync<TResultado>(HttpResponseMessage respuesta, CancellationToken cancelacion)
        where TResultado : class
    {
        if (respuesta.Content.Headers.ContentType?.MediaType != "application/json")
            return null;

        try
        {
            return await respuesta.Content.ReadFromJsonAsync<TResultado>(OpcionesJson.Predeterminadas, cancelacion);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
