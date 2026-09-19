using CgPos.Dominio.Comun;

namespace CgPos.Dominio.Organizacion;

/// <summary>
/// Credencial con la que una caja se autentica ante el Central para obtener su token de dispositivo. El secreto se muestra una sola vez
/// al emitirla (se configura en la caja) y aquí solo queda su hash SHA-256. Una caja tiene a lo sumo una credencial activa.
/// </summary>
public sealed class CredencialDispositivo : Entidad
{
    public const int LargoHashSecreto = 64;
    public const int LargoHuella = 64;
    public const int LargoMaximoNombreEquipo = 100;
    public const int LargoMaximoNombre = 150;
    public const int LargoMaximoMotivo = 250;
    public const int LargoMaximoIp = 45;

    private CredencialDispositivo()
    {
    }

    public int CajaId { get; private set; }
    public string SecretoHash { get; private set; } = string.Empty;
    public DateTimeOffset EmitidaEn { get; private set; }

    /// <summary>Usuario del Central que la emitió.</summary>
    public string EmitidaPor { get; private set; } = string.Empty;

    public DateTimeOffset? RevocadaEn { get; private set; }
    public string? MotivoRevocacion { get; private set; }
    public DateTimeOffset? UltimoUsoEn { get; private set; }
    public string? UltimaIp { get; private set; }

    /// <summary>Equipo al que quedó atada la credencial. Nula mientras no se haya fijado: la primera caja que la use se queda con ella.</summary>
    public string? HuellaEquipo { get; private set; }

    /// <summary>Nombre del equipo cuando se fijó, para que en el Central se sepa de qué máquina se trata.</summary>
    public string? NombreEquipo { get; private set; }

    public DateTimeOffset? EquipoFijadoEn { get; private set; }

    public bool Activa => RevocadaEn is null;

    /// <summary>Ya está atada a un equipo: cualquier otro que presente esta credencial se rechaza.</summary>
    public bool TieneEquipo => HuellaEquipo is { Length: > 0 };

    public static CredencialDispositivo Emitir(int cajaId, string secretoHash, DateTimeOffset ahora, string emitidaPor)
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

    /// <summary>Ata la credencial a un equipo. Solo se hace una vez: para cambiar de equipo hay que liberarla primero.</summary>
    public void FijarEquipo(string huellaEquipo, string nombreEquipo, DateTimeOffset ahora)
    {
        if (TieneEquipo)
            throw new InvalidOperationException("La credencial ya está fijada a un equipo: libérela antes de fijarla a otro.");

        var huella = (huellaEquipo ?? string.Empty).Trim();
        if (huella.Length != LargoHuella || !huella.All(char.IsAsciiHexDigit))
            throw new ArgumentException($"La huella del equipo debe ser hexadecimal de {LargoHuella} caracteres.", nameof(huellaEquipo));

        HuellaEquipo = huella.ToUpperInvariant();
        NombreEquipo = Validar.Texto(nombreEquipo, "Nombre del equipo", LargoMaximoNombreEquipo);
        EquipoFijadoEn = ahora;
    }

    /// <summary>Suelta la credencial del equipo, para poder instalar la caja en otro sin emitir una credencial nueva.</summary>
    public void LiberarEquipo()
    {
        HuellaEquipo = null;
        NombreEquipo = null;
        EquipoFijadoEn = null;
    }

    /// <summary>Sin equipo fijado, cualquiera pasa; con equipo fijado, solo ese. Una caja que no manda huella nunca coincide.</summary>
    public bool CoincideEquipo(string? huellaEquipo) =>
        !TieneEquipo || string.Equals(HuellaEquipo, huellaEquipo?.Trim().ToUpperInvariant(), StringComparison.Ordinal);

    public void RegistrarUso(DateTimeOffset ahora, string? direccionIp)
    {
        UltimoUsoEn = ahora;
        UltimaIp = string.IsNullOrWhiteSpace(direccionIp) ? null : direccionIp.Length <= LargoMaximoIp ? direccionIp : direccionIp[..LargoMaximoIp];
    }
}
