using CgPos.Pos.Aplicacion.Perifericos;
using CgPos.Pos.Infraestructura.Perifericos.Balanzas;
using CgPos.Pos.Infraestructura.Perifericos.Terminales;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CgPos.Pos.Infraestructura.Perifericos;

/// <summary>
/// Elige el periférico según la configuración de la caja, sin que el resto del sistema sepa de qué modelo se trata (RF-285).
/// Cambiar de balanza o de terminal es cambiar <c>Perifericos:Balanza:Tipo</c> o <c>Perifericos:Terminal:Tipo</c> y su modelo.
/// </summary>
internal static class FabricaPerifericos
{
    /// <param name="tipo"><c>Simulada</c> (predeterminado en desarrollo) o <c>Serie</c> para una balanza conectada.</param>
    public static IBalanza CrearBalanza(IServiceProvider proveedor, IConfiguration configuracion)
    {
        var tipo = configuracion["Perifericos:Balanza:Tipo"];
        return string.Equals(tipo, "Serie", StringComparison.OrdinalIgnoreCase)
            ? new BalanzaSerie(configuracion, proveedor.GetRequiredService<ILogger<BalanzaSerie>>())
            : new BalanzaSimulada(configuracion);
    }

    /// <param name="tipo"><c>Simulado</c> (predeterminado en desarrollo) o <c>Conectado</c> para un terminal real.</param>
    public static ITerminalPago CrearTerminal(IServiceProvider proveedor, IConfiguration configuracion)
    {
        var tipo = configuracion["Perifericos:Terminal:Tipo"];
        return string.Equals(tipo, "Conectado", StringComparison.OrdinalIgnoreCase)
            ? new TerminalPagoConectado(configuracion, proveedor.GetRequiredService<TimeProvider>(),
                proveedor.GetRequiredService<ILogger<TerminalPagoConectado>>())
            : new TerminalPagoSimulado(configuracion);
    }
}
