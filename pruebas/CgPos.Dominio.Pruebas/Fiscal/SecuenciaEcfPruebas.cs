using CgPos.Dominio.Fiscal;

namespace CgPos.Dominio.Pruebas.Fiscal;

/// <summary>
/// El e-NCF que emite la caja: serie, tipo de dos dígitos y secuencia de diez, como lo pide la DGII. La serie se guarda con
/// cada rango, así que si algún día la DGII cambia de letra se configura sin tocar el programa y lo ya emitido no se altera.
/// </summary>
public class SecuenciaEcfPruebas
{
    [Fact]
    public void El_encf_lleva_serie_tipo_y_diez_digitos()
    {
        Assert.Equal("E320000000003", SecuenciaEcf.FormatearEncf(TipoComprobante.FacturaConsumo, 3));
        Assert.Equal("E310000000001", SecuenciaEcf.FormatearEncf(TipoComprobante.FacturaCreditoFiscal, 1));

        // Sin indicar serie se usa la de hoy, y el rango entero cabe en los diez dígitos.
        Assert.Equal("E329999999999", SecuenciaEcf.FormatearEncf(TipoComprobante.FacturaConsumo, SecuenciaEcf.SecuenciaMaxima));
    }

    [Fact]
    public void La_serie_del_rango_manda_y_se_guarda_en_mayuscula()
    {
        var rango = SecuenciaEcf.Asignar(Ids.Siguiente(), TipoComprobante.FacturaConsumo, 1, 100,
            new DateOnly(2027, 12, 31), serie: "f");

        Assert.Equal("F", rango.Serie);
        Assert.Equal("F320000000005", rango.Encf(5));

        // Un rango sin serie indicada se queda con la que usa la DGII hoy.
        var predeterminado = SecuenciaEcf.Asignar(Ids.Siguiente(), TipoComprobante.FacturaConsumo, 1, 100, new DateOnly(2027, 12, 31));
        Assert.Equal(SecuenciaEcf.SeriePredeterminada, predeterminado.Serie);
        Assert.Equal("E320000000001", predeterminado.Encf(1));
    }

    [Theory]
    [InlineData("EE")]
    [InlineData("3")]
    [InlineData("-")]
    public void Una_serie_que_no_es_una_letra_se_rechaza(string serie)
    {
        Assert.Throws<ArgumentException>(() =>
            SecuenciaEcf.Asignar(Ids.Siguiente(), TipoComprobante.FacturaConsumo, 1, 100, new DateOnly(2027, 12, 31), serie: serie));
    }
}
