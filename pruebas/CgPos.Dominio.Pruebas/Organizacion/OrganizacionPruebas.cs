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
    public void Conserva_el_id_asignado_por_el_Central()
    {
        var idCentral = Guid.CreateVersion7();

        var caja = Caja.Crear(Guid.CreateVersion7(), "01", "Caja 01", id: idCentral);

        Assert.Equal(idCentral, caja.Id);
    }

    [Fact]
    public void Caja_nace_habilitada_y_se_puede_deshabilitar()
    {
        var caja = Caja.Crear(Guid.CreateVersion7(), "01", "Caja 01");
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
        var deCaja = Parametro.Crear("Seguridad.IntentosMaximosClave", "5", cajaId: Guid.CreateVersion7());

        Assert.Null(general.SucursalId);
        Assert.Null(general.CajaId);
        Assert.NotNull(deCaja.CajaId);

        Assert.Throws<ArgumentException>(() =>
            Parametro.Crear("Seguridad.IntentosMaximosClave", "3", sucursalId: Guid.CreateVersion7(), cajaId: Guid.CreateVersion7()));
        Assert.Throws<ArgumentException>(() => Parametro.Crear("Seguridad.IntentosMaximosClave", "3", cajaId: Guid.Empty));
    }
}
