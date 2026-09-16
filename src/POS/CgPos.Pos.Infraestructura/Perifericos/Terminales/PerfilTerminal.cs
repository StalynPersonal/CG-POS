using System.Globalization;
using Microsoft.Extensions.Configuration;

namespace CgPos.Pos.Infraestructura.Perifericos.Terminales;

/// <summary>Cómo se conecta la caja con el terminal de pago.</summary>
public enum TransporteTerminal
{
    /// <summary>El terminal escucha en la red (host y puerto): es lo habitual en los Lane conectados por Ethernet o Wi-Fi.</summary>
    Socket,

    /// <summary>El terminal se presenta como puerto COM (USB o serie).</summary>
    Serie,
}

/// <summary>
/// Cómo se le habla al terminal: qué mensaje se le manda para cobrar o anular y de dónde se leen la aprobación, los últimos
/// dígitos y la marca. Cada modelo o pasarela es un perfil; cambiar de terminal es cambiar el perfil, no el código.
/// </summary>
/// <param name="PlantillaCobro">Mensaje de cobro; admite {monto}, {montoCentavos}, {referencia} y {fecha}.</param>
/// <param name="PlantillaAnulacion">Mensaje de anulación; además admite {aprobacion}.</param>
/// <param name="PlantillaCierreLote">Mensaje de cierre de lote; vacío si el modelo no lo soporta por este canal.</param>
/// <param name="PatronRespuesta">Expresión regular con los grupos <c>aprobada</c>, <c>aprobacion</c>, <c>digitos</c>, <c>marca</c>, <c>mensaje</c>, <c>lote</c>, <c>transacciones</c> y <c>monto</c>.</param>
/// <param name="Aprobadas">Valores del grupo <c>aprobada</c> que significan aprobada (ej. "00", "APROBADA").</param>
public sealed record PerfilTerminal(
    string Nombre,
    TransporteTerminal Transporte,
    string PlantillaCobro,
    string PlantillaAnulacion,
    string PlantillaCierreLote,
    string PatronRespuesta,
    IReadOnlyList<string> Aprobadas,
    string Terminador = "\r",
    int SegundosEspera = 120);

public static class PerfilesTerminal
{
    /// <summary>
    /// CardNet con Ingenico Lane/7000 (RF-100). El transporte y el formato de los mensajes vienen del documento de integración
    /// de CardNet, así que se dejan en configuración: cuando lo entreguen se ajustan las plantillas y el patrón sin recompilar.
    /// </summary>
    public static PerfilTerminal CardNetLane { get; } = new(
        "CardNet Ingenico Lane/7000",
        TransporteTerminal.Socket,
        PlantillaCobro: "VENTA|{montoCentavos}|{referencia}",
        PlantillaAnulacion: "ANULACION|{montoCentavos}|{aprobacion}",
        PlantillaCierreLote: "CIERRE",
        PatronRespuesta: @"^(?<aprobada>[^|]*)\|(?<aprobacion>[^|]*)\|(?<digitos>[^|]*)\|(?<marca>[^|]*)\|?(?<mensaje>.*)$",
        Aprobadas: ["00", "APROBADA", "APPROVED"]);

    /// <summary>Terminal genérico por socket con mensajes de texto delimitados; sirve para probar una integración nueva.</summary>
    public static PerfilTerminal Generico { get; } = CardNetLane with { Nombre = "Genérico" };

    /// <summary>Perfil que se usará, según <c>Perifericos:Terminal:Modelo</c>, con todo sobrescribible por configuración.</summary>
    public static PerfilTerminal Desde(IConfiguration configuracion)
    {
        ArgumentNullException.ThrowIfNull(configuracion);
        var seccion = configuracion.GetSection("Perifericos:Terminal");
        var perfil = (seccion["Modelo"] ?? string.Empty).Trim() switch
        {
            var modelo when modelo.Contains("CardNet", StringComparison.OrdinalIgnoreCase) => CardNetLane,
            var modelo when modelo.Contains("Lane", StringComparison.OrdinalIgnoreCase) => CardNetLane,
            _ => Generico,
        };

        return perfil with
        {
            Transporte = Enum.TryParse<TransporteTerminal>(seccion["Transporte"], ignoreCase: true, out var transporte) ? transporte : perfil.Transporte,
            PlantillaCobro = seccion["PlantillaCobro"] ?? perfil.PlantillaCobro,
            PlantillaAnulacion = seccion["PlantillaAnulacion"] ?? perfil.PlantillaAnulacion,
            PlantillaCierreLote = seccion["PlantillaCierreLote"] ?? perfil.PlantillaCierreLote,
            PatronRespuesta = seccion["PatronRespuesta"] ?? perfil.PatronRespuesta,
            Terminador = seccion["Terminador"] ?? perfil.Terminador,
            SegundosEspera = int.TryParse(seccion["SegundosEspera"], NumberStyles.Integer, CultureInfo.InvariantCulture, out var espera)
                ? espera
                : perfil.SegundosEspera,
            Aprobadas = seccion["Aprobadas"] is { Length: > 0 } aprobadas
                ? aprobadas.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                : perfil.Aprobadas,
        };
    }
}
