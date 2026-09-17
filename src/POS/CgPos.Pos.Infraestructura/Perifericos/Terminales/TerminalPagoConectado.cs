using System.Globalization;
using System.IO.Ports;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using CgPos.Pos.Aplicacion.Perifericos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CgPos.Pos.Infraestructura.Perifericos.Terminales;

/// <summary>
/// Terminal de pago genérico, por red o por puerto COM, para modelos de mensajes de texto. Lo propio del modelo (mensajes y
/// respuestas) vive en su <see cref="PerfilTerminal"/>; aquí solo se arma el mensaje, se envía y se interpreta la respuesta. Si el
/// terminal no responde, se informa sin conexión y la caja ofrece la aprobación manual (RF-213). CardNet tiene su propio
/// protocolo en <see cref="TerminalPagoCardNet"/>.
/// </summary>
internal sealed class TerminalPagoConectado(IConfiguration configuracion, TimeProvider reloj, ILogger<TerminalPagoConectado> registro) : ITerminalPago
{
    public bool ConsultaTarjeta => false;

    public Task<ResultadoConsultaTarjeta> ConsultarTarjetaAsync(CancellationToken cancelacion = default) =>
        Task.FromResult(new ResultadoConsultaTarjeta(false, false, null, null, "Este terminal no lee la tarjeta antes de cobrar."));

    public Task<ResultadoTerminal> CobrarAsync(decimal monto, decimal impuesto, string referenciaVenta, CancellationToken cancelacion = default) =>
        EjecutarAsync(perfil => Formatear(perfil.PlantillaCobro, monto, referenciaVenta, null), cancelacion);

    public Task<ResultadoTerminal> AnularAsync(string aprobacion, string? referenciaTerminal, decimal monto, CancellationToken cancelacion = default) =>
        EjecutarAsync(perfil => Formatear(perfil.PlantillaAnulacion, monto, null, aprobacion), cancelacion);

    public async Task<ResultadoLoteTerminal> CerrarLoteAsync(CancellationToken cancelacion = default)
    {
        var perfil = PerfilesTerminal.Desde(configuracion);
        if (perfil.PlantillaCierreLote is not { Length: > 0 })
            return new ResultadoLoteTerminal(false, $"El terminal {perfil.Nombre} no cierra el lote desde la caja.");

        var respuesta = await ConversarAsync(perfil, Formatear(perfil.PlantillaCierreLote, 0m, null, null), cancelacion);
        if (respuesta is null)
            return new ResultadoLoteTerminal(false, "El terminal de pago no responde.");

        var campos = Interpretar(respuesta, perfil);
        var correcto = perfil.Aprobadas.Contains(campos.Aprobada, StringComparer.OrdinalIgnoreCase);
        return new ResultadoLoteTerminal(correcto, campos.Mensaje ?? (correcto ? "Lote cerrado." : "El terminal rechazó el cierre de lote."),
            campos.Lote, campos.Transacciones, campos.Monto, campos.Aprobaciones);
    }

    private async Task<ResultadoTerminal> EjecutarAsync(Func<PerfilTerminal, string> armar, CancellationToken cancelacion)
    {
        var perfil = PerfilesTerminal.Desde(configuracion);
        var respuesta = await ConversarAsync(perfil, armar(perfil), cancelacion);
        if (respuesta is null)
            return new ResultadoTerminal(false, true, null, null, null, "El terminal de pago no responde.");

        var campos = Interpretar(respuesta, perfil);
        var aprobada = perfil.Aprobadas.Contains(campos.Aprobada, StringComparer.OrdinalIgnoreCase);
        return new ResultadoTerminal(aprobada, false, aprobada ? campos.Aprobacion : null, campos.Digitos, campos.Marca,
            campos.Mensaje ?? (aprobada ? "Aprobada" : "Transacción declinada por el terminal."));
    }

    /// <summary>Reemplaza en la plantilla del modelo los datos de la transacción.</summary>
    private string Formatear(string plantilla, decimal monto, string? referencia, string? aprobacion) =>
        plantilla
            .Replace("{monto}", monto.ToString("0.00", CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace("{montoCentavos}", ((long)decimal.Round(monto * 100m, 0, MidpointRounding.AwayFromZero)).ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace("{referencia}", referencia ?? string.Empty, StringComparison.Ordinal)
            .Replace("{aprobacion}", aprobacion ?? string.Empty, StringComparison.Ordinal)
            .Replace("{fecha}", reloj.GetLocalNow().ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture), StringComparison.Ordinal);

    /// <summary>Lee de la respuesta del terminal lo que la caja necesita, según el patrón del modelo.</summary>
    internal static CamposTerminal Interpretar(string respuesta, PerfilTerminal perfil)
    {
        ArgumentNullException.ThrowIfNull(perfil);
        var coincidencia = Regex.Match(respuesta.Trim(), perfil.PatronRespuesta, RegexOptions.None, TimeSpan.FromSeconds(1));
        if (!coincidencia.Success)
            return new CamposTerminal(string.Empty, null, null, null, respuesta.Trim(), null, 0, 0m, []);

        string? Grupo(string nombre) => coincidencia.Groups[nombre].Success && coincidencia.Groups[nombre].Value is { Length: > 0 } valor ? valor.Trim() : null;

        return new CamposTerminal(
            Grupo("aprobada") ?? string.Empty,
            Grupo("aprobacion"),
            Grupo("digitos"),
            Grupo("marca"),
            Grupo("mensaje"),
            Grupo("lote"),
            int.TryParse(Grupo("transacciones"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var transacciones) ? transacciones : 0,
            decimal.TryParse(Grupo("monto"), NumberStyles.Number, CultureInfo.InvariantCulture, out var monto) ? monto : 0m,
            Grupo("aprobaciones")?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? []);
    }

    /// <summary>Envía el mensaje por el transporte configurado y devuelve la respuesta; nulo si el terminal no contesta.</summary>
    private async Task<string?> ConversarAsync(PerfilTerminal perfil, string mensaje, CancellationToken cancelacion)
    {
        var seccion = configuracion.GetSection("Perifericos:Terminal");
        try
        {
            return perfil.Transporte == TransporteTerminal.Serie
                ? await PorSerieAsync(seccion, perfil, mensaje, cancelacion)
                : await PorSocketAsync(seccion, perfil, mensaje, cancelacion);
        }
        catch (Exception excepcion) when (excepcion is SocketException or IOException or TimeoutException or UnauthorizedAccessException
            or InvalidOperationException or OperationCanceledException)
        {
            registro.LogWarning(excepcion, "El terminal {Modelo} no respondió", perfil.Nombre);
            return null;
        }
    }

    private static async Task<string?> PorSocketAsync(IConfigurationSection seccion, PerfilTerminal perfil, string mensaje, CancellationToken cancelacion)
    {
        if (seccion["Host"] is not { Length: > 0 } host || !int.TryParse(seccion["Puerto"], out var puerto))
            return null;

        using var cliente = new TcpClient();
        using var tiempo = CancellationTokenSource.CreateLinkedTokenSource(cancelacion);
        tiempo.CancelAfter(TimeSpan.FromSeconds(perfil.SegundosEspera));

        await cliente.ConnectAsync(host, puerto, tiempo.Token);
        await using var flujo = cliente.GetStream();
        var datos = Encoding.ASCII.GetBytes(mensaje + perfil.Terminador);
        await flujo.WriteAsync(datos, tiempo.Token);

        var respuesta = new StringBuilder();
        var bufer = new byte[512];
        while (!tiempo.IsCancellationRequested)
        {
            var leidos = await flujo.ReadAsync(bufer, tiempo.Token);
            if (leidos == 0)
                break;

            respuesta.Append(Encoding.ASCII.GetString(bufer, 0, leidos));
            if (perfil.Terminador is not { Length: > 0 } fin || respuesta.ToString().Contains(fin, StringComparison.Ordinal))
                break;
        }

        return respuesta.Length > 0 ? respuesta.ToString() : null;
    }

    private static async Task<string?> PorSerieAsync(IConfigurationSection seccion, PerfilTerminal perfil, string mensaje, CancellationToken cancelacion)
    {
        if (seccion["Puerto"] is not { Length: > 0 } nombrePuerto)
            return null;

        using var serie = new SerialPort(nombrePuerto, int.TryParse(seccion["Baudios"], out var baudios) ? baudios : 115200)
        {
            ReadTimeout = perfil.SegundosEspera * 1000,
            WriteTimeout = perfil.SegundosEspera * 1000,
            Encoding = Encoding.ASCII,
        };

        serie.Open();
        serie.DiscardInBuffer();
        serie.Write(mensaje + perfil.Terminador);

        var limite = DateTime.UtcNow.AddSeconds(perfil.SegundosEspera);
        var respuesta = new StringBuilder();
        while (DateTime.UtcNow < limite && !cancelacion.IsCancellationRequested)
        {
            if (serie.BytesToRead > 0)
            {
                respuesta.Append(serie.ReadExisting());
                if (perfil.Terminador is not { Length: > 0 } fin || respuesta.ToString().Contains(fin, StringComparison.Ordinal))
                    break;
            }

            await Task.Delay(100, cancelacion);
        }

        return respuesta.Length > 0 ? respuesta.ToString() : null;
    }
}

/// <summary>Lo que la caja lee de la respuesta del terminal, ya separado del formato de cada modelo.</summary>
internal sealed record CamposTerminal(
    string Aprobada,
    string? Aprobacion,
    string? Digitos,
    string? Marca,
    string? Mensaje,
    string? Lote,
    int Transacciones,
    decimal Monto,
    IReadOnlyList<string> Aprobaciones);
