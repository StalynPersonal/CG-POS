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
        Avisar(proveedor, "Perifericos:Balanza:Tipo", tipo, "Simulada", ["Simulada", "Serie"]);

        return string.Equals(tipo, "Serie", StringComparison.OrdinalIgnoreCase)
            ? new BalanzaSerie(configuracion, proveedor.GetRequiredService<ILogger<BalanzaSerie>>())
            : new BalanzaSimulada(configuracion);
    }

    /// <summary>
    /// <c>Perifericos:Terminal:Tipo</c>: <c>Simulado</c> (predeterminado en desarrollo) o <c>Conectado</c> para un terminal real. Conectado,
    /// un <c>Modelo</c> que diga CardNet usa su protocolo ECR (Ingenico 7000); cualquier otro, el perfil genérico.
    /// </summary>
    public static ITerminalPago CrearTerminal(IServiceProvider proveedor, IConfiguration configuracion)
    {
        var tipo = configuracion["Perifericos:Terminal:Tipo"];
        Avisar(proveedor, "Perifericos:Terminal:Tipo", tipo, "Simulado", ["Simulado", "Conectado"]);

        if (!string.Equals(tipo, "Conectado", StringComparison.OrdinalIgnoreCase))
            return new TerminalPagoSimulado(configuracion);

        return (configuracion["Perifericos:Terminal:Modelo"] ?? string.Empty).Contains("CardNet", StringComparison.OrdinalIgnoreCase)
            ? new TerminalPagoCardNet(configuracion, proveedor.GetRequiredService<ILogger<TerminalPagoCardNet>>())
            : new TerminalPagoConectado(configuracion, proveedor.GetRequiredService<TimeProvider>(),
                proveedor.GetRequiredService<ILogger<TerminalPagoConectado>>());
    }

    /// <summary>
    /// Un tipo mal escrito no rompe nada: el periférico se cae al predeterminado. Pero en silencio eso es peor que un
    /// error, porque la caja parece configurada y los tickets se van a una carpeta. Así que queda dicho en el log.
    /// </summary>
    internal static void Avisar(IServiceProvider proveedor, string clave, string? tipo, string predeterminado, IReadOnlyList<string> validos)
    {
        if (string.IsNullOrWhiteSpace(tipo) || validos.Contains(tipo.Trim(), StringComparer.OrdinalIgnoreCase))
            return;

        proveedor.GetService<ILoggerFactory>()?.CreateLogger("CgPos.Perifericos").LogWarning(
            "{Clave} dice «{Tipo}», que no existe. Los valores son: {Validos}. Se usará {Predeterminado}.",
            clave, tipo, string.Join(", ", validos), predeterminado);
    }
}
