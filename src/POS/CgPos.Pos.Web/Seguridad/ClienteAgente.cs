using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CgPos.Contratos.Catalogo;
using CgPos.Contratos.Pantallas;
using CgPos.Contratos.Seguridad;
using CgPos.Contratos.Serializacion;
using CgPos.Contratos.Ventas;
using CgPos.Dominio.Fiscal;

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

    public Task<RespuestaIngreso> IngresarConPinAsync(string codigoUsuario, string pin, CancellationToken cancelacion = default) =>
        IngresarAsync("api/sesion/pin", new SolicitudIngresoPin(codigoUsuario, pin), cancelacion);

    public Task<RespuestaIngreso> IngresarConCarneAsync(string codigoBarras, CancellationToken cancelacion = default) =>
        IngresarAsync("api/sesion/carne", new SolicitudIngresoCarne(codigoBarras), cancelacion);

    public Task<RespuestaIngreso> IngresarConHuellaAsync(CancellationToken cancelacion = default) =>
        IngresarAsync<object?>("api/sesion/huella", null, cancelacion);

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

    public Task<RespuestaVenta> AgregarArticuloAsync(Guid ventaId, string codigo, decimal? cantidad = null, string? serial = null, CancellationToken cancelacion = default) =>
        EnviarAsync(HttpMethod.Post, $"api/ventas/{ventaId}/lineas", new SolicitudAgregarArticulo(codigo, cantidad, serial), ErrorVenta, cancelacion);

    public Task<RespuestaVenta> AgregarDesdeBalanzaAsync(Guid ventaId, string codigo, CancellationToken cancelacion = default) =>
        EnviarAsync(HttpMethod.Post, $"api/ventas/{ventaId}/lineas/balanza", new SolicitudPesarArticulo(codigo), ErrorVenta, cancelacion);

    public Task<RespuestaVenta> CambiarCantidadAsync(Guid ventaId, int numeroLinea, decimal cantidad, CancellationToken cancelacion = default) =>
        EnviarAsync(HttpMethod.Put, $"api/ventas/{ventaId}/lineas/{numeroLinea}/cantidad", new SolicitudCambiarCantidad(cantidad), ErrorVenta, cancelacion);

    public Task<RespuestaVenta> EliminarLineaAsync(Guid ventaId, int numeroLinea, Guid? autorizacionId, CancellationToken cancelacion = default) =>
        EnviarAsync(HttpMethod.Post, $"api/ventas/{ventaId}/lineas/{numeroLinea}/eliminar", new SolicitudConAutorizacion(autorizacionId), ErrorVenta, cancelacion);

    public Task<RespuestaVenta> EliminarPorCodigoAsync(Guid ventaId, string codigo, Guid? autorizacionId, CancellationToken cancelacion = default) =>
        EnviarAsync(HttpMethod.Post, $"api/ventas/{ventaId}/eliminar-por-codigo", new SolicitudEliminarPorCodigo(codigo, autorizacionId), ErrorVenta, cancelacion);

    public Task<RespuestaVenta> LimpiarVentaAsync(Guid ventaId, Guid? autorizacionId, CancellationToken cancelacion = default) =>
        EnviarAsync(HttpMethod.Post, $"api/ventas/{ventaId}/limpiar", new SolicitudConAutorizacion(autorizacionId), ErrorVenta, cancelacion);

    // ---------- Cliente, comprobante, límite, espera, anular y suspender (C4) ----------

    public Task<RespuestaVenta> AsignarClienteAsync(Guid ventaId, string documento, string? nombre, CancellationToken cancelacion = default) =>
        EnviarAsync(HttpMethod.Post, $"api/ventas/{ventaId}/cliente", new SolicitudAsignarCliente(documento, nombre), ErrorVenta, cancelacion);

    public Task<RespuestaVenta> QuitarClienteAsync(Guid ventaId, CancellationToken cancelacion = default) =>
        EnviarAsync<object?, RespuestaVenta>(HttpMethod.Delete, $"api/ventas/{ventaId}/cliente", null, ErrorVenta, cancelacion);

    public Task<RespuestaVenta> CambiarComprobanteAsync(Guid ventaId, TipoComprobante tipo, Guid? autorizacionId, CancellationToken cancelacion = default) =>
        EnviarAsync(HttpMethod.Put, $"api/ventas/{ventaId}/comprobante", new SolicitudCambiarComprobante(tipo, autorizacionId), ErrorVenta, cancelacion);

    public Task<RespuestaVenta> EstablecerLimiteCompraAsync(Guid ventaId, decimal? limite, CancellationToken cancelacion = default) =>
        EnviarAsync(HttpMethod.Put, $"api/ventas/{ventaId}/limite", new SolicitudLimiteCompra(limite), ErrorVenta, cancelacion);

    public Task<RespuestaVenta> PonerEnEsperaAsync(Guid ventaId, CancellationToken cancelacion = default) =>
        EnviarAsync<object?, RespuestaVenta>(HttpMethod.Post, $"api/ventas/{ventaId}/espera", null, ErrorVenta, cancelacion);

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

    public Task<RespuestaVenta> RetomarVentaAsync(Guid ventaId, CancellationToken cancelacion = default) =>
        EnviarAsync<object?, RespuestaVenta>(HttpMethod.Post, $"api/ventas/{ventaId}/retomar", null, ErrorVenta, cancelacion);

    public Task<RespuestaVenta> AnularVentaAsync(Guid ventaId, string? motivo, Guid? autorizacionId, CancellationToken cancelacion = default) =>
        EnviarAsync(HttpMethod.Post, $"api/ventas/{ventaId}/anular", new SolicitudAnularVenta(motivo, autorizacionId), ErrorVenta, cancelacion);

    public Task<RespuestaVenta> SuspenderCajaAsync(Guid? autorizacionId, CancellationToken cancelacion = default) =>
        EnviarAsync(HttpMethod.Post, "api/caja/suspender", new SolicitudConAutorizacion(autorizacionId), ErrorVenta, cancelacion);

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
                ?? error($"Respuesta inesperada del servicio ({(int)respuesta.StatusCode}).");
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
                ?? new RespuestaIngreso(false, $"Respuesta inesperada del servicio ({(int)respuesta.StatusCode}).");

            if (resultado is { Exitoso: true, Token: { } token, ExpiraEn: { } expiraEn, Sesion: { } sesion })
                almacen.Establecer(token, expiraEn, sesion);

            return resultado;
        }
        catch (HttpRequestException)
        {
            return new RespuestaIngreso(false, SinComunicacion);
        }
    }

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
