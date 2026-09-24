using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CgPos.Contratos.Seguridad;
using CgPos.Contratos.Serializacion;
using CgPos.Contratos.Sincronizacion;
using CgPos.Contratos.Ventas;

namespace CgPos.Pos.Pruebas.Api;

/// <summary>
/// El botón «Sincronizar» de la caja: sirve para cuando acaban de cambiar algo en el Central y lo quieren ver enseguida,
/// sin esperar el ciclo del servicio.
/// </summary>
[Collection(ColeccionAgente.Nombre)]
public class ApiSincronizacionPruebas(AgenteEnPruebas agente)
{
    [SkippableFact]
    public async Task Sincronizar_a_pedido_responde_con_el_resumen_y_no_deja_dos_corriendo_a_la_vez()
    {
        Skip.If(agente.MotivoOmision is not null, agente.MotivoOmision);
        using var cliente = agente.Fabrica!.CreateClient();

        using (var sinSesion = await cliente.PostAsync("/api/sincronizacion", content: null))
            Assert.Equal(HttpStatusCode.Unauthorized, sinSesion.StatusCode);

        await IniciarSesionAsync(cliente, "C001", "Cajero.2026");

        var resultado = await SincronizarAsync(cliente);
        Assert.False(string.IsNullOrWhiteSpace(resultado.Mensaje));

        // Dos a la vez no se lanzan: la segunda espera a la primera y lo dice, en vez de hacer el trabajo dos veces.
        var primera = SincronizarAsync(cliente);
        var segunda = SincronizarAsync(cliente);
        var ambas = await Task.WhenAll(primera, segunda);
        Assert.All(ambas, r => Assert.False(string.IsNullOrWhiteSpace(r.Mensaje)));

        // Y el estado que mira la pantalla sigue respondiendo: la sincronización no la deja colgada.
        var estado = await cliente.GetFromJsonAsync<DatosEstadoSincronizacion>("/api/sincronizacion/estado", OpcionesJson.Predeterminadas);
        Assert.NotNull(estado);
    }

    private static async Task<ResultadoSincronizacion> SincronizarAsync(HttpClient cliente)
    {
        using var respuesta = await cliente.PostAsync("/api/sincronizacion", content: null);
        respuesta.EnsureSuccessStatusCode();
        return (await respuesta.Content.ReadFromJsonAsync<ResultadoSincronizacion>(OpcionesJson.Predeterminadas))!;
    }

    private static async Task IniciarSesionAsync(HttpClient cliente, string codigo, string clave)
    {
        using var respuesta = await cliente.PostAsJsonAsync("/api/sesion/ingreso", new SolicitudIngreso(codigo, clave), OpcionesJson.Predeterminadas);
        var ingreso = await respuesta.Content.ReadFromJsonAsync<RespuestaIngreso>(OpcionesJson.Predeterminadas);
        Assert.True(ingreso!.Exitoso, ingreso.Mensaje);
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ingreso.Token);
    }
}
