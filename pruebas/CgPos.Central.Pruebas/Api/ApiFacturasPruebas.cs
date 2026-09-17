using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CgPos.Central.Pruebas.Soporte;
using CgPos.Contratos.Central;
using CgPos.Contratos.Serializacion;
using CgPos.Contratos.Sincronizacion;
using CgPos.Contratos.Ventas;
using CgPos.Dominio.Catalogo;
using CgPos.Dominio.Comun;
using CgPos.Dominio.Fiscal;
using CgPos.Dominio.Pagos;
using CgPos.Dominio.Reportes;
using CgPos.Dominio.Seguridad;

namespace CgPos.Central.Pruebas.Api;

[Collection(ColeccionCentral.Nombre)]
public class ApiFacturasPruebas(CentralEnPruebas central)
{
    [SkippableFact]
    public async Task Las_facturas_que_suben_las_cajas_se_listan_y_se_abren_con_su_detalle()
    {
        Skip.If(central.MotivoOmision is not null, central.MotivoOmision);
        using var cliente = central.CrearCliente();
        var admin = await CentralEnPruebas.TokenAdministradorAsync(cliente);
        var token = await CentralEnPruebas.TokenCajaAsync(cliente, CentralEnPruebas.CajaUno);

        var dia = DateOnly.FromDateTime(DateTime.Today);
        var numero = CentralEnPruebas.NumeroDocumento(CentralEnPruebas.CajaUno, TipoDocumentoNumerado.Factura);
        var codigo = $"ART{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
        Assert.Equal(EstadoRecepcion.Recibido, await EnviarAsync(cliente, token, Venta(numero, codigo)));

        var pagina = await ObtenerAsync<PaginaComprobantesRecibidos>(cliente, admin,
            $"/api/manager/facturas?desde={dia:yyyy-MM-dd}&hasta={dia:yyyy-MM-dd}&buscar={numero}");
        var fila = Assert.Single(pagina.Elementos);
        Assert.Equal((numero, TipoComprobanteVenta.Factura, 1180m, 2), (fila.Numero, fila.Tipo, fila.Total, fila.CantidadLineas));
        Assert.Equal(1180m, pagina.SumaTotal);

        // El detalle trae las líneas tal como las cobró la caja, con sus impuestos y formas de pago.
        var detalle = await ObtenerAsync<DatosComprobanteRecibidoDetalle>(cliente, admin, $"/api/manager/facturas/{fila.Id}");
        Assert.Equal(2, detalle.Lineas.Count);
        var linea = Assert.Single(detalle.Lineas, l => l.Codigo == codigo);
        Assert.Equal((2m, 1000m), (linea.Cantidad, linea.Importe));
        Assert.Equal(TipoFormaPago.Efectivo, Assert.Single(detalle.Pagos).Tipo);
        Assert.Equal(18m, Assert.Single(detalle.Impuestos).Porcentaje);

        // Filtrar por otro tipo de documento deja la factura fuera.
        var soloNotas = await ObtenerAsync<PaginaComprobantesRecibidos>(cliente, admin,
            $"/api/manager/facturas?desde={dia:yyyy-MM-dd}&hasta={dia:yyyy-MM-dd}&buscar={numero}&tipo={TipoComprobanteVenta.NotaCredito}");
        Assert.Empty(soloNotas.Elementos);
    }

    [SkippableFact]
    public async Task Sin_permiso_de_reportes_no_se_ven_las_facturas()
    {
        Skip.If(central.MotivoOmision is not null, central.MotivoOmision);
        using var cliente = central.CrearCliente();
        var codigo = $"SINFAC{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
        await central.CrearUsuarioAsync(codigo, "Sin.Facturas#2026", false, CatalogoPermisosCentral.AdministrarFidelidad);
        var token = (await CentralEnPruebas.IngresarAsync(cliente, codigo, "Sin.Facturas#2026")).Cuerpo!.TokenAcceso!;

        using var respuesta = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Get,
            "/api/manager/facturas?desde=2026-01-01&hasta=2026-01-31", token));

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    private static DocumentoVentaCobrada Venta(string numero, string codigo)
    {
        var cobrada = DateTimeOffset.Now;
        DocumentoLineaVenta Linea(int numeroLinea, string codigoArticulo, string descripcion, decimal cantidad, decimal precio) =>
            new(numeroLinea, codigoArticulo, codigoArticulo, descripcion, TipoArticulo.Normal, "UND", 0, cantidad, precio, precio * cantidad, 18m,
                ListaPrecio.Detalle, MotivoPrecio.PrecioDetalle, false, false, null, false, null, null, 0m, 0m, null, null, null, null, 0m,
                precio * cantidad, 0m);

        return new DocumentoVentaCobrada(numero, 1, "C001", "Cajero Desarrollo", cobrada.AddMinutes(-5), cobrada, TipoComprobante.FacturaConsumo, null, "DOP",
            [Linea(1, codigo, "Cemento gris", 2m, 500m), Linea(2, $"{codigo}B", "Cincel", 1m, 180m)],
            new DatosTotalesVenta(1000m, 180m, 1180m, 2, 3m, [new DatosDesgloseImpuesto(18m, 1, 1000m, 180m, 1180m)]), null,
            [new DocumentoPagoVenta(1, "EFE", "Efectivo", TipoFormaPago.Efectivo, "DOP", 1180m, null, 1180m, null, null, null, null, false)],
            1180m, 0m, 0m, null, null, []);
    }

    private static async Task<EstadoRecepcion?> EnviarAsync(HttpClient cliente, string token, DocumentoVentaCobrada venta)
    {
        var contenido = JsonSerializer.Serialize(venta, OpcionesJson.Predeterminadas);
        var (sucursal, caja) = CentralEnPruebas.CodigosCaja(CentralEnPruebas.CajaUno);
        var mensaje = new MensajeSincronizacion(Guid.CreateVersion7(), TiposMensaje.VentaCobrada, venta.Numero, contenido,
            HashSincronizacion.Calcular(contenido), sucursal, caja, DateTimeOffset.UtcNow);
        using var respuesta = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Post, "/api/sincronizacion/mensajes", token, mensaje));
        return (await respuesta.Content.ReadFromJsonAsync<RespuestaRecepcionCentral>(OpcionesJson.Predeterminadas))?.Estado;
    }

    private static async Task<T> ObtenerAsync<T>(HttpClient cliente, string token, string ruta)
    {
        using var respuesta = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Get, ruta, token));
        respuesta.EnsureSuccessStatusCode();
        return (await respuesta.Content.ReadFromJsonAsync<T>(OpcionesJson.Predeterminadas))!;
    }
}
