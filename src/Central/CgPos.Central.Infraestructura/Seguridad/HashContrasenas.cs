using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using CgPos.Central.Aplicacion.Abstracciones;

namespace CgPos.Central.Infraestructura.Seguridad;

/// <summary>
/// Contraseñas: PBKDF2-SHA256 con sal aleatoria y 600,000 iteraciones (recomendación OWASP).
/// Formato: <c>PBKDF2-SHA256$iteraciones$salBase64$hashBase64</c>; las iteraciones quedan en el hash para poder subirlas sin invalidar los existentes.
/// </summary>
internal sealed class HashContrasenas : IHashContrasenas
{
    private const string Algoritmo = "PBKDF2-SHA256";
    private const int Iteraciones = 600_000;
    private const int IteracionesMinimas = 100_000;
    private const int BytesSal = 16;
    private const int BytesHash = 32;

    public string Hash(string contrasena)
    {
        ArgumentException.ThrowIfNullOrEmpty(contrasena);

        var sal = RandomNumberGenerator.GetBytes(BytesSal);
        var hash = Rfc2898DeriveBytes.Pbkdf2(contrasena, sal, Iteraciones, HashAlgorithmName.SHA256, BytesHash);
        return $"{Algoritmo}${Iteraciones}${Convert.ToBase64String(sal)}${Convert.ToBase64String(hash)}";
    }

    public bool Verificar(string contrasena, string hash)
    {
        if (string.IsNullOrEmpty(contrasena) || !TryLeer(hash, out var iteraciones, out var sal, out var esperado))
            return false;

        var calculado = Rfc2898DeriveBytes.Pbkdf2(contrasena, sal, iteraciones, HashAlgorithmName.SHA256, esperado.Length);
        return CryptographicOperations.FixedTimeEquals(calculado, esperado);
    }

    public bool EsHashReconocido(string? hash) => TryLeer(hash, out _, out _, out _);

    private static bool TryLeer(string? texto, out int iteraciones, out byte[] sal, out byte[] hash)
    {
        iteraciones = 0;
        sal = [];
        hash = [];

        if (texto?.Split('$') is not [Algoritmo, var textoIteraciones, var textoSal, var textoHash]
            || !int.TryParse(textoIteraciones, out iteraciones) || iteraciones < IteracionesMinimas)
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

/// <summary>Tokens aleatorios (renovación de sesión, secretos de caja) y su hash SHA-256 para guardarlos.</summary>
internal static class TokensSeguros
{
    public static string Generar() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));

    public static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    public static bool Coincide(string token, string hashGuardado) =>
        CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(Hash(token)), Encoding.ASCII.GetBytes(hashGuardado.ToUpperInvariant()));
}
