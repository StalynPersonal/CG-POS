using CgPos.Dominio.Comun;

namespace CgPos.Dominio.Seguridad;

/// <summary>
/// Usuario de la caja (cajero, supervisor…). Las credenciales se guardan solo como hash;
/// el cálculo del hash lo hace el servicio de autenticación.
/// </summary>
public sealed class Usuario : Entidad
{
    public const int LargoMaximoCodigo = 20;
    public const int LargoMaximoNombre = 150;
    public const int LargoMaximoHashPin = 256;
    public const int LargoHashCredencialBarras = 64;

    private readonly List<UsuarioCaja> _cajasAsignadas = [];

    private Usuario()
    {
    }

    /// <summary>Código de empleado, único.</summary>
    public string Codigo { get; private set; } = string.Empty;

    public string Nombre { get; private set; } = string.Empty;
    public Guid RolId { get; private set; }
    public bool Activo { get; private set; } = true;

    /// <summary>Hash del PIN (incluye algoritmo, iteraciones y sal).</summary>
    public string? PinHash { get; private set; }

    /// <summary>Hash SHA-256 (hex) del código de barras del carné, para ingresar escaneándolo.</summary>
    public string? CredencialBarrasHash { get; private set; }

    public int IntentosFallidos { get; private set; }
    public DateTimeOffset? BloqueadoHasta { get; private set; }
    public DateTimeOffset? UltimoIngresoEn { get; private set; }
    public IReadOnlyCollection<UsuarioCaja> CajasAsignadas => _cajasAsignadas;

    public static Usuario Crear(string codigo, string nombre, Guid rolId, Guid? id = null)
    {
        var usuario = new Usuario
        {
            Id = id ?? Guid.CreateVersion7(),
            Codigo = Validar.Texto(codigo, "Código de usuario", LargoMaximoCodigo),
        };
        usuario.CambiarNombre(nombre);
        usuario.CambiarRol(rolId);
        return usuario;
    }

    public void CambiarNombre(string nombre) => Nombre = Validar.Texto(nombre, "Nombre de usuario", LargoMaximoNombre);

    public void CambiarRol(Guid rolId) => RolId = Validar.Id(rolId, "Rol");

    public void EstablecerPinHash(string pinHash) => PinHash = Validar.Texto(pinHash, "Hash del PIN", LargoMaximoHashPin);

    public void EstablecerCredencialBarrasHash(string? hash)
    {
        if (hash is not null && (hash.Length != LargoHashCredencialBarras || !hash.All(char.IsAsciiHexDigit)))
            throw new ArgumentException($"El hash de la credencial debe ser hexadecimal de {LargoHashCredencialBarras} caracteres.", nameof(hash));

        CredencialBarrasHash = hash?.ToUpperInvariant();
    }

    public void AsignarCaja(Guid cajaId)
    {
        Validar.Id(cajaId, "Caja");
        if (_cajasAsignadas.Any(c => c.CajaId == cajaId))
            return;

        _cajasAsignadas.Add(new UsuarioCaja(Id, cajaId));
    }

    public void QuitarCaja(Guid cajaId) => _cajasAsignadas.RemoveAll(c => c.CajaId == cajaId);

    /// <summary>El usuario debe estar activo y asignado a la caja (RF-6).</summary>
    public bool PuedeOperarCaja(Guid cajaId) => Activo && _cajasAsignadas.Any(c => c.CajaId == cajaId);

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

    internal UsuarioCaja(Guid usuarioId, Guid cajaId)
    {
        UsuarioId = usuarioId;
        CajaId = cajaId;
    }

    public Guid UsuarioId { get; private set; }
    public Guid CajaId { get; private set; }
}
