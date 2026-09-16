using CgPos.Pos.Infraestructura.Perifericos.Balanzas;
using CgPos.Pos.Infraestructura.Perifericos.Terminales;
using Microsoft.Extensions.Configuration;

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
    public void El_perfil_del_terminal_sale_del_modelo_y_sus_mensajes_se_ajustan_por_configuracion()
    {
        var cardnet = PerfilesTerminal.Desde(Configuracion(("Perifericos:Terminal:Modelo", "CardNet Ingenico Lane/7000")));
        Assert.Equal("CardNet Ingenico Lane/7000", cardnet.Nombre);
        Assert.Equal(TransporteTerminal.Socket, cardnet.Transporte);

        // Cuando CardNet entregue su documento, basta con ajustar las plantillas y el patrón en la configuración.
        var ajustado = PerfilesTerminal.Desde(Configuracion(
            ("Perifericos:Terminal:Modelo", "CardNet"),
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
        var perfil = PerfilesTerminal.CardNetLane;

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
}
