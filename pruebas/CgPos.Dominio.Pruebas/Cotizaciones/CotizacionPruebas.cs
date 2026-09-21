using CgPos.Dominio.Cotizaciones;
using CgPos.Dominio.Ventas;

namespace CgPos.Dominio.Pruebas.Cotizaciones;

/// <summary>
/// La cotización es el presupuesto que se le da al cliente: precios congelados, vigencia y un solo uso. Lo que más importa
/// aquí es que su total coincida al centavo con el de la factura que salga de ella.
/// </summary>
public class CotizacionPruebas
{
    private static readonly DateTimeOffset Ahora = new(2026, 9, 20, 10, 0, 0, TimeSpan.FromHours(-4));
    private static readonly DateOnly Hoy = DateOnly.FromDateTime(Ahora.Date);

    private static Cotizacion Nueva(DateOnly? vence = null) =>
        Cotizacion.Crear("COT000001", "Ferretería del Este", "131234567", "809-555-0000", null, null,
            vence ?? Hoy.AddDays(15), "Entrega en obra", "Ana", Ahora);

    private static DatosLineaCotizacion Linea(string codigo, decimal cantidad, decimal precio, decimal descuento = 0m, decimal impuesto = 18m) =>
        new(codigo, $"Artículo {codigo}", "UND", cantidad, precio, descuento, impuesto, impuesto == 0m ? 4 : 1);

    [Fact]
    public void Los_totales_separan_el_itbis_igual_que_la_factura()
    {
        var cotizacion = Nueva();
        cotizacion.ReemplazarLineas([Linea("A1", 3, 118m), Linea("A2", 1, 59.90m), Linea("A3", 2, 100m, impuesto: 0m)], Ahora);

        var totales = cotizacion.Totales();

        // 354.00 + 59.90 gravados y 200.00 exento, todo sin ITBIS; el ITBIS (63.72 + 10.78) se suma aparte.
        Assert.Equal(613.90m, totales.Subtotal);
        Assert.Equal(74.50m, totales.Impuesto);
        Assert.Equal(688.40m, totales.Total);
        Assert.Equal(totales.Total, totales.Subtotal + totales.Impuesto);

        // La misma venta en la caja tiene que dar el mismo total, centavo por centavo.
        var venta = VentaCon(("A1", 3, 118m, 18m), ("A2", 1, 59.90m, 18m), ("A3", 2, 100m, 0m));
        var deLaCaja = venta.CalcularTotales();
        Assert.Equal((deLaCaja.Total, deLaCaja.Subtotal, deLaCaja.Impuesto), (totales.Total, totales.Subtotal, totales.Impuesto));
    }

    [Fact]
    public void El_descuento_de_la_linea_baja_el_importe_y_su_impuesto()
    {
        var cotizacion = Nueva();
        cotizacion.ReemplazarLineas([Linea("A1", 2, 118m, descuento: 36m)], Ahora);

        var totales = cotizacion.Totales();
        // 2 × 118 − 36 = 200 sin ITBIS; el ITBIS se calcula sobre lo que queda después del descuento.
        Assert.Equal(200m, totales.Subtotal);
        Assert.Equal(36m, totales.Descuento);
        Assert.Equal(36m, totales.Impuesto);
        Assert.Equal(236m, totales.Total);
    }

    [Fact]
    public void Las_lineas_se_reemplazan_completas_y_se_numeran_en_orden()
    {
        var cotizacion = Nueva();
        cotizacion.ReemplazarLineas([Linea("A1", 1, 100m), Linea("A2", 1, 200m)], Ahora);
        cotizacion.ReemplazarLineas([Linea("A3", 5, 50m)], Ahora);

        var linea = Assert.Single(cotizacion.Lineas);
        Assert.Equal(("A3", 1), (linea.ArticuloCodigo, linea.NumeroLinea));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Una_cantidad_que_no_es_positiva_se_rechaza(decimal cantidad) =>
        Assert.Throws<ArgumentException>(() => Nueva().ReemplazarLineas([Linea("A1", cantidad, 100m)], Ahora));

    [Fact]
    public void Un_descuento_mayor_que_la_linea_se_rechaza() =>
        Assert.Throws<ArgumentException>(() => Nueva().ReemplazarLineas([Linea("A1", 1, 100m, descuento: 150m)], Ahora));

    [Fact]
    public void El_mismo_articulo_dos_veces_se_rechaza() =>
        Assert.Throws<ArgumentException>(() => Nueva().ReemplazarLineas([Linea("A1", 1, 100m), Linea("a1", 2, 100m)], Ahora));

    [Fact]
    public void Sin_lineas_no_hay_cotizacion() =>
        Assert.Throws<ArgumentException>(() => Nueva().ReemplazarLineas([], Ahora));

    [Fact]
    public void Vence_el_dia_siguiente_al_ultimo_valido()
    {
        var cotizacion = Nueva(vence: Hoy.AddDays(2));

        Assert.False(cotizacion.EstaVencida(Hoy));
        Assert.False(cotizacion.EstaVencida(Hoy.AddDays(2)));
        Assert.True(cotizacion.EstaVencida(Hoy.AddDays(3)));
    }

    [Fact]
    public void Facturada_no_se_cambia_ni_se_factura_dos_veces()
    {
        var cotizacion = Nueva();
        cotizacion.ReemplazarLineas([Linea("A1", 1, 100m)], Ahora);

        Assert.True(cotizacion.RegistrarFactura("010110000007", Ahora, Ahora));
        Assert.Equal(EstadoCotizacion.Facturada, cotizacion.Estado);
        Assert.Equal("010110000007", cotizacion.VentaNumero);

        // El mismo mensaje de la caja puede llegar otra vez: no pasa nada.
        Assert.False(cotizacion.RegistrarFactura("010110000007", Ahora, Ahora));

        // Y ya no se le tocan las líneas ni los datos.
        Assert.Throws<ArgumentException>(() => cotizacion.ReemplazarLineas([Linea("A2", 1, 50m)], Ahora));
        Assert.Throws<ArgumentException>(() => cotizacion.Anular("Se equivocaron", Ahora));
        Assert.False(cotizacion.EstaVencida(Hoy.AddYears(1)));
    }

    [Fact]
    public void Anular_exige_motivo_y_la_deja_fuera_de_uso()
    {
        var cotizacion = Nueva();
        cotizacion.ReemplazarLineas([Linea("A1", 1, 100m)], Ahora);

        Assert.Throws<ArgumentException>(() => cotizacion.Anular("  ", Ahora));

        cotizacion.Anular("El cliente pidió otra configuración", Ahora);
        Assert.Equal(EstadoCotizacion.Anulada, cotizacion.Estado);
        Assert.Equal("El cliente pidió otra configuración", cotizacion.MotivoAnulacion);
        Assert.False(cotizacion.EsEditable);
    }

    /// <summary>Una venta de caja con esos artículos, para comparar totales con la cotización.</summary>
    private static Venta VentaCon(params (string Codigo, decimal Cantidad, decimal Precio, decimal Impuesto)[] articulos)
    {
        var venta = VentaEnProceso.Iniciar(Ids.Siguiente(), Ids.Siguiente(), Ids.Siguiente(), Ids.Siguiente(), "Cajero", "DOP", "RD$", Ahora);
        foreach (var (codigo, cantidad, precio, impuesto) in articulos)
        {
            venta.AgregarArticulo(
                new ArticuloParaVenta(Ids.Siguiente(), codigo, codigo, $"Artículo {codigo}", CgPos.Dominio.Catalogo.TipoArticulo.Normal, Ids.Siguiente(),
                    true, "UND", false, 0, Ids.Siguiente(), impuesto, impuesto == 0m ? 4 : 1, precio, null, null, null, null, null),
                cantidad, Ahora);
        }

        return venta;
    }
}
