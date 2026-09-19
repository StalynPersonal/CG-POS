using System.Globalization;
using System.Net.Sockets;
using CgPos.Pos.Aplicacion.Perifericos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using CgPos.Dominio.Comun;

namespace CgPos.Pos.Infraestructura.Perifericos;

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
        EnviarAsync("gaveta", $"Gaveta abierta {reloj.Ahora():dd/MM/yyyy HH:mm:ss}", PulsoGaveta, cancelacion);

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
            var baseArchivo = Path.Combine(carpeta, $"{reloj.Ahora():yyyyMMdd-HHmmss-fff}-{nombre}");
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
