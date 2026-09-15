using System.Security.Cryptography;
using System.Text;
using CgPos.Pos.Aplicacion.Abstracciones;

namespace CgPos.Pos.Infraestructura.Seguridad;

/// <summary>
/// PIN: PBKDF2-SHA256 con sal aleatoria. Formato: <c>PBKDF2-SHA256$iteraciones$salBase64$hashBase64</c>.
/// Las iteraciones quedan en el hash, así se pueden subir en el futuro sin invalidar los existentes.
/// Nota: un PIN numérico corto tiene poca entropía; la protección real es el bloqueo por intentos
/// y el acceso restringido a la base local.
/// </summary>
internal sealed class HashCredenciales : IHashCredenciales
{
    private const string Algoritmo = "PBKDF2-SHA256";
    private const int Iteraciones = 100_000;
    private const int BytesSal = 16;
    private const int BytesHash = 32;
    private const int LargoMinimoPin = 4;
    private const int LargoMaximoPin = 8;

    public bool EsPinValido(string? pin) =>
        pin is { Length: >= LargoMinimoPin and <= LargoMaximoPin } && pin.All(char.IsAsciiDigit);

    public string HashPin(string pin)
    {
        if (!EsPinValido(pin))
            throw new ArgumentException($"El PIN debe tener entre {LargoMinimoPin} y {LargoMaximoPin} dígitos.", nameof(pin));

        var sal = RandomNumberGenerator.GetBytes(BytesSal);
        var hash = Rfc2898DeriveBytes.Pbkdf2(pin, sal, Iteraciones, HashAlgorithmName.SHA256, BytesHash);
        return $"{Algoritmo}${Iteraciones}${Convert.ToBase64String(sal)}${Convert.ToBase64String(hash)}";
    }

    public bool VerificarPin(string pin, string pinHash)
    {
        if (!EsPinValido(pin) || !TryLeer(pinHash, out var iteraciones, out var sal, out var esperado))
            return false;

        var calculado = Rfc2898DeriveBytes.Pbkdf2(pin, sal, iteraciones, HashAlgorithmName.SHA256, esperado.Length);
        return CryptographicOperations.FixedTimeEquals(calculado, esperado);
    }

    public bool EsHashPinReconocido(string? pinHash) => TryLeer(pinHash, out _, out _, out _);

    public string HashCredencialBarras(string codigoBarras)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(codigoBarras);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(codigoBarras.Trim())));
    }

    private static bool TryLeer(string? pinHash, out int iteraciones, out byte[] sal, out byte[] hash)
    {
        iteraciones = 0;
        sal = [];
        hash = [];

        var partes = pinHash?.Split('$');
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
