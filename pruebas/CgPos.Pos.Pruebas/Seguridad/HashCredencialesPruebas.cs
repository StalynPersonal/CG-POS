using CgPos.Pos.Infraestructura.Seguridad;

namespace CgPos.Pos.Pruebas.Seguridad;

public class HashCredencialesPruebas
{
    private readonly HashCredenciales _hash = new();

    [Fact]
    public void Hash_de_clave_se_verifica_y_rechaza_clave_distinta()
    {
        var hash = _hash.HashClave("Cajero.2026");

        Assert.StartsWith("PBKDF2-SHA256$100000$", hash);
        Assert.True(_hash.VerificarClave("Cajero.2026", hash));
        Assert.False(_hash.VerificarClave("cajero.2026", hash));
        Assert.False(_hash.VerificarClave("Cajero.20266", hash));
        Assert.False(_hash.VerificarClave("", hash));
    }

    [Fact]
    public void Misma_clave_produce_hashes_distintos_por_la_sal()
    {
        var a = _hash.HashClave("Cajero.2026");
        var b = _hash.HashClave("Cajero.2026");

        Assert.NotEqual(a, b);
        Assert.True(_hash.VerificarClave("Cajero.2026", a));
        Assert.True(_hash.VerificarClave("Cajero.2026", b));
    }

    [Fact]
    public void Clave_vacia_no_se_acepta() => Assert.Throws<ArgumentException>(() => _hash.HashClave(""));

    [Theory]
    [InlineData("")]
    [InlineData("texto-plano")]
    [InlineData("PBKDF2-SHA256$100000$no-es-base64$tampoco")]
    [InlineData("MD5$1$AAAAAAAAAAAAAAAAAAAAAA==$AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=")]
    [InlineData("PBKDF2-SHA256$1$AAAAAAAAAAAAAAAAAAAAAA==$AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=")]
    public void Hash_con_formato_no_reconocido_no_verifica(string hash)
    {
        Assert.False(_hash.EsHashClaveReconocido(hash));
        Assert.False(_hash.VerificarClave("1234", hash));
    }

}
