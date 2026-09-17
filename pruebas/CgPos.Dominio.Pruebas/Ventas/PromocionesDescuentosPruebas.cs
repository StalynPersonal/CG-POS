using CgPos.Dominio.Catalogo;
using CgPos.Dominio.Promociones;
using CgPos.Dominio.Ventas;

namespace CgPos.Dominio.Pruebas.Ventas;

public class PromocionesDescuentosPruebas
{
    /// <summary>Martes 15 de septiembre de 2026, 10:00 hora de RD.</summary>
    private static readonly DateTimeOffset Martes10 = new(2026, 9, 15, 10, 0, 0, TimeSpan.FromHours(-4));

    private static readonly Guid Sucursal = Guid.CreateVersion7();
    private static readonly Guid DepartamentoFerreteria = Guid.CreateVersion7();
    private static readonly Guid DepartamentoPanaderia = Guid.CreateVersion7();

    private static readonly ArticuloParaVenta Cincel = new(
        Guid.CreateVersion7(), "43138", "7891114119695", "Cincel de punta", TipoArticulo.Normal, DepartamentoFerreteria, true,
        "UND", false, 0, Guid.CreateVersion7(), 18m, 1, 850m, null, null, null, null, null);

    private static readonly ArticuloParaVenta Cemento = new(
        Guid.CreateVersion7(), "CEM-425", "7460001000017", "Cemento gris", TipoArticulo.Normal, DepartamentoFerreteria, true,
        "UND", false, 0, Guid.CreateVersion7(), 18m, 1, 485m, 450m, 12m, null, null, null);

    private static readonly ArticuloParaVenta Martillo = new(
        Guid.CreateVersion7(), "MAR-16", "7460001000093", "Martillo 16 oz", TipoArticulo.Normal, DepartamentoFerreteria, true,
        "UND", false, 0, Guid.CreateVersion7(), 18m, 1, 600m, null, null, null, null, null);

    /// <summary>Panadería: no admite descuento manual, solo ofertas (RN-09).</summary>
    private static readonly ArticuloParaVenta Pan = new(
        Guid.CreateVersion7(), "PAN-AGUA", "PAN-AGUA", "Pan de agua", TipoArticulo.Normal, DepartamentoPanaderia, false,
        "UND", false, 0, Guid.CreateVersion7(), 0m, 4, 10m, null, null, null, null, null);

    private static Venta NuevaVenta() =>
        Venta.Iniciar(Sucursal, "01", Guid.CreateVersion7(), "01", Guid.CreateVersion7(), 1, 7, Guid.CreateVersion7(), "Cajera", "DOP", "RD$", Martes10);

    private static Promocion Oferta(string codigo, TipoPromocion tipo, decimal valor, ArticuloParaVenta articulo)
    {
        var promocion = Promocion.Crear(codigo, $"Oferta {codigo}", tipo, valor, Martes10.AddDays(-1), Martes10.AddDays(7));
        promocion.AsignarAlcance([articulo.ArticuloId], null, null);
        return promocion;
    }

    private static void Recalcular(Venta venta, params Promocion[] promociones) => venta.RecalcularPromociones(promociones, Sucursal, Martes10);

    [Fact]
    public void Oferta_por_porcentaje_descuenta_sobre_el_detalle_y_cuadra_los_totales()
    {
        var venta = NuevaVenta();
        var linea = venta.AgregarArticulo(Cincel, null, Martes10);

        Recalcular(venta, Oferta("OFE15", TipoPromocion.Porcentaje, 15m, Cincel));

        Assert.Equal("OFE15", linea.PromocionCodigo);
        Assert.Equal(127.50m, linea.DescuentoPromocion);
        Assert.Equal(722.50m, linea.ImporteConImpuesto);

        var totales = venta.CalcularTotales();
        Assert.Equal(722.50m, totales.Total);
        Assert.Equal(127.50m, totales.Descuento);
        Assert.Equal(totales.Total, totales.Subtotal + totales.Impuesto);
    }

    [Fact]
    public void Dos_por_uno_suma_las_unidades_de_varias_lecturas_del_mismo_articulo()
    {
        var venta = NuevaVenta();
        for (var i = 0; i < 3; i++)
            venta.AgregarArticulo(Pan, null, Martes10);

        var dosPorUno = Oferta("PAN2X1", TipoPromocion.LlevaPaga, 0m, Pan);
        dosPorUno.ConfigurarCantidades(2, 1, null, null);
        Recalcular(venta, dosPorUno);

        Assert.Equal(20m, venta.CalcularTotales().Total);
        Assert.Equal(10m, venta.Lineas.Sum(l => l.DescuentoPromocion));
        Assert.Equal("2x1", dosPorUno.DescripcionCorta);
    }

    [Fact]
    public void Si_hay_varias_ofertas_gana_la_mas_favorable_para_el_cliente()
    {
        var venta = NuevaVenta();
        var linea = venta.AgregarArticulo(Cincel, null, Martes10);

        Recalcular(venta, Oferta("ESP799", TipoPromocion.PrecioEspecial, 799m, Cincel), Oferta("OFE15", TipoPromocion.Porcentaje, 15m, Cincel));

        Assert.Equal("OFE15", linea.PromocionCodigo);
        Assert.Equal(127.50m, linea.DescuentoPromocion);
    }

    [Fact]
    public void La_oferta_solo_reemplaza_el_precio_por_mayor_cuando_deja_mejor_precio()
    {
        var venta = NuevaVenta();
        var linea = venta.AgregarArticulo(Cemento, 12m, Martes10); // mayor: 12 × 450 = 5,400

        Recalcular(venta, Oferta("CEM5", TipoPromocion.Porcentaje, 5m, Cemento)); // 12 × 485 − 5% = 5,529
        Assert.Null(linea.PromocionCodigo);
        Assert.Equal(ListaPrecio.Mayor, linea.Lista);
        Assert.Equal(5400m, venta.CalcularTotales().Total);

        Recalcular(venta, Oferta("CEM10", TipoPromocion.Porcentaje, 10m, Cemento)); // 5,820 − 582 = 5,238
        Assert.Equal("CEM10", linea.PromocionCodigo);
        Assert.Equal(ListaPrecio.Detalle, linea.Lista);
        Assert.Equal(5238m, venta.CalcularTotales().Total);

        // Sin ofertas vigentes vuelve al precio por mayor.
        Recalcular(venta);
        Assert.Equal(ListaPrecio.Mayor, linea.Lista);
        Assert.Equal(0m, linea.DescuentoPromocion);
    }

    [Fact]
    public void Vigencia_respeta_dias_horas_fechas_y_sucursal()
    {
        var martesManana = Oferta("MAR", TipoPromocion.Porcentaje, 10m, Cincel);
        martesManana.Programar(DiasSemana.Martes, new TimeOnly(8, 0), new TimeOnly(12, 0));

        Assert.True(martesManana.EstaVigente(Sucursal, Martes10));
        Assert.False(martesManana.EstaVigente(Sucursal, Martes10.AddHours(3)));   // 13:00
        Assert.False(martesManana.EstaVigente(Sucursal, Martes10.AddDays(1)));    // miércoles
        Assert.False(martesManana.EstaVigente(Sucursal, Martes10.AddDays(14)));   // fuera de fechas

        martesManana.AsignarAlcance([Cincel.ArticuloId], null, [Guid.CreateVersion7()]);
        Assert.False(martesManana.EstaVigente(Sucursal, Martes10));               // otra sucursal

        var nocturna = Oferta("NOC", TipoPromocion.Porcentaje, 10m, Cincel);
        nocturna.Programar(DiasSemana.Todos, new TimeOnly(22, 0), new TimeOnly(2, 0));
        Assert.True(nocturna.EstaVigente(Sucursal, Martes10.AddHours(13)));       // 23:00
        Assert.False(nocturna.EstaVigente(Sucursal, Martes10));
    }

    [Fact]
    public void Limite_por_cliente_y_precio_por_cantidad()
    {
        var limitada = Oferta("LIM", TipoPromocion.Porcentaje, 15m, Cincel);
        limitada.ConfigurarCantidades(null, null, null, 2m);
        Assert.Equal(255m, MotorPromociones.CalcularDescuento(limitada, 5m, 850m, 4250m)); // solo 2 de 5 unidades

        var porCantidad = Oferta("CANT", TipoPromocion.PrecioPorCantidad, 500m, Martillo);
        porCantidad.ConfigurarCantidades(null, null, 10m, null);
        Assert.Equal(0m, MotorPromociones.CalcularDescuento(porCantidad, 9m, 600m, 5400m));
        Assert.Equal(1000m, MotorPromociones.CalcularDescuento(porCantidad, 10m, 600m, 6000m));
    }

    [Fact]
    public void Prorrateo_reparte_al_centavo_sin_perder_ni_sobrar()
    {
        var partes = MotorPromociones.Prorratear(100m, [850m, 485m, 333.33m]);

        Assert.Equal(100m, partes.Sum());
        Assert.All(partes, parte => Assert.Equal(decimal.Round(parte, 2), parte));
    }

    [Fact]
    public void Descuento_manual_respeta_departamentos_sin_descuento_y_articulos_en_oferta()
    {
        var venta = NuevaVenta();
        var pan = venta.AgregarArticulo(Pan, null, Martes10);
        var cincel = venta.AgregarArticulo(Cincel, null, Martes10);
        Recalcular(venta, Oferta("OFE15", TipoPromocion.Porcentaje, 15m, Cincel));

        Assert.Equal(CodigoErrorVenta.DescuentoNoPermitido, Assert.Throws<ReglaVentaExcepcion>(() =>
            venta.AplicarDescuentoLinea(pan.NumeroLinea, TipoDescuento.Porcentaje, 10m, "Cliente frecuente", null, null, Martes10)).Codigo);
        Assert.Equal(CodigoErrorVenta.ArticuloEnOferta, Assert.Throws<ReglaVentaExcepcion>(() =>
            venta.AplicarDescuentoLinea(cincel.NumeroLinea, TipoDescuento.Porcentaje, 10m, "Cliente frecuente", null, null, Martes10)).Codigo);

        // Con la oferta desactivada (con permiso, RF-124) sí admite el descuento manual.
        venta.DesactivarPromocion(cincel.NumeroLinea, Martes10);
        Recalcular(venta, Oferta("OFE15", TipoPromocion.Porcentaje, 15m, Cincel));
        Assert.Null(cincel.PromocionCodigo);
        Assert.True(cincel.PromocionDesactivada);

        Assert.Equal(CodigoErrorVenta.MotivoRequerido, Assert.Throws<ReglaVentaExcepcion>(() =>
            venta.AplicarDescuentoLinea(cincel.NumeroLinea, TipoDescuento.Porcentaje, 10m, " ", null, null, Martes10)).Codigo);
        Assert.Equal(CodigoErrorVenta.DescuentoInvalido, Assert.Throws<ReglaVentaExcepcion>(() =>
            venta.AplicarDescuentoLinea(cincel.NumeroLinea, TipoDescuento.Monto, 900m, "Ajuste", null, null, Martes10)).Codigo);

        var vista = venta.PrevisualizarDescuentoLinea(cincel.NumeroLinea, TipoDescuento.Monto, 85m);
        Assert.Equal(85m, vista.Monto);
        Assert.Equal(10m, vista.Porcentaje);

        venta.AplicarDescuentoLinea(cincel.NumeroLinea, TipoDescuento.Porcentaje, 10m, "Cliente frecuente", Guid.CreateVersion7(), "Supervisor", Martes10);
        Assert.Equal(85m, cincel.DescuentoManual);
        Assert.Equal(765m, cincel.ImporteConImpuesto);
        Assert.Equal("Cliente frecuente", cincel.MotivoDescuento);

        venta.QuitarDescuentoLinea(cincel.NumeroLinea, Martes10);
        Assert.Equal(850m, cincel.ImporteConImpuesto);
    }

    [Fact]
    public void Descuento_a_la_factura_por_monto_se_prorratea_exacto_y_excluye_ofertas_y_departamentos_sin_descuento()
    {
        var venta = NuevaVenta();
        var cincel = venta.AgregarArticulo(Cincel, null, Martes10);
        var cemento = venta.AgregarArticulo(Cemento, null, Martes10);
        var martillo = venta.AgregarArticulo(Martillo, null, Martes10);
        var pan = venta.AgregarArticulo(Pan, null, Martes10);
        Recalcular(venta, Oferta("MAR15", TipoPromocion.Porcentaje, 15m, Martillo));

        var resultado = venta.AplicarDescuentoFactura(TipoDescuento.Monto, 100m, null, "Cliente frecuente", Guid.CreateVersion7(), "Supervisor", Martes10);

        Assert.Equal(100m, resultado.Monto);
        Assert.Equal([martillo.NumeroLinea, pan.NumeroLinea], resultado.LineasExcluidas);
        Assert.Equal(100m, cincel.DescuentoFactura + cemento.DescuentoFactura);
        Assert.Equal(0m, martillo.DescuentoFactura);
        Assert.Equal(0m, pan.DescuentoFactura);
        Assert.Equal(63.67m, cincel.DescuentoFactura); // 850 / 1,335 × 100

        var totales = venta.CalcularTotales();
        Assert.Equal(190m, totales.Descuento); // 90 de la oferta + 100 de la factura
        Assert.Equal(totales.Total, totales.Subtotal + totales.Impuesto);
    }

    [Fact]
    public void Descuento_a_la_factura_puede_limitarse_a_lineas_y_se_reprorratea_al_cambiar_la_venta()
    {
        var venta = NuevaVenta();
        var cincel = venta.AgregarArticulo(Cincel, null, Martes10);
        var cemento = venta.AgregarArticulo(Cemento, null, Martes10);
        var martillo = venta.AgregarArticulo(Martillo, null, Martes10);

        venta.AplicarDescuentoFactura(TipoDescuento.Porcentaje, 10m, [cincel.NumeroLinea, martillo.NumeroLinea], "Ajuste", null, null, Martes10);
        Assert.Equal(85m, cincel.DescuentoFactura);
        Assert.Equal(0m, cemento.DescuentoFactura);
        Assert.Equal(60m, martillo.DescuentoFactura);

        venta.QuitarDescuentoFactura(Martes10);
        venta.AplicarDescuentoFactura(TipoDescuento.Monto, 145m, null, "Ajuste", null, null, Martes10);
        Assert.Equal(145m, venta.Lineas.Sum(l => l.DescuentoFactura));

        venta.EliminarLinea(martillo.NumeroLinea, Martes10);
        Assert.Equal(145m, cincel.DescuentoFactura + cemento.DescuentoFactura);
        Assert.Equal(1190m, venta.CalcularTotales().Total); // 850 + 485 − 145
    }
}
