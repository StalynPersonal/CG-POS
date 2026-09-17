namespace CgPos.Pos.Aplicacion.Abstracciones;

/// <summary>Cálculo y verificación del hash de la clave de los usuarios de la caja.</summary>
public interface IHashCredenciales
{
    /// <summary>Genera el hash de la clave con sal aleatoria. Dos llamadas con la misma clave dan hashes distintos.</summary>
    /// <exception cref="ArgumentException">Clave vacía.</exception>
    string HashClave(string clave);

    /// <summary>Compara en tiempo constante. Devuelve <c>false</c> si la clave está vacía o el hash no tiene un formato reconocido.</summary>
    bool VerificarClave(string clave, string claveHash);

    bool EsHashClaveReconocido(string? claveHash);
}
