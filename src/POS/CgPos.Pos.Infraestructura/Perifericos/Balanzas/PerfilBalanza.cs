using System.Globalization;
using System.IO.Ports;
using Microsoft.Extensions.Configuration;

namespace CgPos.Pos.Infraestructura.Perifericos.Balanzas;

/// <summary>
/// Cómo se habla con una balanza por puerto serie: qué se le pide, cómo termina su respuesta y cómo se lee el peso.
/// Cada modelo es un perfil; cambiar de balanza es cambiar el perfil en la configuración, no el código.
/// </summary>
/// <param name="Comando">Texto que se envía para pedir el peso; vacío si la balanza transmite sola.</param>
/// <param name="Patron">Expresión regular con el grupo <c>peso</c> y, si el modelo lo informa, el grupo <c>unidad</c>.</param>
/// <param name="Estables">Marcas que el modelo usa para decir que el peso está estable; vacío si no lo informa.</param>
public sealed record PerfilBalanza(
    string Nombre,
    string Comando,
    string Patron,
    int Baudios,
    Parity Paridad,
    int BitsDatos,
    StopBits BitsParada,
    string UnidadPredeterminada,
    IReadOnlyList<string> Estables,
    string? Terminador = "\r",
    int MilisegundosEspera = 1500);

/// <summary>Perfiles de los modelos que la empresa usa hoy. Uno nuevo se agrega aquí o se arma por configuración.</summary>
public static class PerfilesBalanza
{
    /// <summary>
    /// Datalogic Magellan 9556 (scanner-balanza) por RS-232 con el protocolo de balanza de Datalogic: la caja envía "S" y el
    /// equipo responde el peso con su unidad. Valores por omisión del manual; el puerto y la velocidad se configuran por caja.
    /// </summary>
    public static PerfilBalanza DatalogicMagellan { get; } = new(
        "Datalogic Magellan",
        Comando: "S",
        Patron: @"(?<estado>[SM])?\s*(?<peso>-?\d+[.,]?\d*)\s*(?<unidad>LB|KG|lb|kg)?",
        Baudios: 9600,
        Paridad: Parity.Odd,
        BitsDatos: 7,
        BitsParada: StopBits.One,
        UnidadPredeterminada: "LB",
        Estables: ["S"]);

    /// <summary>Balanza serie genérica que transmite el peso en texto: sirve para la mayoría de los modelos de mostrador.</summary>
    public static PerfilBalanza Generica { get; } = new(
        "Genérica",
        Comando: "W",
        Patron: @"(?<peso>-?\d+[.,]?\d*)\s*(?<unidad>LB|KG|lb|kg)?",
        Baudios: 9600,
        Paridad: Parity.None,
        BitsDatos: 8,
        BitsParada: StopBits.One,
        UnidadPredeterminada: "LB",
        Estables: []);

    /// <summary>
    /// Perfil que se usará, según <c>Perifericos:Balanza:Modelo</c>. Cualquier valor del perfil se puede sobrescribir en la
    /// configuración de la caja, así una balanza parecida no obliga a tocar el código.
    /// </summary>
    public static PerfilBalanza Desde(IConfiguration configuracion)
    {
        ArgumentNullException.ThrowIfNull(configuracion);
        var seccion = configuracion.GetSection("Perifericos:Balanza");
        var perfil = (seccion["Modelo"] ?? string.Empty).Trim() switch
        {
            var modelo when modelo.Contains("Magellan", StringComparison.OrdinalIgnoreCase) => DatalogicMagellan,
            var modelo when modelo.Contains("Datalogic", StringComparison.OrdinalIgnoreCase) => DatalogicMagellan,
            _ => Generica,
        };

        return perfil with
        {
            Comando = seccion["Comando"] ?? perfil.Comando,
            Patron = seccion["Patron"] ?? perfil.Patron,
            Baudios = Entero(seccion["Baudios"]) ?? perfil.Baudios,
            Paridad = Enum.TryParse<Parity>(seccion["Paridad"], ignoreCase: true, out var paridad) ? paridad : perfil.Paridad,
            BitsDatos = Entero(seccion["BitsDatos"]) ?? perfil.BitsDatos,
            BitsParada = Enum.TryParse<StopBits>(seccion["BitsParada"], ignoreCase: true, out var parada) ? parada : perfil.BitsParada,
            UnidadPredeterminada = seccion["Unidad"] ?? perfil.UnidadPredeterminada,
            Terminador = seccion["Terminador"] ?? perfil.Terminador,
            MilisegundosEspera = Entero(seccion["MilisegundosEspera"]) ?? perfil.MilisegundosEspera,
            Estables = seccion["Estables"] is { Length: > 0 } estables
                ? estables.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                : perfil.Estables,
        };
    }

    private static int? Entero(string? texto) =>
        int.TryParse(texto, NumberStyles.Integer, CultureInfo.InvariantCulture, out var valor) ? valor : null;
}
