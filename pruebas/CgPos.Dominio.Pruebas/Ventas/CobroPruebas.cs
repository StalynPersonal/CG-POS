using CgPos.Dominio.Catalogo;
using CgPos.Dominio.Fiscal;
using CgPos.Dominio.Pagos;
using CgPos.Dominio.Ventas;

namespace CgPos.Dominio.Pruebas.Ventas;

public class CobroPruebas
{
    private static readonly DateTimeOffset Ahora = new(2026, 9, 15, 14, 0, 0, TimeSpan.FromHours(-4));
    private const decimal SinTope = 250_000m;

    private static readonly FormaPagoParaCobro Efectivo = new(Guid.CreateVersion7(), "EFE", "Efectivo", TipoFormaPago.Efectivo, "DOP", true, false, false, true, true);
    private static readonly FormaPagoParaCobro Dolares = new(Guid.CreateVersion7(), "USD", "Dólares", TipoFormaPago.MonedaExtranjera, "USD", true, false, false, true, true);
    private static readonly FormaPagoParaCobro Tarjeta = new(Guid.CreateVersion7(), "TAR", "Tarjeta", TipoFormaPago.Tarjeta, "DOP", false, true, false, true, false);
    private static readonly FormaPagoParaCobro Transferencia = new(Guid.CreateVersion7(), "TRA", "Transferencia", TipoFormaPago.Transferencia, "DOP", false, true, true, true, true);
    private static readonly FormaPagoParaCobro Bono = new(Guid.CreateVersion7(), "BON", "Bono de regalo", TipoFormaPago.BonoRegalo, "DOP", false, true, false, false, false);

    private static readonly ArticuloParaVenta Cincel = new(
        Guid.CreateVersion7(), "43138", "7891114119695", "Cincel de punta", TipoArticulo.Normal, Guid.CreateVersion7(), true,
        "UND", false, 0, Guid.CreateVersion7(), 18m, 1, 850.37m, null, null, null, null, null);

    private static Venta VentaCon(int cinceles = 1)
    {
        var venta = Venta.Iniciar(Guid.CreateVersion7(), 1, Guid.CreateVersion7(), 1, Guid.CreateVersion7(), 1, 7, Guid.CreateVersion7(), "Cajera", "DOP", "RD$", Ahora);
        venta.AgregarArticulo(Cincel, cinceles, Ahora);
        return venta;
    }

    private static ResultadoCobro Cobrar(Venta venta, decimal paso, params PagoSolicitado[] pagos) =>
        venta.Cobrar(pagos, paso, SinTope, Guid.CreateVersion7(), "Cajera", Ahora);

    [Fact]
    public void Efectivo_da_devuelta_y_la_venta_queda_cobrada()
    {
        var venta = VentaCon();

        var resultado = Cobrar(venta, 0m, new PagoSolicitado(Efectivo, 1000m));

        Assert.Equal(850.37m, resultado.Total);
        Assert.Equal(149.63m, resultado.Devuelta);
        Assert.True(resultado.AbreGaveta);
        Assert.Equal(EstadoVenta.Cobrada, venta.Estado);
        Assert.Equal(1000m, venta.Pagos.Single().MontoAplicado);
        Assert.Equal(CodigoErrorVenta.VentaNoEditable, Assert.Throws<ReglaVentaExcepcion>(() => venta.AgregarArticulo(Cincel, null, Ahora)).Codigo);
    }

    [Fact]
    public void Pago_mixto_cierra_el_saldo_y_la_tarjeta_sola_no_abre_gaveta()
    {
        var venta = VentaCon(2); // 1,700.74

        var resultado = Cobrar(venta, 0m,
            new PagoSolicitado(Tarjeta, 1000m, Referencia: "123456", UltimosDigitos: "4242"),
            new PagoSolicitado(Efectivo, 800m));

        Assert.Equal(99.26m, resultado.Devuelta);
        Assert.Equal(2, venta.Pagos.Count);
        Assert.Equal("123456", venta.Pagos.First().Referencia);

        var soloTarjeta = VentaCon();
        Assert.False(Cobrar(soloTarjeta, 0m, new PagoSolicitado(Tarjeta, 850.37m, Referencia: "999")).AbreGaveta);
    }

    [Fact]
    public void La_devuelta_solo_sale_del_efectivo()
    {
        var venta = VentaCon();

        var error = Assert.Throws<ReglaVentaExcepcion>(() => Cobrar(venta, 0m, new PagoSolicitado(Tarjeta, 900m, Referencia: "111")));
        Assert.Equal(CodigoErrorVenta.DevueltaNoPermitida, error.Codigo);

        // Con efectivo, la devuelta no puede superar lo entregado en efectivo (59.63 de devuelta con solo 10 en efectivo).
        Assert.Equal(CodigoErrorVenta.DevueltaNoPermitida, Assert.Throws<ReglaVentaExcepcion>(() =>
            Cobrar(venta, 0m, new PagoSolicitado(Tarjeta, 900m, Referencia: "111"), new PagoSolicitado(Efectivo, 10m))).Codigo);
        Assert.Equal(EstadoVenta.EnCurso, venta.Estado);
    }

    [Fact]
    public void Dolares_se_convierten_a_la_tasa_y_la_devuelta_es_en_pesos()
    {
        var venta = VentaCon();

        var resultado = Cobrar(venta, 0m, new PagoSolicitado(Dolares, 20m, TasaCambio: 60.25m));

        var pago = venta.Pagos.Single();
        Assert.Equal(1205m, pago.MontoAplicado);
        Assert.Equal(60.25m, pago.TasaCambio);
        Assert.Equal(354.63m, resultado.Devuelta);

        Assert.Equal(CodigoErrorVenta.PagoInvalido, Assert.Throws<ReglaVentaExcepcion>(() =>
            Cobrar(VentaCon(), 0m, new PagoSolicitado(Dolares, 20m))).Codigo);
    }

    [Fact]
    public void Referencias_banco_y_monto_se_validan_por_forma_de_pago()
    {
        Assert.Equal(CodigoErrorVenta.PagoInvalido, Assert.Throws<ReglaVentaExcepcion>(() => Cobrar(VentaCon(), 0m, new PagoSolicitado(Tarjeta, 850.37m))).Codigo);
        Assert.Equal(CodigoErrorVenta.PagoInvalido, Assert.Throws<ReglaVentaExcepcion>(() =>
            Cobrar(VentaCon(), 0m, new PagoSolicitado(Transferencia, 850.37m, Referencia: "TRF-1"))).Codigo); // falta el banco
        Assert.Equal(CodigoErrorVenta.PagoInsuficiente, Assert.Throws<ReglaVentaExcepcion>(() => Cobrar(VentaCon(), 0m, new PagoSolicitado(Efectivo, 800m))).Codigo);
        Assert.Equal(CodigoErrorVenta.PagoInvalido, Assert.Throws<ReglaVentaExcepcion>(() => Cobrar(VentaCon(), 0m)).Codigo);
    }

    [Fact]
    public void Bonos_no_se_aceptan_con_credito_fiscal()
    {
        var venta = VentaCon();
        venta.AsignarCliente(new ClienteVenta(null, TipoDocumentoIdentidad.Rnc, "131246796", "Constructora", TipoComprobante.FacturaCreditoFiscal), Ahora);

        Assert.Equal(CodigoErrorVenta.ComprobanteNoPermitido, Assert.Throws<ReglaVentaExcepcion>(() =>
            Cobrar(venta, 0m, new PagoSolicitado(Bono, 850.37m, Referencia: "BONO-1"))).Codigo);

        var consumo = VentaCon();
        Assert.Equal(0m, Cobrar(consumo, 0m, new PagoSolicitado(Bono, 850.37m, Referencia: "BONO-1")).Devuelta);
    }

    [Fact]
    public void El_efectivo_se_redondea_al_paso_configurado_y_sin_efectivo_no()
    {
        var conEfectivo = VentaCon(); // 850.37
        var redondeado = Cobrar(conEfectivo, 1m, new PagoSolicitado(Efectivo, 850m));

        Assert.Equal(850m, redondeado.TotalCobrado);
        Assert.Equal(-0.37m, redondeado.Redondeo);
        Assert.Equal(0m, redondeado.Devuelta);
        Assert.Equal(-0.37m, conEfectivo.RedondeoEfectivo);

        var conTarjeta = VentaCon();
        Assert.Equal(850.37m, Cobrar(conTarjeta, 1m, new PagoSolicitado(Tarjeta, 850.37m, Referencia: "1")).TotalCobrado);
    }

    [Fact]
    public void Factura_de_consumo_grande_sin_identificacion_no_se_cobra()
    {
        var venta = VentaCon(300); // 255,111

        var error = Assert.Throws<ReglaVentaExcepcion>(() => venta.Cobrar([new PagoSolicitado(Efectivo, 300_000m)], 0m, 250_000m, Guid.CreateVersion7(), "Cajera", Ahora));
        Assert.Equal(CodigoErrorVenta.DocumentoRequerido, error.Codigo);
    }
}
