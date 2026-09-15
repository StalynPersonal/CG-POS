using CgPos.Dominio.Globalizacion;

namespace CgPos.Dominio.Pruebas.Globalizacion;

public class CulturaRdPruebas
{
    [Fact]
    public void Monto_usa_punto_decimal_y_coma_de_miles()
    {
        var cultura = CulturaRd.Crear();

        Assert.Equal("2,175.34", 2175.34m.ToString("N2", cultura));
        Assert.Equal("-485.00", (-485m).ToString("N2", cultura));
    }

    [Fact]
    public void Moneda_usa_simbolo_RD()
    {
        var cultura = CulturaRd.Crear();

        Assert.Equal("RD$850.00", 850m.ToString("C", cultura));
        Assert.Equal("RD$2,175.34", 2175.34m.ToString("C", cultura));
        Assert.Equal("-RD$850.00", (-850m).ToString("C", cultura));
    }

    [Fact]
    public void Fecha_corta_es_dia_mes_anio()
    {
        var cultura = CulturaRd.Crear();

        Assert.Equal("14/09/2026", new DateTime(2026, 9, 14).ToString("d", cultura));
    }
}
