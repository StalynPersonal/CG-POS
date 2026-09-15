using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CgPos.Contratos.Seguridad;
using CgPos.Contratos.Serializacion;
using CgPos.Contratos.Ventas;
using CgPos.Dominio.Seguridad;

namespace CgPos.Pos.Pruebas.Api;

[Collection(ColeccionAgente.Nombre)]
public class ApiVentasPruebas(AgenteEnPruebas agente)
{
    private const string CodigoCincel = "7891114119695";

    [SkippableFact]
    public async Task Flujo_de_venta_por_api_turno_escaneo_rechazos_y_eliminacion_autorizada()
    {
        Skip.If(agente.MotivoOmision is not null, agente.MotivoOmision);
        using var cliente = agente.Fabrica!.CreateClient();

        using (var sinSesion = await cliente.GetAsync("/api/ventas/actual"))
            Assert.Equal(HttpStatusCode.Unauthorized, sinSesion.StatusCode);

        await IniciarSesionAsync(cliente, "C001", "1111");

        // Turno: se abre si no hay (la base del Agente es compartida por la colección) y una segunda apertura choca.
        var estado = await cliente.GetFromJsonAsync<DatosEstadoTurno>("/api/turnos/actual", OpcionesJson.Predeterminadas);
        if (estado!.TurnoAbierto is null)
        {
            Assert.True(estado.PuedeAbrir);
            using var apertura = await cliente.PostAsJsonAsync("/api/turnos", new SolicitudAbrirTurno(2000m), OpcionesJson.Predeterminadas);
            Assert.Equal(HttpStatusCode.OK, apertura.StatusCode);
        }

        using (var repetida = await cliente.PostAsJsonAsync("/api/turnos", new SolicitudAbrirTurno(null), OpcionesJson.Predeterminadas))
        {
            Assert.Equal(HttpStatusCode.Conflict, repetida.StatusCode);
            Assert.Equal(CodigoResultadoTurno.YaExisteTurnoAbierto, (await Leer<RespuestaTurno>(repetida)).Resultado);
        }

        var venta = (await cliente.GetFromJsonAsync<RespuestaVenta>("/api/ventas/actual", OpcionesJson.Predeterminadas))!.Venta!;
        var totalInicial = venta.Totales.Total;

        using (var agregada = await cliente.PostAsJsonAsync($"/api/ventas/{venta.Id}/lineas", new SolicitudAgregarArticulo($"2*{CodigoCincel}"), OpcionesJson.Predeterminadas))
        {
            Assert.Equal(HttpStatusCode.OK, agregada.StatusCode);
            venta = (await Leer<RespuestaVenta>(agregada)).Venta!;
        }

        var linea = venta.Lineas.Last(l => !l.EsReverso && !l.Anulada);
        Assert.Equal(2m, linea.Cantidad);

        // Los maestros de desarrollo traen ofertas para el cincel: el importe es 2 × 850 menos la oferta vigente.
        Assert.Equal(1700m, linea.Importe + linea.DescuentoPromocion);
        Assert.Equal(totalInicial + linea.Importe, venta.Totales.Total);

        using (var inexistente = await cliente.PostAsJsonAsync($"/api/ventas/{venta.Id}/lineas", new SolicitudAgregarArticulo("NO-EXISTE"), OpcionesJson.Predeterminadas))
        {
            Assert.Equal(HttpStatusCode.UnprocessableEntity, inexistente.StatusCode);
            var respuesta = await Leer<RespuestaVenta>(inexistente);
            Assert.Equal(CodigoResultadoVenta.ArticuloNoEncontrado, respuesta.Resultado);
            Assert.NotNull(respuesta.Venta);
        }

        var rutaEliminar = $"/api/ventas/{venta.Id}/lineas/{linea.NumeroLinea}/eliminar";
        using (var sinPermiso = await cliente.PostAsJsonAsync(rutaEliminar, new SolicitudConAutorizacion(), OpcionesJson.Predeterminadas))
        {
            var respuesta = await Leer<RespuestaVenta>(sinPermiso);
            Assert.Equal(CodigoResultadoVenta.RequiereAutorizacion, respuesta.Resultado);
            Assert.Equal(CatalogoPermisos.EliminarLinea, respuesta.PermisoRequerido);
        }

        using var autorizacion = await cliente.PostAsJsonAsync("/api/autorizaciones",
            new SolicitudAutorizacion(CatalogoPermisos.EliminarLinea, "Cliente no lo quiere", "S001", "2222", TipoEntidad: "Venta", EntidadId: venta.NumeroTransaccion),
            OpcionesJson.Predeterminadas);
        var concedida = await Leer<RespuestaAutorizacion>(autorizacion);
        Assert.True(concedida.Concedida, concedida.Mensaje);

        using (var eliminada = await cliente.PostAsJsonAsync(rutaEliminar, new SolicitudConAutorizacion(concedida.AutorizacionId), OpcionesJson.Predeterminadas))
        {
            Assert.Equal(HttpStatusCode.OK, eliminada.StatusCode);
            var respuesta = await Leer<RespuestaVenta>(eliminada);
            Assert.Equal(totalInicial, respuesta.Venta!.Totales.Total);
            Assert.Contains(respuesta.Venta.Lineas, l => l.EsReverso && l.LineaAnuladaNumero == linea.NumeroLinea);
        }

        var sincronizacion = await cliente.GetFromJsonAsync<DatosEstadoSincronizacion>("/api/sincronizacion/estado", OpcionesJson.Predeterminadas);
        Assert.False(sincronizacion!.EnLinea);
    }

    private static async Task<T> Leer<T>(HttpResponseMessage respuesta) =>
        (await respuesta.Content.ReadFromJsonAsync<T>(OpcionesJson.Predeterminadas))!;

    private static async Task IniciarSesionAsync(HttpClient cliente, string codigo, string pin)
    {
        using var respuesta = await cliente.PostAsJsonAsync("/api/sesion/pin", new SolicitudIngresoPin(codigo, pin), OpcionesJson.Predeterminadas);
        var ingreso = await respuesta.Content.ReadFromJsonAsync<RespuestaIngreso>(OpcionesJson.Predeterminadas);
        Assert.True(ingreso!.Exitoso, ingreso.Mensaje);
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ingreso.Token);
    }
}
