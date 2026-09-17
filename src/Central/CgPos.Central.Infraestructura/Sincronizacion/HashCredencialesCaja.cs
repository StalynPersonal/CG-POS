using System.Security.Cryptography;

namespace CgPos.Central.Infraestructura.Sincronizacion;

/// <summary>
/// Hash de la clave de los usuarios de caja con el formato que verifica la caja (PBKDF2-SHA256 de 100,000 iteraciones). Los usuarios de caja se
/// administran en el Central y bajan ya con su hash: la clave nunca viaja en claro.
/// </summary>
internal static class HashCredencialesCaja
{
    private const string Algoritmo = "PBKDF2-SHA256";
    private const int Iteraciones = 100_000;
    private const int BytesSal = 16;
    private const int BytesHash = 32;

    public static string HashClave(string clave)
    {
        var sal = RandomNumberGenerator.GetBytes(BytesSal);
        var hash = Rfc2898DeriveBytes.Pbkdf2(clave, sal, Iteraciones, HashAlgorithmName.SHA256, BytesHash);
        return $"{Algoritmo}${Iteraciones}${Convert.ToBase64String(sal)}${Convert.ToBase64String(hash)}";
    }

    public static bool VerificarClave(string clave, string claveHash)
    {
        if (claveHash.Split('$') is not [Algoritmo, var textoIteraciones, var textoSal, var textoHash] || !int.TryParse(textoIteraciones, out var iteraciones))
            return false;

        try
        {
            var esperado = Convert.FromBase64String(textoHash);
            var calculado = Rfc2898DeriveBytes.Pbkdf2(clave, Convert.FromBase64String(textoSal), iteraciones, HashAlgorithmName.SHA256, esperado.Length);
            return CryptographicOperations.FixedTimeEquals(calculado, esperado);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
