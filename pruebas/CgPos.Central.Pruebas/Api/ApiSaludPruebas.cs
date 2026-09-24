using System.Net;
using System.Text.Json;
using CgPos.Central.Pruebas.Soporte;

namespace CgPos.Central.Pruebas.Api;

/// <summary>
/// /salud es lo primero que abre quien instala o soporta el Central. Responde sin sesión, así que dice si el servidor está
/// bien y contra qué base trabaja, y nada que sirva para atacarlo.
/// </summary>
[Collection(ColeccionCentral.Nombre)]
public class ApiSaludPruebas(CentralEnPruebas central)
{
    [SkippableFact]
    public async Task El_diagnostico_responde_sin_sesion_y_dice_si_el_chequeador_esta_encendido()
    {
        Skip.If(central.MotivoOmision is not null, central.MotivoOmision);
        using var cliente = central.CrearCliente();

        using var respuesta = await cliente.GetAsync("/salud");
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);

        using var cuerpo = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
        var raiz = cuerpo.RootElement;
        Assert.Equal("Correcto", raiz.GetProperty("estado").GetString());
        Assert.False(string.IsNullOrWhiteSpace(raiz.GetProperty("baseDatos").GetProperty("nombre").GetString()));

        // El chequeador viene apagado: la pantalla del pasillo no responde hasta que se encienda en Parámetros.
        var chequeador = raiz.GetProperty("verificaciones").GetProperty("Chequeador de precios");
        Assert.Equal("Correcto", chequeador.GetProperty("estado").GetString());
    }
}
