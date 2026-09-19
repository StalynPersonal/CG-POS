using System.Net.Http.Json;
using System.Text.Json;
using CgPos.Central.Aplicacion.Organizacion;
using CgPos.Central.Pruebas.Soporte;
using CgPos.Contratos.Catalogo;
using CgPos.Contratos.Central;
using CgPos.Contratos.Serializacion;
using CgPos.Contratos.Sincronizacion;
using CgPos.Contratos.Ventas;
using CgPos.Dominio.Comun;
using CgPos.Dominio.Fiscal;
using CgPos.Dominio.ListasBoda;
using CgPos.Dominio.Pagos;

namespace CgPos.Central.Pruebas.Api;

[Collection(ColeccionCentral.Nombre)]
public class ApiListasBodaPruebas(CentralEnPruebas central)
{
    [SkippableFact]
    public async Task La_lista_se_crea_en_el_central_la_consulta_la_caja_y_la_compra_descuenta_lo_pedido()
    {
        Skip.If(central.MotivoOmision is not null, central.MotivoOmision);
        using var cliente = central.CrearCliente();
        var admin = await CentralEnPruebas.TokenAdministradorAsync(cliente);
        var tokenCaja = await CentralEnPruebas.TokenCajaAsync(cliente, CentralEnPruebas.CajaUno);
        await central.CambiarParametroAsync(ClavesParametrosCentral.ListasBodaDescontarCompras, "true");

        // Los artículos de la lista son los del maestro: se piden por su código interno y tienen que existir.
        var codigoArticulo = await CrearArticuloAsync(cliente, admin);
        var solicitud = new SolicitudListaBoda(null, "Boda de Ana y Luis", new DateOnly(2026, 12, 12), "Salón Jardín", "40212345678", "Ana Pérez",
            "8095551234", "ana@ejemplo.do", null, null, [new SolicitudArticuloListaBoda(codigoArticulo, "Juego de copas", 6m)]);

        var inventado = await EnviarAsync(cliente, admin, HttpMethod.Post, "/api/manager/listas-boda",
            solicitud with { Articulos = [new SolicitudArticuloListaBoda("NOEXISTE000", "Lo que sea", 1m)] });
        Assert.False(inventado.Exitosa);
        Assert.Contains("No existe el artículo", inventado.Mensaje);

        var creada = await EnviarAsync(cliente, admin, HttpMethod.Post, "/api/manager/listas-boda", solicitud);
        Assert.True(creada.Exitosa, creada.Mensaje);

        // El Central numera la lista; la caja la consulta por ese número, sin Ids del Central.
        var listas = await ObtenerAsync<List<DatosListaBoda>>(cliente, admin, "/api/manager/listas-boda?buscar=Ana%20P%C3%A9rez");
        var lista = Assert.Single(listas, l => l.ClienteDocumento == "40212345678");
        Assert.StartsWith("LB", lista.Numero, StringComparison.Ordinal);

        var paraCaja = await ObtenerAsync<DatosListaBodaParaCaja>(cliente, tokenCaja, $"/api/listas-boda/{lista.Numero}");
        Assert.Equal((lista.Numero, "Boda de Ana y Luis", EstadoListaBoda.Abierta, true), (paraCaja.Numero, paraCaja.Evento, paraCaja.Estado, paraCaja.DescuentaCompras));
        Assert.Equal(6m, Assert.Single(paraCaja.Articulos).Pendiente);

        // La caja cobra contra la lista: la compra baja lo pedido y queda en el historial.
        var numeroFactura = CentralEnPruebas.NumeroDocumento(CentralEnPruebas.CajaUno, TipoDocumentoNumerado.Factura);
        Assert.Equal(EstadoRecepcion.Recibido, await EnviarVentaAsync(cliente, tokenCaja, Venta(numeroFactura, lista.Numero, codigoArticulo, 2m)));

        var despues = await ObtenerAsync<DatosListaBodaParaCaja>(cliente, tokenCaja, $"/api/listas-boda/{lista.Numero}");
        var articulo = Assert.Single(despues.Articulos);
        Assert.Equal((2m, 4m), (articulo.Comprado, articulo.Pendiente));

        var detalle = await ObtenerAsync<DatosListaBoda>(cliente, admin, $"/api/manager/listas-boda/{lista.Id}");
        Assert.Equal(numeroFactura, Assert.Single(detalle.Compras).VentaNumero);

        // Reenviar la misma factura no la cuenta dos veces.
        Assert.Equal(EstadoRecepcion.Recibido, await EnviarVentaAsync(cliente, tokenCaja,
            Venta(CentralEnPruebas.NumeroDocumento(CentralEnPruebas.CajaUno, TipoDocumentoNumerado.Factura), lista.Numero, codigoArticulo, 1m)));
        var tercera = await ObtenerAsync<DatosListaBodaParaCaja>(cliente, tokenCaja, $"/api/listas-boda/{lista.Numero}");
        Assert.Equal(3m, Assert.Single(tercera.Articulos).Comprado);

        // Cerrada, la caja la ve cerrada y no la usa en una venta nueva.
        var cerrada = await EnviarAsync(cliente, admin, HttpMethod.Post, $"/api/manager/listas-boda/{lista.Id}/estado?cerrar=true", new { });
        Assert.True(cerrada.Exitosa, cerrada.Mensaje);
        Assert.Equal(EstadoListaBoda.Cerrada, (await ObtenerAsync<DatosListaBodaParaCaja>(cliente, tokenCaja, $"/api/listas-boda/{lista.Numero}")).Estado);
    }

    private static DocumentoVentaCobrada Venta(string numero, string listaBoda, string codigoArticulo, decimal cantidad)
    {
        var cobrada = DateTimeOffset.Now;
        var precio = 500m;
        var importe = precio * cantidad;
        return new DocumentoVentaCobrada(numero, 1, "C001", "Cajero Desarrollo", cobrada.AddMinutes(-5), cobrada, TipoComprobante.FacturaConsumo, null, "DOP",
            [
                new DocumentoLineaVenta(1, codigoArticulo, codigoArticulo, "Juego de copas", CgPos.Dominio.Catalogo.TipoArticulo.Normal, "UND", 0, cantidad,
                    precio, importe, 18m, CgPos.Dominio.Catalogo.ListaPrecio.Detalle, CgPos.Dominio.Catalogo.MotivoPrecio.PrecioDetalle, false, false, null,
                    false, null, null, 0m, 0m, null, null, null, null, 0m, importe, 0m),
            ],
            new DatosTotalesVenta(importe / 1.18m, importe - (importe / 1.18m), importe, 1, cantidad, []), null,
            [new DocumentoPagoVenta(1, "EFE", "Efectivo", TipoFormaPago.Efectivo, "DOP", importe, null, importe, null, null, null, null, false)],
            importe, 0m, 0m, null, null, [], null, listaBoda);
    }

    private static async Task<EstadoRecepcion?> EnviarVentaAsync(HttpClient cliente, string token, DocumentoVentaCobrada venta)
    {
        var contenido = JsonSerializer.Serialize(venta, OpcionesJson.Predeterminadas);
        var (sucursal, caja) = CentralEnPruebas.CodigosCaja(CentralEnPruebas.CajaUno);
        var mensaje = new MensajeSincronizacion(Guid.CreateVersion7(), TiposMensaje.VentaCobrada, venta.Numero, contenido,
            HashSincronizacion.Calcular(contenido), sucursal, caja, DateTimeOffset.UtcNow);
        using var respuesta = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Post, "/api/sincronizacion/mensajes", token, mensaje));
        return (await respuesta.Content.ReadFromJsonAsync<RespuestaRecepcionCentral>(OpcionesJson.Predeterminadas))?.Estado;
    }

    /// <summary>Artículo nuevo en el maestro, para pedirlo en la lista por su código interno.</summary>
    private static async Task<string> CrearArticuloAsync(HttpClient cliente, string admin)
    {
        var categoria = (await ObtenerAsync<List<DatosMaestroCentral<CategoriaCarga>>>(cliente, admin, "/api/maestros/categorias")).First(c => c.Dato.Activa).Dato;
        var unidad = (await ObtenerAsync<List<DatosMaestroCentral<UnidadMedidaCarga>>>(cliente, admin, "/api/maestros/unidades-medida")).First().Dato;
        var impuesto = (await ObtenerAsync<List<DatosMaestroCentral<ImpuestoCarga>>>(cliente, admin, "/api/maestros/impuestos")).First(i => i.Dato.Activo).Dato;

        var codigo = $"ART{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
        var articulo = new ArticuloCarga(codigo, "Juego de copas", categoria.DepartamentoCodigo, unidad.Codigo, impuesto.Codigo, 1200m,
            CategoriaCodigo: categoria.Codigo);
        var creado = await EnviarAsync(cliente, admin, HttpMethod.Post, "/api/maestros/articulos", articulo);
        Assert.True(creado.Exitosa, creado.Mensaje);
        return codigo;
    }

    private static async Task<RespuestaAdministracion> EnviarAsync(HttpClient cliente, string token, HttpMethod metodo, string ruta, object cuerpo)
    {
        using var respuesta = await cliente.SendAsync(CentralEnPruebas.Solicitud(metodo, ruta, token, cuerpo));
        return (await respuesta.Content.ReadFromJsonAsync<RespuestaAdministracion>(OpcionesJson.Predeterminadas))!;
    }

    private static async Task<T> ObtenerAsync<T>(HttpClient cliente, string token, string ruta)
    {
        using var respuesta = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Get, ruta, token));
        respuesta.EnsureSuccessStatusCode();
        return (await respuesta.Content.ReadFromJsonAsync<T>(OpcionesJson.Predeterminadas))!;
    }
}
