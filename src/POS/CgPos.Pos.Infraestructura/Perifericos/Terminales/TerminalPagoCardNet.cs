using System.Globalization;
using System.Net.Sockets;
using System.Text;
using CgPos.Pos.Aplicacion.Perifericos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CgPos.Pos.Infraestructura.Perifericos.Terminales;

/// <summary>
/// Terminal CardNet (Ingenico 7000) en modo caja (ECRti), según "Integración CAJA – Modelo POS". La caja se conecta por TCP al
/// puerto que escucha el terminal (7060) y conversa con bytes de control: ENQ/ACK para saludar, SYN/EOM/ENQ para ponerlo a recibir,
/// el mensaje con campos separados por FS, ACK a la respuesta y EOT/EOM para cerrar. La IP de la caja debe estar en la lista blanca
/// del terminal. Configuración en <c>Perifericos:Terminal</c>: <c>Host</c>, <c>Puerto</c>, <c>IdMultiMerchant</c> y
/// <c>ConsultaTarjeta</c> (falso si el terminal no tiene activa la consulta CS00).
/// </summary>
internal sealed class TerminalPagoCardNet(IConfiguration configuracion, ILogger<TerminalPagoCardNet> registro) : ITerminalPago
{
    public const int PuertoPredeterminado = 7060;

    private static readonly TimeSpan EsperaSaludo = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan EsperaConsulta = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan EsperaVenta = TimeSpan.FromSeconds(90);
    private static readonly TimeSpan EsperaCierre = TimeSpan.FromSeconds(150);

    private IConfigurationSection Seccion => configuracion.GetSection("Perifericos:Terminal");

    public bool ConsultaTarjeta => !string.Equals(Seccion["ConsultaTarjeta"], "false", StringComparison.OrdinalIgnoreCase);

    public async Task<ResultadoConsultaTarjeta> ConsultarTarjetaAsync(CancellationToken cancelacion = default)
    {
        var respuesta = await ConversarAsync(ProtocoloCardNet.Consulta(), EsperaConsulta, cancelacion);
        return respuesta is null
            ? new ResultadoConsultaTarjeta(false, true, null, null, "El terminal de pago no responde.")
            : ProtocoloCardNet.LeerConsulta(respuesta);
    }

    public async Task<ResultadoTerminal> CobrarAsync(decimal monto, decimal impuesto, string referenciaVenta, CancellationToken cancelacion = default)
    {
        var respuesta = await ConversarAsync(ProtocoloCardNet.Venta(monto, impuesto, referenciaVenta, Seccion["IdMultiMerchant"]), EsperaVenta, cancelacion);
        return respuesta is null
            ? new ResultadoTerminal(false, true, null, null, null, "El terminal de pago no responde.")
            : ProtocoloCardNet.LeerVenta(respuesta);
    }

    public async Task<ResultadoTerminal> AnularAsync(string aprobacion, string? referenciaTerminal, decimal monto, CancellationToken cancelacion = default)
    {
        if (ProtocoloCardNet.Anulacion(referenciaTerminal, Seccion["IdMultiMerchant"]) is not { } mensaje)
            return new ResultadoTerminal(false, false, null, null, null, "La operación no tiene la referencia del terminal: anúlela en el terminal.");

        var respuesta = await ConversarAsync(mensaje, EsperaVenta, cancelacion);
        return respuesta is null
            ? new ResultadoTerminal(false, true, null, null, null, "El terminal de pago no responde.")
            : ProtocoloCardNet.LeerAnulacion(respuesta, aprobacion);
    }

    public async Task<ResultadoLoteTerminal> CerrarLoteAsync(CancellationToken cancelacion = default)
    {
        var respuesta = await ConversarAsync(ProtocoloCardNet.Cierre(Seccion["IdMultiMerchant"]), EsperaCierre, cancelacion);
        return respuesta is null
            ? new ResultadoLoteTerminal(false, "El terminal de pago no responde.")
            : ProtocoloCardNet.LeerCierre(respuesta);
    }

    /// <summary>Una conversación completa con el terminal; devuelve la trama de respuesta o nulo si no contestó.</summary>
    private async Task<string?> ConversarAsync(string mensaje, TimeSpan espera, CancellationToken cancelacion)
    {
        if (Seccion["Host"] is not { Length: > 0 } host)
        {
            registro.LogWarning("El terminal CardNet no tiene Host configurado");
            return null;
        }

        var puerto = int.TryParse(Seccion["Puerto"], NumberStyles.Integer, CultureInfo.InvariantCulture, out var configurado) ? configurado : PuertoPredeterminado;
        using var limite = CancellationTokenSource.CreateLinkedTokenSource(cancelacion);
        limite.CancelAfter(espera + EsperaSaludo + EsperaSaludo);

        try
        {
            using var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
            await socket.ConnectAsync(host, puerto, limite.Token);
            var lector = new LectorSocket(socket);

            // Saludo: el terminal solo contesta si la IP de la caja está en su lista blanca.
            await EnviarAsync(socket, [ProtocoloCardNet.Enq], limite.Token);
            await lector.EsperarAsync(ProtocoloCardNet.Ack, EsperaSaludo, limite.Token);

            // Se le pide pasar a recibir: responde EOM y luego ENQ cuando está listo.
            await EnviarAsync(socket, [ProtocoloCardNet.Syn], limite.Token);
            await lector.EsperarAsync(ProtocoloCardNet.Enq, EsperaSaludo, limite.Token);

            await EnviarAsync(socket, Encoding.ASCII.GetBytes(mensaje), limite.Token);
            if (await lector.SiguienteAsync(EsperaSaludo, limite.Token) == ProtocoloCardNet.Nak)
                return ProtocoloCardNet.RespuestaNak;

            var respuesta = await lector.LeerTramaAsync(espera, limite.Token);

            // Sin este ACK el terminal reversa la transacción aprobada.
            await EnviarAsync(socket, [ProtocoloCardNet.Ack], limite.Token);
            await lector.DescartarAsync(TimeSpan.FromSeconds(2), limite.Token);
            return respuesta;
        }
        catch (Exception excepcion) when (excepcion is SocketException or IOException or TimeoutException or OperationCanceledException)
        {
            registro.LogWarning(excepcion, "El terminal CardNet no completó la conversación");
            return null;
        }
    }

    private static async Task EnviarAsync(Socket socket, byte[] datos, CancellationToken cancelacion) =>
        await socket.SendAsync(datos, SocketFlags.None, cancelacion);

    /// <summary>Lee del socket byte a byte con tiempo de espera, sin cancelar lecturas pendientes (el socket sigue usable).</summary>
    private sealed class LectorSocket(Socket socket)
    {
        private static readonly TimeSpan SilencioFinTrama = TimeSpan.FromMilliseconds(600);
        private readonly Queue<byte> _pendientes = new();
        private readonly byte[] _bufer = new byte[1024];

        public async Task<byte?> SiguienteAsync(TimeSpan espera, CancellationToken cancelacion)
        {
            var hasta = DateTime.UtcNow + espera;
            while (_pendientes.Count == 0)
            {
                if (socket.Available > 0)
                {
                    var leidos = await socket.ReceiveAsync(_bufer, SocketFlags.None, cancelacion);
                    if (leidos == 0)
                        throw new IOException("El terminal cerró la conexión.");
                    for (var i = 0; i < leidos; i++)
                        _pendientes.Enqueue(_bufer[i]);
                    break;
                }

                if (socket.Poll(0, SelectMode.SelectRead) && socket.Available == 0)
                    throw new IOException("El terminal cerró la conexión.");
                if (DateTime.UtcNow >= hasta)
                    return null;

                await Task.Delay(20, cancelacion);
            }

            return _pendientes.Dequeue();
        }

        /// <summary>Espera un byte de control; los demás que lleguen antes (EOM, por ejemplo) se descartan.</summary>
        public async Task EsperarAsync(byte esperado, TimeSpan espera, CancellationToken cancelacion)
        {
            var hasta = DateTime.UtcNow + espera;
            while (DateTime.UtcNow < hasta)
            {
                if (await SiguienteAsync(hasta - DateTime.UtcNow, cancelacion) == esperado)
                    return;
            }

            throw new TimeoutException($"El terminal no respondió 0x{esperado:X2}.");
        }

        /// <summary>
        /// La respuesta termina con ETX y su LRC; si el terminal no la enmarca, con EOT o con un silencio corto después de los datos.
        /// </summary>
        public async Task<string> LeerTramaAsync(TimeSpan espera, CancellationToken cancelacion)
        {
            var trama = new StringBuilder();
            var hasta = DateTime.UtcNow + espera;
            while (true)
            {
                var restante = trama.Length == 0 ? hasta - DateTime.UtcNow : SilencioFinTrama;
                if (restante <= TimeSpan.Zero)
                    throw new TimeoutException("El terminal no envió la respuesta.");

                if (await SiguienteAsync(restante, cancelacion) is not { } valor)
                {
                    if (trama.Length == 0)
                        throw new TimeoutException("El terminal no envió la respuesta.");
                    return trama.ToString();
                }

                switch (valor)
                {
                    case ProtocoloCardNet.Stx:
                        trama.Clear();
                        break;
                    case ProtocoloCardNet.Etx:
                        await SiguienteAsync(SilencioFinTrama, cancelacion); // LRC
                        return trama.ToString();
                    case ProtocoloCardNet.Eot when trama.Length > 0:
                        return trama.ToString();
                    case ProtocoloCardNet.Fs:
                        trama.Append((char)valor);
                        break;
                    case < 0x20:
                        break; // ACK, EOM y demás controles antes de los datos
                    default:
                        trama.Append((char)valor);
                        break;
                }
            }
        }

        public async Task DescartarAsync(TimeSpan espera, CancellationToken cancelacion)
        {
            try
            {
                while (await SiguienteAsync(espera, cancelacion) is { } valor && valor != ProtocoloCardNet.Eom)
                {
                }
            }
            catch (IOException)
            {
                // El terminal cierra la conexión al terminar: es lo esperado.
            }
        }
    }
}

/// <summary>Mensajes de la caja al terminal CardNet y lectura de sus respuestas, sin red: se prueba por separado.</summary>
internal static class ProtocoloCardNet
{
    public const byte Stx = 0x02;
    public const byte Etx = 0x03;
    public const byte Eot = 0x04;
    public const byte Enq = 0x05;
    public const byte Ack = 0x06;
    public const byte Nak = 0x15;
    public const byte Syn = 0x16;
    public const byte Eom = 0x19;
    public const byte Fs = 0x1C;

    public const string RespuestaNak = "NAK";

    private static readonly string Separador = ((char)Fs).ToString();

    public static string Consulta() => "CS00" + Separador;

    /// <param name="referenciaVenta">Número de la transacción de la caja; al terminal van sus últimos 6 dígitos como ticket.</param>
    public static string Venta(decimal monto, decimal impuesto, string referenciaVenta, string? idMultiMerchant) =>
        Armar("CN00", idMultiMerchant, Importe(monto), Importe(impuesto), Importe(0m), Ticket(referenciaVenta));

    /// <param name="referenciaTerminal">"host-referencia" guardado al aprobar la venta; nulo si no sirve para anular.</param>
    public static string? Anulacion(string? referenciaTerminal, string? idMultiMerchant)
    {
        var partes = (referenciaTerminal ?? string.Empty).Split('-');
        return partes is [{ Length: 2 } host, { Length: 3 } referencia] && host.All(char.IsAsciiDigit) && referencia.All(char.IsAsciiDigit)
            ? Armar("CN02", idMultiMerchant, host, referencia)
            : null;
    }

    public static string Cierre(string? idMultiMerchant) => Armar("CN01", idMultiMerchant);

    public static ResultadoConsultaTarjeta LeerConsulta(string trama)
    {
        var campos = Campos(trama);
        if (campos.Count < 4 || SoloDigitos(campos[3]) is not { Length: >= 6 } bin)
            return new ResultadoConsultaTarjeta(false, false, null, null, Rechazo(campos, "No se leyó la tarjeta: se canceló o se agotó el tiempo."));

        return new ResultadoConsultaTarjeta(true, false, bin, Texto(campos[1]), null);
    }

    /// <summary>HOST, tipo de tarjeta, modo, tarjeta enmascarada, lote, fecha, hora, nombre, aprobación, terminal, referencia, RRN…</summary>
    public static ResultadoTerminal LeerVenta(string trama)
    {
        var campos = Campos(trama);
        if (campos.Count < 11 || campos[8].Trim() is not { Length: > 0 } aprobacion)
            return new ResultadoTerminal(false, false, null, null, null, Rechazo(campos, "Transacción no aprobada por el terminal."));

        var tarjeta = campos[3].Trim();
        var ultimos = tarjeta.Length >= 4 && tarjeta[^4..].All(char.IsAsciiDigit) ? tarjeta[^4..] : null;
        var host = SoloDigitos(campos[0]).PadLeft(2, '0');
        var referencia = SoloDigitos(campos[10]) is { Length: > 0 } digitos ? digitos[^Math.Min(3, digitos.Length)..].PadLeft(3, '0') : null;

        return new ResultadoTerminal(true, false, aprobacion, ultimos, Texto(campos[1]), "Aprobada",
            referencia is null || host.Length != 2 ? null : $"{host}-{referencia}");
    }

    public static ResultadoTerminal LeerAnulacion(string trama, string aprobacion)
    {
        var campos = Campos(trama);
        return campos is ["00", ..]
            ? new ResultadoTerminal(true, false, aprobacion, null, null, "Anulada en el terminal.")
            : new ResultadoTerminal(false, false, null, null, null, Rechazo(campos, "El terminal no anuló la transacción."));
    }

    /// <summary>
    /// Un grupo por host cerrado: resultado, host, comercio, terminal, lote, fecha, hora, cantidad, monto e ITBIS de devoluciones,
    /// cantidad de ventas, total, ITBIS y otros impuestos. Con cierre automático solo llegan resultado, host y lote.
    /// </summary>
    public static ResultadoLoteTerminal LeerCierre(string trama)
    {
        if (trama.Contains("VAC", StringComparison.OrdinalIgnoreCase))
            return new ResultadoLoteTerminal(true, "Lotes vacíos: el terminal no tenía transacciones.");

        var campos = Campos(trama);
        if (campos.Count < 3)
            return new ResultadoLoteTerminal(false, Rechazo(campos, "El terminal no cerró el lote."));

        const int completo = 14;
        var correcto = true;
        string? lote = null;
        var transacciones = 0;
        var monto = 0m;
        for (var i = 0; i < campos.Count;)
        {
            var resultado = campos[i];
            correcto &= resultado == "00";
            if (campos.Count - i >= completo)
            {
                lote ??= campos[i + 4];
                transacciones += int.TryParse(campos[i + 10], NumberStyles.Integer, CultureInfo.InvariantCulture, out var cantidad) ? cantidad : 0;
                monto += LeerImporte(campos[i + 11]);
                i += completo;
            }
            else
            {
                lote ??= campos.Count - i >= 3 ? campos[i + 2] : null;
                i += 3;
            }

            if (resultado != "00")
                break;
        }

        var mensaje = campos[0] switch
        {
            "00" when correcto => "Lote cerrado.",
            "01" => "El cierre del lote falló: intente de nuevo.",
            "02" => "El terminal informa lote duplicado.",
            _ => Rechazo(campos, "El terminal no cerró el lote."),
        };
        return new ResultadoLoteTerminal(correcto, mensaje, lote, transacciones, monto);
    }

    /// <summary>Monto sin punto decimal, 12 posiciones: los dos últimos dígitos son los centavos.</summary>
    public static string Importe(decimal valor) =>
        ((long)decimal.Round(Math.Max(0m, valor) * 100m, 0, MidpointRounding.AwayFromZero)).ToString("D12", CultureInfo.InvariantCulture);

    private static decimal LeerImporte(string valor) =>
        long.TryParse(SoloDigitos(valor), NumberStyles.Integer, CultureInfo.InvariantCulture, out var centavos) ? centavos / 100m : 0m;

    private static string Ticket(string referencia)
    {
        var digitos = SoloDigitos(referencia);
        return (digitos.Length > 6 ? digitos[^6..] : digitos).PadLeft(6, '0');
    }

    private static string Armar(string tipo, string? idMultiMerchant, params string[] campos)
    {
        var todos = new List<string> { tipo };
        if (idMultiMerchant is { Length: > 0 })
            todos.Add(SoloDigitos(idMultiMerchant).PadLeft(2, '0'));
        todos.AddRange(campos);
        return string.Join(Separador, todos) + Separador;
    }

    /// <summary>Separa los campos quitando el marco STX/ETX/LRC y los controles; se descartan los vacíos del final.</summary>
    private static List<string> Campos(string trama)
    {
        var limpia = trama;
        if (limpia.IndexOf((char)Stx) is >= 0 and var inicio)
            limpia = limpia[(inicio + 1)..];
        if (limpia.IndexOf((char)Etx) is >= 0 and var fin)
            limpia = limpia[..fin];

        var campos = new string(limpia.Where(c => c == (char)Fs || c >= ' ').ToArray()).Split((char)Fs).ToList();
        while (campos.Count > 0 && campos[^1].Trim().Length == 0)
            campos.RemoveAt(campos.Count - 1);
        return campos.Select(c => c.TrimEnd()).ToList();
    }

    private static string Rechazo(IReadOnlyList<string> campos, string predeterminado) =>
        campos.Count == 0 ? predeterminado : campos[0].Trim() switch
        {
            "99" => predeterminado,
            "40" => "El terminal no tiene habilitada esta operación (código 40).",
            RespuestaNak => "El terminal rechazó el mensaje de la caja (NAK).",
            var codigo => $"{predeterminado} Respuesta del terminal: {codigo}.",
        };

    private static string? Texto(string valor) => valor.Trim() is { Length: > 0 } texto ? texto : null;

    private static string SoloDigitos(string? valor) => new((valor ?? string.Empty).Where(char.IsAsciiDigit).ToArray());
}
