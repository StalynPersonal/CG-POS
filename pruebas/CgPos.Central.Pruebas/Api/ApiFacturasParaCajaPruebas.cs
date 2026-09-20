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
using CgPos.Dominio.Devoluciones;
using CgPos.Dominio.Fiscal;
using CgPos.Dominio.Pagos;

namespace CgPos.Central.Pruebas.Api;

/// <summary>
/// La caja le pide al Central cualquier factura de la empresa para devolverla. Lo importante es que el Central diga cuánto
/// queda disponible de cada línea: es el único que ve las devoluciones de todas las tiendas.
/// </summary>
[Collection(ColeccionCentral.Nombre)]
public class ApiFacturasParaCajaPruebas(CentralEnPruebas central)
{
    [SkippableFact]
    public async Task La_caja_encuentra_una_factura_de_otra_sucursal_con_lo_que_ya_se_devolvio()
    {
        Skip.If(central.MotivoOmision is not null, central.MotivoOmision);
        using var cliente = central.CrearCliente();
        var cajaUno = await CentralEnPruebas.TokenCajaAsync(cliente, CentralEnPruebas.CajaUno);
        var cajaDos = await CentralEnPruebas.TokenCajaAsync(cliente, CentralEnPruebas.CajaDos);

        // La caja 01 vende 5 unidades y sube su factura.
        var numero = CentralEnPruebas.NumeroDocumento(CentralEnPruebas.CajaUno, TipoDocumentoNumerado.Factura);
        var encf = $"E32{Random.Shared.NextInt64(1, 9_999_999_999):D10}";
        Assert.Equal(EstadoRecepcion.Recibido, await EnviarAsync(cliente, cajaUno,
            Mensaje(TiposMensaje.VentaCobrada, numero, Factura(numero, encf, cantidad: 5m), CentralEnPruebas.CajaUno)));

        // La caja 02, que no la emitió, la encuentra igual y la ve completa.
        var factura = await BuscarAsync(cliente, cajaDos, numero);
        Assert.Equal((numero, encf, false), (factura.Numero, factura.Encf, factura.Propia));
        var linea = Assert.Single(factura.Lineas);
        Assert.Equal((5m, 0m, 5m), (linea.Cantidad, linea.Devuelta, linea.Disponible));

        // Para la caja que la emitió, la factura es suya: puede devolverla con lo que tiene en su base.
        Assert.True((await BuscarAsync(cliente, cajaUno, numero)).Propia);

        // Se devuelven 2 desde la caja 02 y el Central lo refleja para cualquiera que pregunte.
        Assert.Equal(EstadoRecepcion.Recibido, await EnviarAsync(cliente, cajaDos,
            Mensaje(TiposMensaje.NotaCreditoEmitida, numero, NotaCredito(numero, encf, cantidad: 2m), CentralEnPruebas.CajaDos)));

        var despues = await BuscarAsync(cliente, cajaUno, encf);
        Assert.Equal((2m, 3m), (Assert.Single(despues.Lineas).Devuelta, Assert.Single(despues.Lineas).Disponible));

        // También se busca por el documento del cliente, para cuando llega sin el ticket.
        using (var respuesta = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Get, "/api/facturas?buscar=40100755", cajaDos)))
        {
            respuesta.EnsureSuccessStatusCode();
            var encontradas = (await respuesta.Content.ReadFromJsonAsync<IReadOnlyList<ResumenFacturaParaCaja>>(OpcionesJson.Predeterminadas))!;
            Assert.Contains(encontradas, f => f.Numero == numero);
        }

        // Una que no existe es 404: la caja lo distingue de no haber podido preguntar.
        using (var inexistente = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Get, "/api/facturas/019990000001", cajaDos)))
            Assert.Equal(HttpStatusCode.NotFound, inexistente.StatusCode);

        // La caja 02 retiene 2 de las 3 que quedan mientras emite su nota.
        Assert.True((await ReservarAsync(cliente, cajaDos, numero, [(1, 2m)])).Exitosa);

        // Para la caja 01 esas 2 ya no están disponibles: no se devuelve dos veces la misma mercancía.
        Assert.Equal(1m, Assert.Single((await BuscarAsync(cliente, cajaUno, numero)).Lineas).Disponible);

        // Y no puede reservar más de lo que queda.
        var deMas = await ReservarAsync(cliente, cajaUno, numero, [(1, 2m)]);
        Assert.False(deMas.Exitosa);
        Assert.Contains("solo quedan 1", deMas.Mensaje);

        // La caja 02 suelta lo suyo y todo vuelve a estar disponible.
        using (var liberada = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Delete, $"/api/facturas/{numero}/reservas", cajaDos)))
            Assert.Equal(HttpStatusCode.NoContent, liberada.StatusCode);

        Assert.Equal(3m, Assert.Single((await BuscarAsync(cliente, cajaUno, numero)).Lineas).Disponible);
    }

    private static async Task<RespuestaReservaFactura> ReservarAsync(HttpClient cliente, string token, string numero,
        (int Linea, decimal Cantidad)[] lineas)
    {
        var solicitud = new SolicitudReservaFactura(lineas.Select(l => new LineaReservaFactura(l.Linea, l.Cantidad)).ToList());
        using var respuesta = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Post, $"/api/facturas/{numero}/reservas", token, solicitud));
        respuesta.EnsureSuccessStatusCode();
        return (await respuesta.Content.ReadFromJsonAsync<RespuestaReservaFactura>(OpcionesJson.Predeterminadas))!;
    }

    private static async Task<DatosFacturaParaCaja> BuscarAsync(HttpClient cliente, string token, string numeroOEncf)
    {
        using var respuesta = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Get, $"/api/facturas/{numeroOEncf}", token));
        respuesta.EnsureSuccessStatusCode();
        return (await respuesta.Content.ReadFromJsonAsync<DatosFacturaParaCaja>(OpcionesJson.Predeterminadas))!;
    }

    private static DocumentoVentaCobrada Factura(string numero, string encf, decimal cantidad)
    {
        const decimal Precio = 118m;
        var cobrada = DateTimeOffset.UtcNow.AddHours(-2);
        var total = cantidad * Precio;
        var baseImponible = decimal.Round(total / 1.18m, 2, MidpointRounding.AwayFromZero);

        return new DocumentoVentaCobrada(numero, 1, "C001", "Cajero Desarrollo", cobrada.AddMinutes(-5), cobrada, TipoComprobante.FacturaConsumo,
            new DocumentoClienteVenta(null, TipoDocumentoIdentidad.Rnc, "401007551", "Cliente de Devoluciones"), "DOP",
            [new DocumentoLineaVenta(1, "43138", "7891114119695", "Cincel de punta", TipoArticulo.Normal, "UND", 0, cantidad, Precio, total, 18m,
                ListaPrecio.Detalle, MotivoPrecio.PrecioDetalle, false, false, null, false, null, null, 0m, 0m, null, null, null, null, 0m, total, 0m)],
            new DatosTotalesVenta(baseImponible, total - baseImponible, total, 1, cantidad,
                [new DatosDesgloseImpuesto(18m, 1, baseImponible, total - baseImponible, total)]),
            null,
            [new DocumentoPagoVenta(1, "EFE", "Efectivo", TipoFormaPago.Efectivo, "DOP", total, null, total, null, null, null, null, false)],
            total, 0m, 0m,
            new DatosComprobanteElectronico(encf, TipoComprobante.FacturaConsumo, "ABC123", cobrada, "https://ecf.dgii.gov.do/consulta",
                EstadoDocumentoElectronico.PendienteSincronizar),
            null, []);
    }

    private static DocumentoNotaCreditoEmitida NotaCredito(string facturaNumero, string encfFactura, decimal cantidad)
    {
        const decimal Precio = 118m;
        var total = cantidad * Precio;
        var baseImponible = decimal.Round(total / 1.18m, 2, MidpointRounding.AwayFromZero);
        var numero = CentralEnPruebas.NumeroDocumento(CentralEnPruebas.CajaDos, TipoDocumentoNumerado.NotaCredito);

        return new DocumentoNotaCreditoEmitida(numero, facturaNumero, encfFactura, DateTimeOffset.UtcNow.AddHours(-1), null,
            TipoDocumentoIdentidad.Rnc, "401007551", "Cliente de Devoluciones", 1, "Devolución", null, "Cajero Prueba", null, false, false,
            baseImponible, total - baseImponible, 0m, total, "DOP", DateOnly.FromDateTime(DateTime.Today), DateTimeOffset.UtcNow,
            [new DatosLineaNotaCredito(1, "43138", "7891114119695", "Cincel de punta", "UND", 0, cantidad, Precio, 18m, baseImponible,
                total - baseImponible, 0m, total, null)],
            null, 0, TipoReembolso.SaldoNotaCredito, null, null, null);
    }

    private static MensajeSincronizacion Mensaje(string tipo, string referencia, object documento, int cajaId)
    {
        var (sucursal, caja) = CentralEnPruebas.CodigosCaja(cajaId);
        var contenido = JsonSerializer.Serialize(documento, OpcionesJson.Predeterminadas);
        return new MensajeSincronizacion(Guid.CreateVersion7(), tipo, referencia, contenido, HashSincronizacion.Calcular(contenido), sucursal, caja,
            DateTimeOffset.UtcNow);
    }

    private static async Task<EstadoRecepcion?> EnviarAsync(HttpClient cliente, string token, MensajeSincronizacion mensaje)
    {
        using var respuesta = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Post, "/api/sincronizacion/mensajes", token, mensaje));
        return (await respuesta.Content.ReadFromJsonAsync<RespuestaRecepcionCentral>(OpcionesJson.Predeterminadas))?.Estado;
    }
}
