using System.Security.Cryptography;
using CgPos.Pos.Aplicacion.Abstracciones;

namespace CgPos.Pos.Infraestructura.Seguridad;

/// <summary>
/// Clave: PBKDF2-SHA256 con sal aleatoria. Formato: <c>PBKDF2-SHA256$iteraciones$salBase64$hashBase64</c> (el mismo que genera el Central).
/// Las iteraciones quedan en el hash, así se pueden subir en el futuro sin invalidar los existentes.
/// </summary>
internal sealed class HashCredenciales : IHashCredenciales
{
    private const string Algoritmo = "PBKDF2-SHA256";
    private const int Iteraciones = 100_000;
    private const int BytesSal = 16;
    private const int BytesHash = 32;

    public string HashClave(string clave)
    {
        if (string.IsNullOrEmpty(clave))
            throw new ArgumentException("La clave no puede estar vacía.", nameof(clave));

        var sal = RandomNumberGenerator.GetBytes(BytesSal);
        var hash = Rfc2898DeriveBytes.Pbkdf2(clave, sal, Iteraciones, HashAlgorithmName.SHA256, BytesHash);
        return $"{Algoritmo}${Iteraciones}${Convert.ToBase64String(sal)}${Convert.ToBase64String(hash)}";
    }

    public bool VerificarClave(string clave, string claveHash)
    {
        if (string.IsNullOrEmpty(clave) || !TryLeer(claveHash, out var iteraciones, out var sal, out var esperado))
            return false;

        var calculado = Rfc2898DeriveBytes.Pbkdf2(clave, sal, iteraciones, HashAlgorithmName.SHA256, esperado.Length);
        return CryptographicOperations.FixedTimeEquals(calculado, esperado);
    }

    public bool EsHashClaveReconocido(string? claveHash) => TryLeer(claveHash, out _, out _, out _);

    private static bool TryLeer(string? claveHash, out int iteraciones, out byte[] sal, out byte[] hash)
    {
        iteraciones = 0;
        sal = [];
        hash = [];

        var partes = claveHash?.Split('$');
        if (partes is not [Algoritmo, var textoIteraciones, var textoSal, var textoHash]
            || !int.TryParse(textoIteraciones, out iteraciones) || iteraciones < 10_000)
            return false;

        try
        {
            sal = Convert.FromBase64String(textoSal);
            hash = Convert.FromBase64String(textoHash);
        }
        catch (FormatException)
        {
            return false;
        }

        return sal.Length >= BytesSal && hash.Length >= BytesHash;
    }
}
