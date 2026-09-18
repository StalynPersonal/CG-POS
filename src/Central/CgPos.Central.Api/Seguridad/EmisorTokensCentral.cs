using System.Collections.Frozen;
using System.Security.Claims;
using System.Security.Cryptography;
using CgPos.Central.Aplicacion.Dispositivos;
using CgPos.Central.Aplicacion.Seguridad;
using CgPos.Contratos.Central;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace CgPos.Central.Api.Seguridad;

/// <summary>
/// Emite y lee los tokens del Central: de usuario (Central Manager) y de dispositivo (cajas). La clave de firma sale de la configuración
/// del servidor, así todas las instancias del Central validan los mismos tokens y sobreviven a un reinicio.
/// </summary>
public sealed class EmisorTokensCentral
{
    public const string Emisor = "CgPos.Central";
    public const string AudienciaUsuarios = "CgPos.Central.Web";
    public const string AudienciaDispositivos = "CgPos.Pos.Agente";
    public const string ClaveConfiguracion = "Seguridad:ClaveFirmaJwt";

    private const int BytesMinimosClave = 32;

    private readonly JsonWebTokenHandler _manejador = new();
    private readonly TimeProvider _reloj;

    public EmisorTokensCentral(IConfiguration configuracion, IHostEnvironment entorno, TimeProvider reloj, ILogger<EmisorTokensCentral> logger)
    {
        _reloj = reloj;
        Clave = new SymmetricSecurityKey(LeerClave(configuracion, entorno, logger));
    }

    public SymmetricSecurityKey Clave { get; }

    public (string Token, DateTimeOffset ExpiraEn) EmitirUsuario(SesionCentralUsuario sesion, TimeSpan duracion)
    {
        var claims = new List<Claim>
        {
            new(AtributosTokenCentral.Tipo, AtributosTokenCentral.TipoUsuario),
            new(AtributosTokenCentral.UsuarioId, sesion.UsuarioId.ToString()),
            new(AtributosTokenCentral.Codigo, sesion.Codigo),
            new(AtributosTokenCentral.Nombre, sesion.Nombre),
            new(AtributosTokenCentral.RolId, sesion.RolId.ToString()),
            new(AtributosTokenCentral.Rol, sesion.RolCodigo),
            new(AtributosTokenCentral.RolNombre, sesion.RolNombre),
            new(AtributosTokenCentral.Sesion, sesion.SesionId.ToString()),
        };
        claims.AddRange(sesion.Permisos.Select(permiso => new Claim(AtributosTokenCentral.Permiso, permiso)));
        if (sesion.DebeCambiarContrasena)
            claims.Add(new Claim(AtributosTokenCentral.CambiarContrasena, "true"));

        return Emitir(claims, AudienciaUsuarios, duracion);
    }

    public (string Token, DateTimeOffset ExpiraEn) EmitirDispositivo(DispositivoAutenticado dispositivo, TimeSpan duracion) =>
        Emitir(
        [
            new(AtributosTokenCentral.Tipo, AtributosTokenCentral.TipoDispositivo),
            new(AtributosTokenCentral.Caja, dispositivo.CajaId.ToString()),
            new(AtributosTokenCentral.CajaCodigo, dispositivo.CajaCodigo.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            new(AtributosTokenCentral.CajaNombre, dispositivo.CajaNombre),
            new(AtributosTokenCentral.Sucursal, dispositivo.SucursalId.ToString()),
            new(AtributosTokenCentral.SucursalCodigo, dispositivo.SucursalCodigo.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            new(AtributosTokenCentral.Credencial, dispositivo.CredencialId.ToString()),
        ], AudienciaDispositivos, duracion);

    public TokenValidationParameters ParametrosValidacion() => new()
    {
        ValidIssuer = Emisor,
        ValidAudiences = [AudienciaUsuarios, AudienciaDispositivos],
        IssuerSigningKey = Clave,
        ValidateIssuerSigningKey = true,
        ClockSkew = TimeSpan.FromSeconds(30),
        NameClaimType = AtributosTokenCentral.Nombre,
        RoleClaimType = AtributosTokenCentral.Rol,
    };

    /// <summary>Sesión del usuario desde su token; nulo si no es un token de usuario completo.</summary>
    public static DatosSesionCentral? LeerSesion(ClaimsPrincipal usuario)
    {
        if (usuario.Identity?.IsAuthenticated != true || usuario.FindFirst(AtributosTokenCentral.Tipo)?.Value != AtributosTokenCentral.TipoUsuario)
            return null;

        string? Valor(string tipo) => usuario.FindFirst(tipo)?.Value;

        if (!int.TryParse(Valor(AtributosTokenCentral.UsuarioId), out var usuarioId) || !Guid.TryParse(Valor(AtributosTokenCentral.Sesion), out var sesionId))
            return null;

        return new DatosSesionCentral(
            usuarioId,
            Valor(AtributosTokenCentral.Codigo) ?? string.Empty,
            Valor(AtributosTokenCentral.Nombre) ?? string.Empty,
            Valor(AtributosTokenCentral.Rol) ?? string.Empty,
            Valor(AtributosTokenCentral.RolNombre) ?? string.Empty,
            usuario.FindAll(AtributosTokenCentral.Permiso).Select(c => c.Value).Order(StringComparer.Ordinal).ToList(),
            Valor(AtributosTokenCentral.CambiarContrasena) == "true",
            sesionId);
    }

    /// <summary>Caja autenticada desde su token de dispositivo; nulo si no es un token de dispositivo completo.</summary>
    public static DatosDispositivo? LeerDispositivo(ClaimsPrincipal usuario)
    {
        if (usuario.Identity?.IsAuthenticated != true || usuario.FindFirst(AtributosTokenCentral.Tipo)?.Value != AtributosTokenCentral.TipoDispositivo)
            return null;

        string? Valor(string tipo) => usuario.FindFirst(tipo)?.Value;

        return int.TryParse(Valor(AtributosTokenCentral.Caja), out var cajaId) && int.TryParse(Valor(AtributosTokenCentral.Sucursal), out var sucursalId)
            ? new DatosDispositivo(cajaId, Valor(AtributosTokenCentral.CajaCodigo) ?? string.Empty,
                Valor(AtributosTokenCentral.CajaNombre) ?? string.Empty, sucursalId,
                Valor(AtributosTokenCentral.SucursalCodigo) ?? string.Empty)
            : null;
    }

    public static DatosSesionCentral ConvertirDto(SesionCentralUsuario sesion) =>
        new(sesion.UsuarioId, sesion.Codigo, sesion.Nombre, sesion.RolCodigo, sesion.RolNombre, sesion.Permisos.Order(StringComparer.Ordinal).ToList(),
            sesion.DebeCambiarContrasena, sesion.SesionId);

    private (string Token, DateTimeOffset ExpiraEn) Emitir(IEnumerable<Claim> claims, string audiencia, TimeSpan duracion)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(duracion, TimeSpan.Zero);
        var ahora = _reloj.GetUtcNow();
        var expira = ahora + duracion;

        var token = _manejador.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = Emisor,
            Audience = audiencia,
            Subject = new ClaimsIdentity(claims),
            IssuedAt = ahora.UtcDateTime,
            NotBefore = ahora.UtcDateTime,
            Expires = expira.UtcDateTime,
            SigningCredentials = new SigningCredentials(Clave, SecurityAlgorithms.HmacSha256),
        });

        return (token, expira);
    }

    private static byte[] LeerClave(IConfiguration configuracion, IHostEnvironment entorno, ILogger logger)
    {
        if (configuracion[ClaveConfiguracion] is { Length: > 0 } texto)
        {
            byte[] clave;
            try
            {
                clave = Convert.FromBase64String(texto);
            }
            catch (FormatException)
            {
                throw new InvalidOperationException($"La clave '{ClaveConfiguracion}' debe estar en Base64.");
            }

            return clave.Length >= BytesMinimosClave
                ? clave
                : throw new InvalidOperationException($"La clave '{ClaveConfiguracion}' debe tener al menos {BytesMinimosClave} bytes.");
        }

        if (!entorno.IsDevelopment())
            throw new InvalidOperationException($"Falta la clave de firma de tokens '{ClaveConfiguracion}' (Base64 de {BytesMinimosClave} bytes o más) en la configuración segura del servidor.");

        logger.LogWarning("Sin '{Clave}': se usa una clave temporal de desarrollo y las sesiones no sobreviven a un reinicio.", ClaveConfiguracion);
        return RandomNumberGenerator.GetBytes(BytesMinimosClave);
    }
}

public static class PoliticasCentral
{
    /// <summary>Usuario del Central con sesión, aunque todavía deba cambiar su contraseña temporal.</summary>
    public const string Usuario = "Central.Usuario";

    /// <summary>Caja autenticada con su credencial de dispositivo.</summary>
    public const string Dispositivo = "Central.Dispositivo";

    internal static readonly FrozenSet<string> Todas = new[] { Usuario, Dispositivo }.ToFrozenSet(StringComparer.Ordinal);
}
