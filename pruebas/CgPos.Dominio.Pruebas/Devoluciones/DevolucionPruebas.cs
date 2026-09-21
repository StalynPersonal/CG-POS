using CgPos.Dominio.Catalogo;
using CgPos.Dominio.Devoluciones;
using CgPos.Dominio.Fiscal;
using CgPos.Dominio.Pagos;
using CgPos.Dominio.Ventas;

namespace CgPos.Dominio.Pruebas.Devoluciones;

public class DevolucionPruebas
{
    private static readonly DateTimeOffset Cobro = new(2026, 9, 1, 10, 0, 0, TimeSpan.FromHours(-4));
    private static readonly DateOnly DiaCobro = new(2026, 9, 1);
    private static readonly TimeZoneInfo HoraCaja = TimeZoneInfo.CreateCustomTimeZone("Caja de prueba", TimeSpan.FromHours(-4), "Caja de prueba", "Caja de prueba");
    private static readonly ClienteDevolucion Cliente = new(TipoDocumentoIdentidad.Rnc, "401007551", "Cliente de prueba");
    private static readonly FormaPagoParaCobro Efectivo = new(Ids.Siguiente(), "EFE", "Efectivo", TipoFormaPago.Efectivo, "DOP", true, false, false, true, true);

    private static readonly ArticuloParaVenta Cincel = new(
        Ids.Siguiente(), "43138", "7891114119695", "Cincel de punta", TipoArticulo.Normal, Ids.Siguiente(), true,
        "UND", false, 0, Ids.Siguiente(), 18m, 1, 720.34m, null, null, null, null, null); // 720.34 + 129.66 de ITBIS = 850

    private static Venta VentaCobrada(decimal cinceles)
    {
        var venta = CgPos.Dominio.Ventas.VentaCobrada.DesdeBorrador(VentaEnProceso.Iniciar(Ids.Siguiente(), Ids.Siguiente(), Ids.Siguiente(), Ids.Siguiente(), "Cajera", "DOP", "RD$", Cobro));
        venta.AgregarArticulo(Cincel, cinceles, Cobro);
        venta.Cobrar([new PagoSolicitado(Efectivo, 10_000m)], 0m, 250_000m, Ids.Siguiente(), "Cajera", Cobro);
        return venta;
    }

    private static Devolucion Devolver(Venta venta, decimal cantidad, IReadOnlyDictionary<int, DevueltoLinea>? devuelto = null, DateOnly? hoy = null,
        string? serial = null) =>
        Devolucion.Registrar(FacturaParaDevolver.De(venta), venta.SucursalId, venta.CajaId, "E320000000001", [new LineaSolicitadaDevolucion(1, cantidad, serial)], devuelto ?? new Dictionary<int, DevueltoLinea>(),
            Cliente, 1, "Artículo defectuoso", null, "NC-01-00000001", null, Ids.Siguiente(), "Cajera", Ids.Siguiente(), "Encargado",
            diasRetencionImpuesto: 30, hoy ?? DiaCobro.AddDays(3), Cobro.AddDays(3), HoraCaja);

    [Fact]
    public void Devolucion_parcial_acredita_la_parte_proporcional_y_la_ultima_toma_el_resto_exacto()
    {
        var venta = VentaCobrada(3);

        var primera = Devolver(venta, 1);
        Assert.Equal(850m, primera.Total);
        Assert.Equal(720.34m, primera.Subtotal);
        Assert.Equal(129.66m, primera.Impuesto);
        Assert.False(primera.EsTotal);
        Assert.Equal(DiaCobro.AddDays(3), primera.FechaEmision);
        Assert.Equal(DiaCobro.AddDays(3 + 180), primera.VenceEn(180));

        var devuelto = new Dictionary<int, DevueltoLinea> { [1] = new(1m, 850m) };
        var segunda = Devolver(venta, 2, devuelto);
        Assert.Equal(1700m, segunda.Total);
        Assert.True(segunda.EsTotal);

        var todo = new Dictionary<int, DevueltoLinea> { [1] = new(3m, 2550m) };
        Assert.Equal(CodigoErrorDevolucion.CantidadExcedida, Assert.Throws<ReglaDevolucionExcepcion>(() => Devolver(venta, 1, todo)).Codigo);
    }

    [Fact]
    public void Fuera_de_plazo_se_retiene_el_itbis_y_la_nota_acredita_solo_la_base()
    {
        var venta = VentaCobrada(1);

        var nota = Devolver(venta, 1, hoy: DiaCobro.AddDays(31));

        Assert.True(nota.RetieneImpuesto);
        Assert.Equal(720.34m, nota.Total);
        Assert.Equal(129.66m, nota.ImpuestoRetenido);
        Assert.Equal(0m, nota.Impuesto);
        Assert.False(Devolver(venta, 1, hoy: DiaCobro.AddDays(30)).RetieneImpuesto);
    }

    [Fact]
    public void Sin_cliente_valido_o_sin_motivo_no_se_devuelve()
    {
        var venta = VentaCobrada(1);

        var sinCliente = Assert.Throws<ReglaDevolucionExcepcion>(() => Devolucion.Registrar(FacturaParaDevolver.De(venta), venta.SucursalId, venta.CajaId, null, [new LineaSolicitadaDevolucion(1, 1)],
            new Dictionary<int, DevueltoLinea>(), new ClienteDevolucion(null, "123", "X"), 1, "Defecto", null, "NC-1", null, Ids.Siguiente(), "Cajera",
            null, null, 30, DiaCobro, Cobro, HoraCaja));
        Assert.Equal(CodigoErrorDevolucion.ClienteRequerido, sinCliente.Codigo);

        var sinMotivo = Assert.Throws<ReglaDevolucionExcepcion>(() => Devolucion.Registrar(FacturaParaDevolver.De(venta), venta.SucursalId, venta.CajaId, null, [new LineaSolicitadaDevolucion(1, 1)],
            new Dictionary<int, DevueltoLinea>(), Cliente, null, null, null, "NC-1", null, Ids.Siguiente(), "Cajera", null, null, 30, DiaCobro, Cobro, HoraCaja));
        Assert.Equal(CodigoErrorDevolucion.MotivoRequerido, sinMotivo.Codigo);
    }

    [Fact]
    public void El_saldo_se_consume_por_partes_hasta_agotarse_y_vence()
    {
        const int dias = 60;
        var nota = Devolver(VentaCobrada(1), 1);

        Assert.Equal(350m, nota.Consumir(Ids.Siguiente(), "010110000009", Ids.Siguiente(), 500m, DiaCobro.AddDays(5), dias, Cobro.AddDays(5)));
        Assert.Equal(EstadoNotaCredito.Vigente, nota.EstadoSaldo(DiaCobro.AddDays(5), dias));
        Assert.Equal(CodigoErrorDevolucion.SaldoInsuficiente,
            Assert.Throws<ReglaDevolucionExcepcion>(() => nota.Consumir(Ids.Siguiente(), "X", Ids.Siguiente(), 351m, DiaCobro.AddDays(5), dias, Cobro)).Codigo);

        // Vence a los días configurados desde su emisión; si el negocio sube los días, vuelve a estar vigente.
        var vencida = nota.VenceEn(dias).AddDays(1);
        Assert.Equal(EstadoNotaCredito.Vencida, nota.EstadoSaldo(vencida, dias));
        Assert.Equal(CodigoErrorDevolucion.NotaCreditoVencida,
            Assert.Throws<ReglaDevolucionExcepcion>(() => nota.Consumir(Ids.Siguiente(), "X", Ids.Siguiente(), 10m, vencida, dias, Cobro)).Codigo);
        Assert.Equal(EstadoNotaCredito.Vigente, nota.EstadoSaldo(vencida, dias + 30));

        Assert.Equal(0m, nota.Consumir(Ids.Siguiente(), "010110000010", Ids.Siguiente(), 350m, DiaCobro.AddDays(6), dias, Cobro.AddDays(6)));
        Assert.Equal(EstadoNotaCredito.Consumida, nota.EstadoSaldo(DiaCobro.AddDays(6), dias));
        Assert.Equal(2, nota.Consumos.Count);
    }
}
