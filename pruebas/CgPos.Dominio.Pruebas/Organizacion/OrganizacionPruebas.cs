using CgPos.Dominio.Organizacion;

namespace CgPos.Dominio.Pruebas.Organizacion;

public class OrganizacionPruebas
{
    [Theory]
    [InlineData("401007551")]
    [InlineData(" 131246796 ")]
    [InlineData("131-24679-6")]
    // Una persona física factura con su cédula, de 11 dígitos.
    [InlineData("00100000001")]
    public void Empresa_acepta_rnc_de_9_digitos_o_cedula_de_11(string rnc)
    {
        var empresa = Empresa.Crear(rnc, "Contreras Group SRL");

        Assert.Equal(rnc.Trim().Replace("-", ""), empresa.Rnc);
    }

    [Theory]
    [InlineData("12345678")]
    [InlineData("1234567890")]
    [InlineData("10100000A")]
    [InlineData("")]
    // Formato de RNC correcto, pero el dígito verificador no corresponde.
    [InlineData("101000000")]
    public void Empresa_rechaza_rnc_invalido(string rnc)
    {
        Assert.Throws<ArgumentException>(() => Empresa.Crear(rnc, "Contreras Group SRL"));
    }

    [Fact]
    public void Caja_nace_habilitada_y_se_puede_deshabilitar()
    {
        var caja = Caja.Crear(Ids.Siguiente(), "01", "Caja 01", "10.12.1.101");
        Assert.True(caja.Habilitada);

        caja.Deshabilitar();
        Assert.False(caja.Habilitada);

        caja.Habilitar();
        Assert.True(caja.Habilitada);
    }

    [Fact]
    public void Caja_exige_su_direccion_y_solo_reconoce_la_suya()
    {
        var sucursal = Ids.Siguiente();

        // Sin dirección no hay caja: es parte de lo que la identifica al comunicarse con el Central.
        Assert.Throws<ArgumentException>(() => Caja.Crear(sucursal, "01", "Caja 01", string.Empty));
        Assert.Throws<ArgumentException>(() => Caja.Crear(sucursal, "01", "Caja 01", "no-es-una-ip"));

        var caja = Caja.Crear(sucursal, "01", "Caja 01", " 10.12.1.101 ");
        Assert.Equal("10.12.1.101", caja.DireccionIp);
        Assert.True(caja.CoincideDireccionIp("10.12.1.101"));
        Assert.False(caja.CoincideDireccionIp("10.12.1.102"));
        Assert.False(caja.CoincideDireccionIp(null));

        caja.CambiarDireccionIp("10.12.1.150");
        Assert.True(caja.CoincideDireccionIp("10.12.1.150"));
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
