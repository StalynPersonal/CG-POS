namespace CgPos.Pos.Aplicacion.Abstracciones;

/// <summary>Cálculo y verificación de hashes de credenciales de usuario (PIN y código de barras del carné).</summary>
public interface IHashCredenciales
{
    /// <summary>El PIN debe tener entre 4 y 8 dígitos.</summary>
    bool EsPinValido(string? pin);

    /// <summary>Genera el hash del PIN con sal aleatoria. Dos llamadas con el mismo PIN dan hashes distintos.</summary>
    /// <exception cref="ArgumentException">PIN con formato inválido.</exception>
    string HashPin(string pin);

    /// <summary>Compara en tiempo constante. Devuelve <c>false</c> si el hash no tiene un formato reconocido.</summary>
    bool VerificarPin(string pin, string pinHash);

    bool EsHashPinReconocido(string? pinHash);

    /// <summary>SHA-256 hexadecimal (mayúsculas) del código de barras, determinístico para poder buscar por él.</summary>
    string HashCredencialBarras(string codigoBarras);
}
