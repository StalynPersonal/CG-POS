using CgPos.Dominio.Comun;

namespace CgPos.Dominio.Organizacion;

/// <summary>
/// Credencial con la que una caja se autentica ante el Central para obtener su token de dispositivo. El secreto se muestra una sola vez
/// al emitirla (se configura en la caja) y aquí solo queda su hash SHA-256. Una caja tiene a lo sumo una credencial activa.
/// </summary>
public sealed class CredencialDispositivo : Entidad
{
    public const int LargoHashSecreto = 64;
    public const int LargoMaximoNombre = 150;
    public const int LargoMaximoMotivo = 250;
    public const int LargoMaximoIp = 45;

    private CredencialDispositivo()
    {
    }

    public Guid CajaId { get; private set; }
    public string SecretoHash { get; private set; } = string.Empty;
    public DateTimeOffset EmitidaEn { get; private set; }

    /// <summary>Usuario del Central que la emitió.</summary>
    public string EmitidaPor { get; private set; } = string.Empty;

    public DateTimeOffset? RevocadaEn { get; private set; }
    public string? MotivoRevocacion { get; private set; }
    public DateTimeOffset? UltimoUsoEn { get; private set; }
    public string? UltimaIp { get; private set; }

    public bool Activa => RevocadaEn is null;

    public static CredencialDispositivo Emitir(Guid cajaId, string secretoHash, DateTimeOffset ahora, string emitidaPor)
    {
        if (secretoHash is not { Length: LargoHashSecreto } || !secretoHash.All(char.IsAsciiHexDigit))
            throw new ArgumentException($"El hash del secreto debe ser hexadecimal de {LargoHashSecreto} caracteres.", nameof(secretoHash));

        return new CredencialDispositivo
        {
            CajaId = Validar.Id(cajaId, "Caja"),
            SecretoHash = secretoHash.ToUpperInvariant(),
            EmitidaEn = ahora,
            EmitidaPor = Validar.Texto(emitidaPor, "Emitida por", LargoMaximoNombre),
        };
    }

    public void Revocar(DateTimeOffset ahora, string motivo)
    {
        if (RevocadaEn is not null)
            return;

        MotivoRevocacion = Validar.Texto(motivo, "Motivo de revocación", LargoMaximoMotivo);
        RevocadaEn = ahora;
    }

    public void RegistrarUso(DateTimeOffset ahora, string? direccionIp)
    {
        UltimoUsoEn = ahora;
        UltimaIp = string.IsNullOrWhiteSpace(direccionIp) ? null : direccionIp.Length <= LargoMaximoIp ? direccionIp : direccionIp[..LargoMaximoIp];
    }
}
