using System.Globalization;
using System.Net.Sockets;
using CgPos.Pos.Aplicacion.Perifericos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CgPos.Pos.Infraestructura.Perifericos.Terminales;

/// <summary>
/// Terminal de pago de desarrollo, configurable en <c>Perifericos:TerminalSimulado</c>: <c>SinConexion</c>, <c>Rechazar</c>,
/// <c>RetardoMs</c>, <c>Marca</c> y <c>UltimosDigitos</c>. Se reemplaza por el controlador del Verifone o la pasarela elegida.
/// </summary>
internal sealed class TerminalPagoSimulado(IConfiguration configuracion) : ITerminalPago
{
    public async Task<ResultadoTerminal> CobrarAsync(decimal monto, string referenciaVenta, CancellationToken cancelacion = default)
    {
        var seccion = configuracion.GetSection("Perifericos:TerminalSimulado");
        await EsperarAsync(seccion, cancelacion);

        if (EsVerdadero(seccion["SinConexion"]))
            return new ResultadoTerminal(false, true, null, null, null, "El terminal de pago no responde.");
        if (EsVerdadero(seccion["Rechazar"]))
            return new ResultadoTerminal(false, false, null, null, null, "Transacción declinada por el banco emisor.");

        return new ResultadoTerminal(true, false, NuevaAprobacion(), seccion["UltimosDigitos"] ?? "4242", seccion["Marca"] ?? "VISA", "Aprobada");
    }

    public async Task<ResultadoTerminal> AnularAsync(string aprobacion, decimal monto, CancellationToken cancelacion = default)
    {
        var seccion = configuracion.GetSection("Perifericos:TerminalSimulado");
        await EsperarAsync(seccion, cancelacion);

        return EsVerdadero(seccion["SinConexion"])
            ? new ResultadoTerminal(false, true, null, null, null, "El terminal de pago no responde.")
            : new ResultadoTerminal(true, false, NuevaAprobacion(), seccion["UltimosDigitos"] ?? "4242", seccion["Marca"] ?? "VISA", $"Anulada la aprobación {aprobacion}");
    }

    /// <summary>
    /// Cierre de lote simulado: informa lo que la propia caja registró como aprobado, que es lo que un terminal real debería reportar.
    /// El modelo definitivo se conecta aquí cuando se defina la pasarela.
    /// </summary>
    public async Task<ResultadoLoteTerminal> CerrarLoteAsync(CancellationToken cancelacion = default)
    {
        var seccion = configuracion.GetSection("Perifericos:TerminalSimulado");
        await EsperarAsync(seccion, cancelacion);

        if (EsVerdadero(seccion["SinConexion"]))
            return new ResultadoLoteTerminal(false, "El terminal de pago no responde.");

        return new ResultadoLoteTerminal(true, "Lote cerrado.", seccion["NumeroLote"] ?? DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture));
    }

    private static Task EsperarAsync(IConfigurationSection seccion, CancellationToken cancelacion) =>
        int.TryParse(seccion["RetardoMs"], NumberStyles.Integer, CultureInfo.InvariantCulture, out var retardo) && retardo > 0
            ? Task.Delay(retardo, cancelacion)
            : Task.CompletedTask;

    private static bool EsVerdadero(string? valor) => string.Equals(valor, "true", StringComparison.OrdinalIgnoreCase);

    private static string NuevaAprobacion() => Random.Shared.Next(100_000, 999_999).ToString(CultureInfo.InvariantCulture);
}

