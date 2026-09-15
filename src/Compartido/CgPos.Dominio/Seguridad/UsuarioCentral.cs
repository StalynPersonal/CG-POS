using CgPos.Dominio.Comun;

namespace CgPos.Dominio.Seguridad;

/// <summary>
/// Usuario del Central Manager (administración, fiscal, monitoreo…). Ingresa con usuario y contraseña, que se guarda solo como hash.
/// Es distinto del <see cref="Usuario"/> de las cajas, que el Central administra y distribuye.
/// </summary>
public sealed class UsuarioCentral : Entidad
{
    public const int LargoMaximoCodigo = 50;
    public const int LargoMaximoNombre = 150;
    public const int LargoMaximoCorreo = 150;
    public const int LargoMaximoHashContrasena = 256;

    private UsuarioCentral()
    {
    }

    /// <summary>Nombre de usuario con el que ingresa, único.</summary>
    public string Codigo { get; private set; } = string.Empty;

    public string Nombre { get; private set; } = string.Empty;
    public string? Correo { get; private set; }
    public Guid RolId { get; private set; }
    public bool Activo { get; private set; } = true;

    /// <summary>Hash de la contraseña (incluye algoritmo, iteraciones y sal).</summary>
    public string ContrasenaHash { get; private set; } = string.Empty;

    /// <summary>La contraseña la asignó un administrador: hasta cambiarla el usuario solo puede cambiar su contraseña.</summary>
    public bool DebeCambiarContrasena { get; private set; }

    public DateTimeOffset? ContrasenaCambiadaEn { get; private set; }
    public int IntentosFallidos { get; private set; }
    public DateTimeOffset? BloqueadoHasta { get; private set; }
    public DateTimeOffset? UltimoIngresoEn { get; private set; }

    public static UsuarioCentral Crear(string codigo, string nombre, string? correo, Guid rolId, string contrasenaHash, bool debeCambiarContrasena, Guid? id = null)
    {
        var usuario = new UsuarioCentral
        {
            Id = id ?? Guid.CreateVersion7(),
            Codigo = Validar.Texto(codigo, "Usuario", LargoMaximoCodigo),
            ContrasenaHash = Validar.Texto(contrasenaHash, "Hash de la contraseña", LargoMaximoHashContrasena),
            DebeCambiarContrasena = debeCambiarContrasena,
        };
        usuario.ActualizarDatos(nombre, correo);
        usuario.CambiarRol(rolId);
        return usuario;
    }

    public void ActualizarDatos(string nombre, string? correo)
    {
        var textoCorreo = Validar.TextoOpcional(correo, "Correo", LargoMaximoCorreo);
        if (textoCorreo is not null && (textoCorreo.IndexOf('@') <= 0 || textoCorreo.EndsWith('@') || textoCorreo.Contains(' ')))
            throw new ArgumentException("El correo no es válido.", nameof(correo));

        Nombre = Validar.Texto(nombre, "Nombre de usuario", LargoMaximoNombre);
        Correo = textoCorreo;
    }

    public void CambiarRol(Guid rolId) => RolId = Validar.Id(rolId, "Rol");

    /// <param name="debeCambiar">
    /// <c>true</c> cuando la asigna un administrador (contraseña temporal); <c>false</c> cuando la cambia el propio usuario.
    /// </param>
    public void CambiarContrasena(string contrasenaHash, bool debeCambiar, DateTimeOffset ahora)
    {
        ContrasenaHash = Validar.Texto(contrasenaHash, "Hash de la contraseña", LargoMaximoHashContrasena);
        DebeCambiarContrasena = debeCambiar;
        ContrasenaCambiadaEn = ahora;
    }

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
