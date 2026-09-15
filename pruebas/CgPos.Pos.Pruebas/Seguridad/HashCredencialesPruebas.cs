using CgPos.Pos.Infraestructura.Seguridad;

namespace CgPos.Pos.Pruebas.Seguridad;

public class HashCredencialesPruebas
{
    private readonly HashCredenciales _hash = new();

    [Fact]
    public void Hash_de_pin_se_verifica_y_rechaza_pin_distinto()
    {
        var hash = _hash.HashPin("1234");

        Assert.StartsWith("PBKDF2-SHA256$100000$", hash);
        Assert.True(_hash.VerificarPin("1234", hash));
        Assert.False(_hash.VerificarPin("1235", hash));
        Assert.False(_hash.VerificarPin("12345", hash));
    }

    [Fact]
    public void Mismo_pin_produce_hashes_distintos_por_la_sal()
    {
        var a = _hash.HashPin("1234");
        var b = _hash.HashPin("1234");

        Assert.NotEqual(a, b);
        Assert.True(_hash.VerificarPin("1234", a));
        Assert.True(_hash.VerificarPin("1234", b));
    }

    [Theory]
    [InlineData("123")]
    [InlineData("123456789")]
    [InlineData("12a4")]
    [InlineData("")]
    [InlineData(" 1234")]
    public void Pin_invalido_no_se_acepta(string pin)
    {
        Assert.False(_hash.EsPinValido(pin));
        Assert.Throws<ArgumentException>(() => _hash.HashPin(pin));
    }

    [Theory]
    [InlineData("")]
    [InlineData("texto-plano")]
    [InlineData("PBKDF2-SHA256$100000$no-es-base64$tampoco")]
    [InlineData("MD5$1$AAAAAAAAAAAAAAAAAAAAAA==$AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=")]
    [InlineData("PBKDF2-SHA256$1$AAAAAAAAAAAAAAAAAAAAAA==$AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=")]
    public void Hash_con_formato_no_reconocido_no_verifica(string hash)
    {
        Assert.False(_hash.EsHashPinReconocido(hash));
        Assert.False(_hash.VerificarPin("1234", hash));
    }

    [Fact]
    public void Credencial_de_barras_es_sha256_hex_deterministico()
    {
        var a = _hash.HashCredencialBarras("CGP-C001");
        var b = _hash.HashCredencialBarras("  CGP-C001 ");

        Assert.Equal(64, a.Length);
        Assert.Equal(a, b);
        Assert.Equal(a.ToUpperInvariant(), a);
        Assert.NotEqual(a, _hash.HashCredencialBarras("CGP-C002"));
    }
}
