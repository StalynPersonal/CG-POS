using CgPos.Dominio.Fiscal;

namespace CgPos.Dominio.Pruebas.Fiscal;

public class DocumentoIdentidadPruebas
{
    [Theory]
    [InlineData("401007551")]    // RNC de la propia DGII
    [InlineData("401-00755-1")]
    [InlineData("131246796")]
    [InlineData("101000007")]
    public void Rnc_con_digito_verificador_correcto_es_valido(string rnc)
    {
        var resultado = DocumentoIdentidad.Validar(rnc);

        Assert.Equal(TipoDocumentoIdentidad.Rnc, resultado.Tipo);
        Assert.True(resultado.FormatoValido);
        Assert.True(resultado.DigitoVerificadorValido);
        Assert.True(resultado.EsValido);
        Assert.DoesNotContain("-", resultado.Documento);
    }

    [Theory]
    [InlineData("401007552")]
    [InlineData("131246790")]
    public void Rnc_con_digito_alterado_no_es_valido(string rnc)
    {
        var resultado = DocumentoIdentidad.Validar(rnc);

        Assert.True(resultado.FormatoValido);
        Assert.False(resultado.DigitoVerificadorValido);
    }

    [Theory]
    [InlineData("00113918205")]
    [InlineData("001-1391820-5")]
    [InlineData("40212345678")]
    public void Cedula_con_digito_verificador_correcto_es_valida(string cedula)
    {
        var resultado = DocumentoIdentidad.Validar(cedula);

        Assert.Equal(TipoDocumentoIdentidad.Cedula, resultado.Tipo);
        Assert.True(resultado.EsValido);
    }

    [Fact]
    public void Cambiar_cualquier_digito_de_una_cedula_valida_la_invalida()
    {
        const string valida = "00113918205";
        for (var posicion = 0; posicion < valida.Length; posicion++)
        {
            var digito = valida[posicion] - '0';
            var alterada = valida[..posicion] + (char)('0' + (digito + 1) % 10) + valida[(posicion + 1)..];
            Assert.False(DocumentoIdentidad.CedulaValida(alterada), $"La cédula {alterada} no debería ser válida.");
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("12345")]
    [InlineData("1234567890")]
    [InlineData("ABC123456")]
    [InlineData(null)]
    public void Documento_sin_formato_de_rnc_ni_cedula(string? documento)
    {
        var resultado = DocumentoIdentidad.Validar(documento);

        Assert.Null(resultado.Tipo);
        Assert.False(resultado.FormatoValido);
        Assert.False(resultado.EsValido);
    }
}
