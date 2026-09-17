using CgPos.Dominio.Organizacion;

namespace CgPos.Dominio.Pruebas.Organizacion;

public class OrganizacionPruebas
{
    [Theory]
    [InlineData("101000000")]
    [InlineData(" 131246796 ")]
    public void Empresa_acepta_rnc_de_9_digitos(string rnc)
    {
        var empresa = Empresa.Crear(rnc, "Contreras Group SRL");

        Assert.Equal(rnc.Trim(), empresa.Rnc);
    }

    [Theory]
    [InlineData("12345678")]
    [InlineData("1234567890")]
    [InlineData("10100000A")]
    [InlineData("")]
    public void Empresa_rechaza_rnc_invalido(string rnc)
    {
        Assert.Throws<ArgumentException>(() => Empresa.Crear(rnc, "Contreras Group SRL"));
    }

    [Fact]
    public void Caja_nace_habilitada_y_se_puede_deshabilitar()
    {
        var caja = Caja.Crear(Ids.Siguiente(), 1, "Caja 01");
        Assert.True(caja.Habilitada);

        caja.Deshabilitar();
        Assert.False(caja.Habilitada);

        caja.Habilitar();
        Assert.True(caja.Habilitada);
    }

    [Fact]
    public void Parametro_aplica_a_un_solo_ambito()
    {
        var general = Parametro.Crear("Seguridad.IntentosMaximosClave", "3");
        var deCaja = Parametro.Crear("Seguridad.IntentosMaximosClave", "5", cajaId: Ids.Siguiente());

        Assert.Null(general.SucursalId);
        Assert.Null(general.CajaId);
        Assert.NotNull(deCaja.CajaId);

        Assert.Throws<ArgumentException>(() =>
            Parametro.Crear("Seguridad.IntentosMaximosClave", "3", sucursalId: Ids.Siguiente(), cajaId: Ids.Siguiente()));
        Assert.Throws<ArgumentException>(() => Parametro.Crear("Seguridad.IntentosMaximosClave", "3", cajaId: 0));
    }
}
