using System.Collections.Frozen;
using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using CgPos.Contratos.Seguridad;
using CgPos.Pos.Aplicacion.Seguridad;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace CgPos.Pos.Agente.Seguridad;

/// <summary>
/// Emite y lee los tokens de sesión de la caja. La clave de firma se genera al arrancar y vive solo en memoria:
/// al reiniciar el Agente las sesiones anteriores dejan de ser válidas y el usuario vuelve a ingresar.
/// </summary>
public sealed class EmisorTokens
{
    public const string Emisor = "CgPos.Pos.Agente";
    public const string Audiencia = "CgPos.Pos.Web";

    private readonly JsonWebTokenHandler _manejador = new();
    private readonly TimeProvider _reloj;

    public EmisorTokens(TimeProvider reloj) => _reloj = reloj;

    public SymmetricSecurityKey Clave { get; } = new(RandomNumberGenerator.GetBytes(32));

    /// <param name="duracion">Duración de la sesión configurada por el negocio (<c>Seguridad.HorasSesion</c>).</param>
    public (string Token, DateTimeOffset ExpiraEn) Emitir(SesionUsuario sesion, TimeSpan duracion)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(duracion, TimeSpan.Zero);
        var ahora = _reloj.GetUtcNow();
        var expira = ahora + duracion;

        var token = _manejador.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = Emisor,
            Audience = Audiencia,
            Subject = new ClaimsIdentity(CrearClaims(sesion)),
            IssuedAt = ahora.UtcDateTime,
            NotBefore = ahora.UtcDateTime,
            Expires = expira.UtcDateTime,
            SigningCredentials = new SigningCredentials(Clave, SecurityAlgorithms.HmacSha256),
        });

        return (token, expira);
    }

    public TokenValidationParameters ParametrosValidacion() => new()
    {
        ValidIssuer = Emisor,
        ValidAudience = Audiencia,
        IssuerSigningKey = Clave,
        ValidateIssuerSigningKey = true,
        ClockSkew = TimeSpan.FromSeconds(30),
        NameClaimType = AtributosToken.Nombre,
        RoleClaimType = AtributosToken.Rol,
    };

    public static IEnumerable<Claim> CrearClaims(SesionUsuario sesion)
    {
        yield return new Claim(AtributosToken.UsuarioId, sesion.UsuarioId.ToString());
        yield return new Claim(AtributosToken.Codigo, sesion.Codigo);
        yield return new Claim(AtributosToken.Nombre, sesion.Nombre);
        yield return new Claim(AtributosToken.RolId, sesion.RolId.ToString());
        yield return new Claim(AtributosToken.Rol, sesion.RolCodigo);
        yield return new Claim(AtributosToken.RolNombre, sesion.RolNombre);
        yield return new Claim(AtributosToken.Nivel, sesion.Nivel.ToString(CultureInfo.InvariantCulture));
        yield return new Claim(AtributosToken.Caja, sesion.CajaId.ToString());
        yield return new Claim(AtributosToken.CajaCodigo, sesion.CajaCodigo);
        yield return new Claim(AtributosToken.CajaNombre, sesion.CajaNombre);
        yield return new Claim(AtributosToken.Sucursal, sesion.SucursalId.ToString());

        foreach (var permiso in sesion.Permisos)
            yield return new Claim(AtributosToken.Permiso, permiso);
    }

    /// <summary>Reconstruye la sesión desde los atributos del token; nulo si faltan datos.</summary>
    public static SesionUsuario? LeerSesion(ClaimsPrincipal usuario)
    {
        if (usuario.Identity?.IsAuthenticated != true)
            return null;

        string? Valor(string tipo) => usuario.FindFirst(tipo)?.Value;

        if (!Guid.TryParse(Valor(AtributosToken.UsuarioId), out var usuarioId)
            || !Guid.TryParse(Valor(AtributosToken.RolId), out var rolId)
            || !Guid.TryParse(Valor(AtributosToken.Caja), out var cajaId)
            || !Guid.TryParse(Valor(AtributosToken.Sucursal), out var sucursalId)
            || !int.TryParse(Valor(AtributosToken.Nivel), NumberStyles.Integer, CultureInfo.InvariantCulture, out var nivel))
            return null;

        return new SesionUsuario(
            usuarioId,
            Valor(AtributosToken.Codigo) ?? string.Empty,
            Valor(AtributosToken.Nombre) ?? string.Empty,
            rolId,
            Valor(AtributosToken.Rol) ?? string.Empty,
            Valor(AtributosToken.RolNombre) ?? string.Empty,
            nivel,
            usuario.FindAll(AtributosToken.Permiso).Select(c => c.Value).ToFrozenSet(StringComparer.Ordinal),
            cajaId,
            Valor(AtributosToken.CajaCodigo) ?? string.Empty,
            Valor(AtributosToken.CajaNombre) ?? string.Empty,
            sucursalId);
    }
}
