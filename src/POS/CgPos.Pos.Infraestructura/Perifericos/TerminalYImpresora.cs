using System.Globalization;
using System.Net.Sockets;
using CgPos.Pos.Aplicacion.Perifericos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CgPos.Pos.Infraestructura.Perifericos;

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

/// <summary>
/// Impresora de tickets según <c>Perifericos:Impresora:Tipo</c>:
/// <list type="bullet">
/// <item><c>Archivo</c> (predeterminado): deja el ticket en texto y en ESC/POS en <c>Carpeta</c> (sin impresora conectada).</item>
/// <item><c>Red</c>: envía los bytes ESC/POS a <c>Host</c>:<c>Puerto</c> (9100), el modo de las térmicas de red.</item>
/// </list>
/// La gaveta se abre con el pulso ESC/POS a través de la misma impresora.
/// </summary>
internal sealed class ImpresoraTicket(IConfiguration configuracion, TimeProvider reloj, ILogger<ImpresoraTicket> registro) : IImpresoraTicket
{
    /// <summary>ESC p 0 25 250: pulso al conector de la gaveta.</summary>
    private static readonly byte[] PulsoGaveta = [0x1B, 0x70, 0x00, 0x19, 0xFA];

    public Task<ResultadoImpresion> ImprimirAsync(DocumentoImpresion documento, CancellationToken cancelacion = default) =>
        EnviarAsync(documento.Nombre, documento.Texto, documento.EscPos, cancelacion);

    public Task<ResultadoImpresion> AbrirGavetaAsync(CancellationToken cancelacion = default) =>
        EnviarAsync("gaveta", $"Gaveta abierta {reloj.GetLocalNow():dd/MM/yyyy HH:mm:ss}", PulsoGaveta, cancelacion);

    private async Task<ResultadoImpresion> EnviarAsync(string nombre, string texto, byte[] bytes, CancellationToken cancelacion)
    {
        var seccion = configuracion.GetSection("Perifericos:Impresora");
        try
        {
            if (string.Equals(seccion["Tipo"], "Red", StringComparison.OrdinalIgnoreCase))
            {
                var puerto = int.TryParse(seccion["Puerto"], out var valor) ? valor : 9100;
                using var cliente = new TcpClient();
                using var limite = CancellationTokenSource.CreateLinkedTokenSource(cancelacion);
                limite.CancelAfter(TimeSpan.FromSeconds(5));
                await cliente.ConnectAsync(seccion["Host"] ?? throw new InvalidOperationException("Falta Perifericos:Impresora:Host."), puerto, limite.Token);
                await using var flujo = cliente.GetStream();
                await flujo.WriteAsync(bytes, limite.Token);
                return new ResultadoImpresion(true, null);
            }

            var carpeta = seccion["Carpeta"] is { Length: > 0 } ruta ? ruta : @"C:\CGPOS\Impresiones";
            Directory.CreateDirectory(carpeta);
            var baseArchivo = Path.Combine(carpeta, $"{reloj.GetLocalNow():yyyyMMdd-HHmmss-fff}-{nombre}");
            await File.WriteAllTextAsync(baseArchivo + ".txt", texto, cancelacion);
            await File.WriteAllBytesAsync(baseArchivo + ".escpos", bytes, cancelacion);
            return new ResultadoImpresion(true, null);
        }
        catch (Exception excepcion) when (excepcion is IOException or SocketException or UnauthorizedAccessException or InvalidOperationException or OperationCanceledException)
        {
            registro.LogWarning(excepcion, "La impresora no respondió al enviar {Documento}", nombre);
            return new ResultadoImpresion(false, $"La impresora no respondió: {excepcion.Message}");
        }
    }
}
