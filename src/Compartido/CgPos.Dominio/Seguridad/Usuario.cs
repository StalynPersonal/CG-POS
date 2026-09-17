using CgPos.Dominio.Comun;

namespace CgPos.Dominio.Seguridad;

/// <summary>
/// Usuario de la caja (cajero, supervisor…). Entra con su código y su clave; la clave se guarda solo como hash,
/// que calcula el Central (o la carga inicial de desarrollo).
/// </summary>
public sealed class Usuario : Entidad
{
    public const int LargoMaximoCodigo = 20;
    public const int LargoMaximoNombre = 150;
    public const int LargoMaximoHashClave = 256;

    private readonly List<UsuarioCaja> _cajasAsignadas = [];

    private Usuario()
    {
    }

    /// <summary>Código de empleado, único.</summary>
    public string Codigo { get; private set; } = string.Empty;

    public string Nombre { get; private set; } = string.Empty;
    public int RolId { get; private set; }
    public bool Activo { get; private set; } = true;

    /// <summary>Hash de la clave (incluye algoritmo, iteraciones y sal).</summary>
    public string? ClaveHash { get; private set; }

    public int IntentosFallidos { get; private set; }
    public DateTimeOffset? BloqueadoHasta { get; private set; }
    public DateTimeOffset? UltimoIngresoEn { get; private set; }
    public IReadOnlyCollection<UsuarioCaja> CajasAsignadas => _cajasAsignadas;

    public static Usuario Crear(string codigo, string nombre, int rolId)
    {
        var usuario = new Usuario
        {
            Codigo = Validar.Texto(codigo, "Código de usuario", LargoMaximoCodigo),
        };
        usuario.CambiarNombre(nombre);
        usuario.CambiarRol(rolId);
        return usuario;
    }

    public void CambiarNombre(string nombre) => Nombre = Validar.Texto(nombre, "Nombre de usuario", LargoMaximoNombre);

    public void CambiarRol(int rolId) => RolId = Validar.Id(rolId, "Rol");

    public void EstablecerClaveHash(string claveHash) => ClaveHash = Validar.Texto(claveHash, "Hash de la clave", LargoMaximoHashClave);

    public void AsignarCaja(int cajaId)
    {
        Validar.Id(cajaId, "Caja");
        if (_cajasAsignadas.Any(c => c.CajaId == cajaId))
            return;

        _cajasAsignadas.Add(new UsuarioCaja(Id, cajaId));
    }

    public void QuitarCaja(int cajaId) => _cajasAsignadas.RemoveAll(c => c.CajaId == cajaId);

    /// <summary>El usuario debe estar activo y asignado a la caja (RF-6).</summary>
    public bool PuedeOperarCaja(int cajaId) => Activo && _cajasAsignadas.Any(c => c.CajaId == cajaId);

    public bool EstaBloqueado(DateTimeOffset ahora) => BloqueadoHasta > ahora;

    /// <summary>Suma un intento fallido; al llegar al máximo bloquea al usuario durante <paramref name="duracionBloqueo"/>.</summary>
    /// <returns><c>true</c> si el usuario quedó bloqueado con este intento.</returns>
    public bool RegistrarIngresoFallido(DateTimeOffset ahora, int intentosMaximos, TimeSpan duracionBloqueo)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(intentosMaximos, 1);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(duracionBloqueo, TimeSpan.Zero);

        IntentosFallidos++;
        if (IntentosFallidos < intentosMaximos)
            return false;

        BloqueadoHasta = ahora + duracionBloqueo;
        IntentosFallidos = 0;
        return true;
    }

    public void RegistrarIngresoExitoso(DateTimeOffset ahora)
    {
        IntentosFallidos = 0;
        BloqueadoHasta = null;
        UltimoIngresoEn = ahora;
    }

    public void Desbloquear()
    {
        IntentosFallidos = 0;
        BloqueadoHasta = null;
    }

    public void Activar() => Activo = true;

    public void Desactivar() => Activo = false;
}

public sealed class UsuarioCaja
{
    private UsuarioCaja()
    {
    }

    internal UsuarioCaja(int usuarioId, int cajaId)
    {
        UsuarioId = usuarioId;
        CajaId = cajaId;
    }

    public int UsuarioId { get; private set; }
    public int CajaId { get; private set; }
}
