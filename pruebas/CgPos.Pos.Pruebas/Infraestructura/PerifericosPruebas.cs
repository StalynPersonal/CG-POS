using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using CgPos.Pos.Infraestructura.Perifericos.Balanzas;
using CgPos.Pos.Infraestructura.Perifericos.Terminales;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace CgPos.Pos.Pruebas.Infraestructura;

/// <summary>
/// Los periféricos se cambian por configuración: el perfil del modelo dice cómo se le habla y cómo se lee su respuesta.
/// Aquí se prueba esa parte, que es la que cambia al sustituir un equipo por otro.
/// </summary>
public class PerifericosPruebas
{
    private static IConfiguration Configuracion(params (string Clave, string Valor)[] valores) =>
        new ConfigurationBuilder().AddInMemoryCollection(valores.Select(v => new KeyValuePair<string, string?>(v.Clave, v.Valor))).Build();

    [Fact]
    public void El_perfil_de_la_balanza_sale_del_modelo_y_la_configuracion_lo_puede_ajustar()
    {
        var magellan = PerfilesBalanza.Desde(Configuracion(("Perifericos:Balanza:Modelo", "Datalogic Magellan 9556")));
        Assert.Equal("Datalogic Magellan", magellan.Nombre);
        Assert.Equal(("S", 9600, 7), (magellan.Comando, magellan.Baudios, magellan.BitsDatos));

        // Una balanza parecida no obliga a tocar el código: se ajusta lo que cambie.
        var ajustada = PerfilesBalanza.Desde(Configuracion(
            ("Perifericos:Balanza:Modelo", "Datalogic Magellan 9556"),
            ("Perifericos:Balanza:Baudios", "19200"),
            ("Perifericos:Balanza:Comando", "W"),
            ("Perifericos:Balanza:Unidad", "KG")));
        Assert.Equal(("W", 19200, "KG"), (ajustada.Comando, ajustada.Baudios, ajustada.UnidadPredeterminada));

        // Sin modelo conocido se usa el perfil genérico.
        Assert.Equal("Genérica", PerfilesBalanza.Desde(Configuracion()).Nombre);
    }

    [Fact]
    public void La_lectura_de_la_balanza_toma_el_peso_la_unidad_y_la_estabilidad_que_informa_el_equipo()
    {
        var perfil = PerfilesBalanza.DatalogicMagellan;

        var estable = BalanzaSerie.Interpretar("S 12.345 LB\r", perfil);
        Assert.NotNull(estable);
        Assert.Equal((true, 12.345m, "LB"), (estable.Estable, estable.Peso, estable.Unidad));

        // El equipo informa que el peso aún se mueve: no se acepta.
        var enMovimiento = BalanzaSerie.Interpretar("M 12.345 LB\r", perfil);
        Assert.NotNull(enMovimiento);
        Assert.False(enMovimiento.Estable);

        // Una balanza que no informa estabilidad entrega el peso tal cual, con la unidad configurada.
        var generica = BalanzaSerie.Interpretar("2,500\r", PerfilesBalanza.Generica with { UnidadPredeterminada = "KG" });
        Assert.NotNull(generica);
        Assert.Equal((true, 2.500m, "KG"), (generica.Estable, generica.Peso, generica.Unidad));

        Assert.Null(BalanzaSerie.Interpretar("ERROR", perfil));
    }

    [Fact]
    public void El_perfil_del_terminal_generico_se_ajusta_por_configuracion()
    {
        var generico = PerfilesTerminal.Desde(Configuracion());
        Assert.Equal("Genérico", generico.Nombre);
        Assert.Equal(TransporteTerminal.Socket, generico.Transporte);

        // Otro modelo de terminal no obliga a tocar el código: se ajustan sus mensajes y su patrón.
        var ajustado = PerfilesTerminal.Desde(Configuracion(
            ("Perifericos:Terminal:Transporte", "Serie"),
            ("Perifericos:Terminal:PlantillaCobro", "0200|{montoCentavos}"),
            ("Perifericos:Terminal:Aprobadas", "00,08")));
        Assert.Equal(TransporteTerminal.Serie, ajustado.Transporte);
        Assert.Equal("0200|{montoCentavos}", ajustado.PlantillaCobro);
        Assert.Equal(["00", "08"], ajustado.Aprobadas);
    }

    [Fact]
    public void La_respuesta_del_terminal_se_lee_segun_el_patron_del_modelo()
    {
        var perfil = PerfilesTerminal.Generico;

        var aprobada = TerminalPagoConectado.Interpretar("00|123456|4242|VISA|Aprobada\r", perfil);
        Assert.Equal(("00", "123456", "4242", "VISA"), (aprobada.Aprobada, aprobada.Aprobacion, aprobada.Digitos, aprobada.Marca));
        Assert.Contains(aprobada.Aprobada, perfil.Aprobadas);

        var declinada = TerminalPagoConectado.Interpretar("05||||Fondos insuficientes\r", perfil);
        Assert.DoesNotContain(declinada.Aprobada, perfil.Aprobadas);
        Assert.Equal("Fondos insuficientes", declinada.Mensaje);

        // Una respuesta que no encaja con el patrón se entrega como mensaje, sin inventar una aprobación.
        var desconocida = TerminalPagoConectado.Interpretar("TIMEOUT", perfil with { PatronRespuesta = @"^(?<aprobada>\d{2})\|(?<aprobacion>.*)$" });
        Assert.Equal(string.Empty, desconocida.Aprobada);
        Assert.Equal("TIMEOUT", desconocida.Mensaje);
    }

    // ---------- CardNet (Ingenico 7000) en modo caja: "Integración CAJA – Modelo POS" ----------

    private const char Fs = (char)0x1C;

    [Fact]
    public void Los_mensajes_a_cardnet_llevan_los_campos_con_su_largo_y_separados_por_fs()
    {
        // Monto, ITBIS y otros impuestos van en 12 posiciones sin punto decimal, y el ticket con los últimos 6 dígitos de la venta.
        Assert.Equal($"CN00{Fs}000000118000{Fs}000000018000{Fs}000000000000{Fs}123456{Fs}",
            ProtocoloCardNet.Venta(1180m, 180m, "01-01-000123456", null));

        // Multi-Merchant, cuando el terminal lo usa, va enseguida del tipo de transacción.
        Assert.Equal($"CN00{Fs}07{Fs}000000010000{Fs}000000000000{Fs}000000000000{Fs}000001{Fs}",
            ProtocoloCardNet.Venta(100m, 0m, "1", "7"));

        Assert.Equal($"CS00{Fs}", ProtocoloCardNet.Consulta());
        Assert.Equal($"CN01{Fs}", ProtocoloCardNet.Cierre(null));

        // La anulación necesita el host y el número de referencia que devolvió la venta aprobada.
        Assert.Equal($"CN02{Fs}06{Fs}001{Fs}", ProtocoloCardNet.Anulacion("06-001", null));
        Assert.Null(ProtocoloCardNet.Anulacion(null, null));
    }

    [Fact]
    public void Las_respuestas_de_cardnet_dan_la_aprobacion_los_ultimos_digitos_y_con_que_anular()
    {
        var aprobada = ProtocoloCardNet.LeerVenta(string.Join(Fs,
            "06", "VISA    ", "D@5", "542298xxxxxx6290", "001", "120310", "101010", "JOAQUIN PRUEBA", "123456", "12345678", "001", "009912345678") + Fs);
        Assert.True(aprobada.Aprobada);
        Assert.Equal(("123456", "6290", "VISA", "06-001"), (aprobada.Aprobacion, aprobada.UltimosDigitos, aprobada.Marca, aprobada.ReferenciaTerminal));

        // Todo rechazo del terminal llega como 99; una operación apagada en el terminal, como 40.
        Assert.False(ProtocoloCardNet.LeerVenta("99").Aprobada);
        Assert.Contains("no tiene habilitada", ProtocoloCardNet.LeerVenta("40").Mensaje);

        // La consulta de tarjeta (CS00) entrega los primeros 8 dígitos: con eso se busca el descuento del banco.
        var consulta = ProtocoloCardNet.LeerConsulta(string.Join(Fs, "06", "VISA", "D@5", "54229810", "101", "JUANA GOMEZ") + Fs);
        Assert.Equal((true, "54229810", "VISA"), (consulta.Leida, consulta.Bin, consulta.Marca));
        Assert.False(ProtocoloCardNet.LeerConsulta("99").Leida);

        // El cierre viene por host, con la cantidad de transacciones y el total depositado.
        var cierre = ProtocoloCardNet.LeerCierre(string.Join(Fs,
            "00", "CREDITO", "349111111000", "12345678", "001", "120310", "220000", "000", "000000000000", "000000000000", "004", "000000118000",
            "000000018000", "000000000000") + Fs);
        Assert.True(cierre.Correcto);
        Assert.Equal(("001", 4, 1180m), (cierre.NumeroLote, cierre.Transacciones, cierre.Monto));
        Assert.True(ProtocoloCardNet.LeerCierre("Lotes Vacíos").Correcto);
        Assert.False(ProtocoloCardNet.LeerCierre($"01{Fs}CREDITO{Fs}001{Fs}").Correcto);
    }

    [Fact]
    public async Task La_caja_conversa_con_el_terminal_cardnet_saludando_y_confirmando_cada_paso()
    {
        await using var pos = new IngenicoSimulado(
            string.Join(Fs, "06", "VISA", "D@5", "54229810", "101", "JUANA GOMEZ") + Fs,
            string.Join(Fs, "06", "VISA    ", "C@5", "542298xxxxxx6290", "001", "120310", "101010", "JUANA GOMEZ", "123456", "12345678", "001",
                "009912345678") + Fs);
        var terminal = new TerminalPagoCardNet(Configuracion(
            ("Perifericos:Terminal:Host", "127.0.0.1"),
            ("Perifericos:Terminal:Puerto", pos.Puerto.ToString(CultureInfo.InvariantCulture))), NullLogger<TerminalPagoCardNet>.Instance);

        var consulta = await terminal.ConsultarTarjetaAsync();
        var cobro = await terminal.CobrarAsync(1180m, 180m, "01-01-000000015");

        Assert.True(terminal.ConsultaTarjeta);
        Assert.Equal("54229810", consulta.Bin);
        Assert.True(cobro.Aprobada, cobro.Mensaje);
        Assert.Equal(("123456", "06-001"), (cobro.Aprobacion, cobro.ReferenciaTerminal));
        Assert.Equal([$"CS00{Fs}", $"CN00{Fs}000000118000{Fs}000000018000{Fs}000000000000{Fs}000015{Fs}"], pos.Recibidos);
    }

    [Fact]
    public async Task Si_el_terminal_cardnet_no_responde_la_caja_lo_informa_sin_conexion()
    {
        var terminal = new TerminalPagoCardNet(Configuracion(
            ("Perifericos:Terminal:Host", "127.0.0.1"),
            ("Perifericos:Terminal:Puerto", "1")), NullLogger<TerminalPagoCardNet>.Instance);

        var cobro = await terminal.CobrarAsync(100m, 0m, "01-01-000000001");

        Assert.False(cobro.Aprobada);
        Assert.True(cobro.SinConexion);
    }

    /// <summary>Terminal CardNet de mentira: hace el saludo del documento y contesta lo que se le indique, en orden.</summary>
    private sealed class IngenicoSimulado : IAsyncDisposable
    {
        private readonly TcpListener _escucha = new(IPAddress.Loopback, 0);
        private readonly Queue<string> _respuestas;
        private readonly Task _atencion;

        public IngenicoSimulado(params string[] respuestas)
        {
            _respuestas = new Queue<string>(respuestas);
            _escucha.Start();
            Puerto = ((IPEndPoint)_escucha.LocalEndpoint).Port;
            _atencion = AtenderAsync();
        }

        public int Puerto { get; }

        public List<string> Recibidos { get; } = [];

        public async ValueTask DisposeAsync()
        {
            _escucha.Stop();
            try
            {
                await _atencion;
            }
            catch (Exception excepcion) when (excepcion is SocketException or ObjectDisposedException or InvalidOperationException)
            {
            }
        }

        private async Task AtenderAsync()
        {
            while (_respuestas.Count > 0)
            {
                using var cliente = await _escucha.AcceptTcpClientAsync();
                await using var flujo = cliente.GetStream();

                await EsperarAsync(flujo, ProtocoloCardNet.Enq);
                await EscribirAsync(flujo, ProtocoloCardNet.Ack);
                await EsperarAsync(flujo, ProtocoloCardNet.Syn);
                await EscribirAsync(flujo, ProtocoloCardNet.Eom);
                await EscribirAsync(flujo, ProtocoloCardNet.Enq);

                Recibidos.Add(await LeerMensajeAsync(flujo));
                await EscribirAsync(flujo, ProtocoloCardNet.Ack);
                await flujo.WriteAsync(Encoding.ASCII.GetBytes(_respuestas.Dequeue()));
                await EsperarAsync(flujo, ProtocoloCardNet.Ack);
                await EscribirAsync(flujo, ProtocoloCardNet.Eot);
                await EscribirAsync(flujo, ProtocoloCardNet.Eom);
            }
        }

        private static Task EscribirAsync(NetworkStream flujo, byte valor) => flujo.WriteAsync(new[] { valor }).AsTask();

        private static async Task EsperarAsync(NetworkStream flujo, byte esperado)
        {
            var bufer = new byte[1];
            while (await flujo.ReadAsync(bufer) == 1)
            {
                if (bufer[0] == esperado)
                    return;
            }

            throw new InvalidOperationException($"La caja no envió 0x{esperado:X2}.");
        }

        private static async Task<string> LeerMensajeAsync(NetworkStream flujo)
        {
            var mensaje = new StringBuilder();
            var bufer = new byte[256];
            while (await flujo.ReadAsync(bufer) is > 0 and var leidos)
            {
                mensaje.Append(Encoding.ASCII.GetString(bufer, 0, leidos));
                if (bufer[leidos - 1] == ProtocoloCardNet.Fs)
                    break;
            }

            return mensaje.ToString();
        }
    }
}
