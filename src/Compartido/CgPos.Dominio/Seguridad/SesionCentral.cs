using CgPos.Dominio.Comun;

namespace CgPos.Dominio.Seguridad;

/// <summary>
/// Token de renovación de una sesión del Central Manager; solo se guarda su hash SHA-256. Cada renovación usa el token una sola vez
/// y emite otro de la misma familia (rotación). Presentar un token ya usado indica que fue copiado: se revoca la familia completa.
/// </summary>
public sealed class SesionCentral : Entidad
{
    public const int LargoHashToken = 64;
    public const int LargoMaximoMotivo = 150;
    public const int LargoMaximoIp = 45;
    public const int LargoMaximoAgenteUsuario = 256;

    private SesionCentral()
    {
    }

    public Guid UsuarioId { get; private set; }

    /// <summary>Id de la sesión: el del primer token, compartido por todos los que salen de sus renovaciones.</summary>
    public Guid Familia { get; private set; }

    public string TokenHash { get; private set; } = string.Empty;
    public DateTimeOffset CreadaEn { get; private set; }

    /// <summary>Vencimiento de este token por inactividad, sin pasar del fin de la sesión.</summary>
    public DateTimeOffset ExpiraEn { get; private set; }

    /// <summary>Fin absoluto de la sesión: ninguna renovación lo extiende.</summary>
    public DateTimeOffset FinSesion { get; private set; }

    public DateTimeOffset? UsadaEn { get; private set; }
    public Guid? ReemplazadaPorId { get; private set; }
    public DateTimeOffset? RevocadaEn { get; private set; }
    public string? MotivoRevocacion { get; private set; }
    public string? DireccionIp { get; private set; }
    public string? AgenteUsuario { get; private set; }

    public bool FueUsada => UsadaEn is not null;

    /// <param name="inactividad">Tiempo sin renovar tras el que vence el token.</param>
    /// <param name="duracion">Duración máxima de la sesión.</param>
    public static SesionCentral Iniciar(Guid usuarioId, string tokenHash, DateTimeOffset ahora, TimeSpan inactividad, TimeSpan duracion,
        string? direccionIp, string? agenteUsuario)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(duracion, TimeSpan.Zero);

        var sesion = Nueva(usuarioId, null, tokenHash, ahora, ahora + duracion, inactividad, direccionIp, agenteUsuario);
        sesion.Familia = sesion.Id;
        return sesion;
    }

    /// <summary>Marca este token como usado y devuelve el que lo reemplaza en la misma sesión.</summary>
    public SesionCentral Rotar(string tokenHash, DateTimeOffset ahora, TimeSpan inactividad, string? direccionIp, string? agenteUsuario)
    {
        if (!EstaVigente(ahora))
            throw new InvalidOperationException("Solo se renueva un token vigente que no se ha usado.");

        var nueva = Nueva(UsuarioId, Familia, tokenHash, ahora, FinSesion, inactividad, direccionIp, agenteUsuario);
        UsadaEn = ahora;
        ReemplazadaPorId = nueva.Id;
        return nueva;
    }

    public bool EstaVigente(DateTimeOffset ahora) => UsadaEn is null && RevocadaEn is null && ExpiraEn > ahora;

    public void Revocar(DateTimeOffset ahora, string motivo)
    {
        if (RevocadaEn is not null)
            return;

        MotivoRevocacion = Validar.Texto(motivo, "Motivo de revocación", LargoMaximoMotivo);
        RevocadaEn = ahora;
    }

    private static SesionCentral Nueva(Guid usuarioId, Guid? familia, string tokenHash, DateTimeOffset ahora, DateTimeOffset finSesion, TimeSpan inactividad,
        string? direccionIp, string? agenteUsuario)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(inactividad, TimeSpan.Zero);
        if (tokenHash is not { Length: LargoHashToken } || !tokenHash.All(char.IsAsciiHexDigit))
            throw new ArgumentException($"El hash del token debe ser hexadecimal de {LargoHashToken} caracteres.", nameof(tokenHash));

        var vence = ahora + inactividad;
        var sesion = new SesionCentral
        {
            UsuarioId = Validar.Id(usuarioId, "Usuario"),
            TokenHash = tokenHash.ToUpperInvariant(),
            CreadaEn = ahora,
            FinSesion = finSesion,
            ExpiraEn = vence < finSesion ? vence : finSesion,
            DireccionIp = Recortar(direccionIp, LargoMaximoIp),
            AgenteUsuario = Recortar(agenteUsuario, LargoMaximoAgenteUsuario),
        };
        sesion.Familia = familia ?? Guid.Empty;
        return sesion;
    }

    private static string? Recortar(string? texto, int largo) =>
        string.IsNullOrWhiteSpace(texto) ? null : texto.Length <= largo ? texto : texto[..largo];
}
