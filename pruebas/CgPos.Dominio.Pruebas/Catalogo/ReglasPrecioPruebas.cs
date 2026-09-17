using CgPos.Dominio.Catalogo;

namespace CgPos.Dominio.Pruebas.Catalogo;

public class ReglasPrecioPruebas
{
    private static readonly DateTimeOffset Ahora = new(2026, 9, 15, 12, 0, 0, TimeSpan.Zero);
    private static readonly Impuesto Itbis18 = Ids.Asignar(Impuesto.Crear("ITBIS18", "ITBIS 18%", 18m, 1));

    private static Articulo CrearArticulo(TipoArticulo tipo = TipoArticulo.Normal, decimal? cantidadMinimaMayor = 12m, decimal? costo = null, decimal? precioMinimo = null)
    {
        var articulo = Articulo.Crear("CEM-425", "Cemento gris 42.5 kg", Ids.Siguiente(), Ids.Siguiente(), Itbis18.Id, tipo);
        articulo.ConfigurarPrecios(costo, precioMinimo, cantidadMinimaMayor);
        return articulo;
    }

    private static readonly PreciosVigentes DetalleYMayor = new(485.00m, 450.00m);

    [Fact]
    public void Por_debajo_de_la_cantidad_minima_usa_precio_detalle()
    {
        var resultado = ReglasPrecio.Determinar(CrearArticulo(), DetalleYMayor, 11m, SeleccionListaPrecio.Automatica);

        Assert.Equal(ListaPrecio.Detalle, resultado.Lista);
        Assert.Equal(485.00m, resultado.PrecioUnitario);
        Assert.False(resultado.RequiereAutorizacion);
    }

    [Fact]
    public void Al_alcanzar_la_cantidad_minima_cambia_solo_a_mayor_sin_autorizacion()
    {
        var resultado = ReglasPrecio.Determinar(CrearArticulo(), DetalleYMayor, 12m, SeleccionListaPrecio.Automatica);

        Assert.Equal(ListaPrecio.Mayor, resultado.Lista);
        Assert.Equal(450.00m, resultado.PrecioUnitario);
        Assert.Equal(MotivoPrecio.MayorPorCantidad, resultado.Motivo);
        Assert.False(resultado.RequiereAutorizacion);
    }

    [Fact]
    public void Aplicar_mayor_manualmente_requiere_autorizacion()
    {
        var resultado = ReglasPrecio.Determinar(CrearArticulo(), DetalleYMayor, 1m, SeleccionListaPrecio.Mayor);

        Assert.Equal(ListaPrecio.Mayor, resultado.Lista);
        Assert.Equal(MotivoPrecio.MayorManual, resultado.Motivo);
        Assert.True(resultado.RequiereAutorizacion);
    }

    [Theory]
    [InlineData(SeleccionListaPrecio.Automatica, MotivoPrecio.PrecioDetalle)]
    [InlineData(SeleccionListaPrecio.Mayor, MotivoPrecio.MayorNoAplicaComboKit)]
    public void Combos_y_kits_nunca_usan_precio_por_mayor(SeleccionListaPrecio seleccion, MotivoPrecio motivoEsperado)
    {
        var resultado = ReglasPrecio.Determinar(CrearArticulo(TipoArticulo.ComboKit), DetalleYMayor, 100m, seleccion);

        Assert.Equal(ListaPrecio.Detalle, resultado.Lista);
        Assert.Equal(motivoEsperado, resultado.Motivo);
    }

    [Fact]
    public void Sin_precio_mayor_se_queda_en_detalle()
    {
        var resultado = ReglasPrecio.Determinar(CrearArticulo(), new PreciosVigentes(485.00m, null), 50m, SeleccionListaPrecio.Mayor);

        Assert.Equal(ListaPrecio.Detalle, resultado.Lista);
        Assert.Equal(MotivoPrecio.SinPrecioMayor, resultado.Motivo);
    }

    [Fact]
    public void Sin_precio_detalle_vigente_no_se_puede_vender()
    {
        Assert.Throws<InvalidOperationException>(() =>
            ReglasPrecio.Determinar(CrearArticulo(), new PreciosVigentes(null, 450m), 1m, SeleccionListaPrecio.Automatica));
    }

    [Fact]
    public void Precio_vigente_es_el_mas_reciente_ya_iniciado_y_los_futuros_no_aplican_todavia()
    {
        var articuloId = Ids.Siguiente();
        var historial = new[]
        {
            PrecioArticulo.Registrar(articuloId, ListaPrecio.Detalle, 450m, Ahora.AddDays(-30), Ahora.AddDays(-30), "SAP B1"),
            PrecioArticulo.Registrar(articuloId, ListaPrecio.Detalle, 485m, Ahora.AddDays(-1), Ahora.AddDays(-2), "SAP B1"),
            PrecioArticulo.Registrar(articuloId, ListaPrecio.Detalle, 500m, Ahora.AddDays(3), Ahora.AddDays(-1), "SAP B1"),
            PrecioArticulo.Registrar(articuloId, ListaPrecio.Mayor, 440m, Ahora.AddDays(-10), Ahora.AddDays(-10), "SAP B1"),
        };

        var hoy = PreciosVigentes.Resolver(historial, Ahora);
        var enCuatroDias = PreciosVigentes.Resolver(historial, Ahora.AddDays(4));

        Assert.Equal(485m, hoy.Detalle);
        Assert.Equal(440m, hoy.Mayor);
        Assert.Equal(500m, enCuatroDias.Detalle);
    }

    [Theory]
    [InlineData(399.99, true)]    // bajo el precio mínimo con ITBIS (400)
    [InlineData(400.00, false)]
    public void Precio_minimo_se_compara_con_impuesto(decimal precio, bool bajoMinimo)
    {
        var articulo = CrearArticulo(precioMinimo: 400m);

        Assert.Equal(bajoMinimo, ReglasPrecio.EstaBajoMinimo(articulo, Itbis18, precio));
    }

    [Theory]
    [InlineData(353.99, true)]    // base 299.99 < costo 300
    [InlineData(354.00, false)]   // base 300.00
    public void Costo_se_compara_con_la_base_sin_impuesto(decimal precio, bool bajoMinimo)
    {
        var articulo = CrearArticulo(costo: 300m);

        Assert.Equal(bajoMinimo, ReglasPrecio.EstaBajoMinimo(articulo, Itbis18, precio));
    }

    [Fact]
    public void Impuesto_separa_base_y_monto_de_un_precio_con_itbis()
    {
        Assert.Equal(720.34m, decimal.Round(Itbis18.BaseDesdePrecioConImpuesto(850.00m), 2));
        Assert.Equal(129.66m, decimal.Round(Itbis18.MontoDesdePrecioConImpuesto(850.00m), 2));

        var exento = Ids.Asignar(Impuesto.Crear("EXENTO", "Exento", 0m, 4));
        Assert.Equal(100m, exento.BaseDesdePrecioConImpuesto(100m));
        Assert.Equal(0m, exento.MontoDesdePrecioConImpuesto(100m));
    }
}
