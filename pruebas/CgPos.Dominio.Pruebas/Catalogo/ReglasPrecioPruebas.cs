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
    public void Impuesto_separa_base_y_monto_de_un_precio_con_itbis()
    {
        Assert.Equal(720.34m, decimal.Round(Itbis18.BaseDesdePrecioConImpuesto(850.00m), 2));
        Assert.Equal(129.66m, decimal.Round(Itbis18.MontoDesdePrecioConImpuesto(850.00m), 2));

        var exento = Ids.Asignar(Impuesto.Crear("EXENTO", "Exento", 0m, 4));
        Assert.Equal(100m, exento.BaseDesdePrecioConImpuesto(100m));
        Assert.Equal(0m, exento.MontoDesdePrecioConImpuesto(100m));
    }
}
