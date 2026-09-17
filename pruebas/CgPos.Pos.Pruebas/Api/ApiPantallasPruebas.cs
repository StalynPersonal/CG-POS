using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CgPos.Contratos.Catalogo;
using CgPos.Contratos.Pantallas;
using CgPos.Contratos.Seguridad;
using CgPos.Contratos.Serializacion;

namespace CgPos.Pos.Pruebas.Api;

[Collection(ColeccionAgente.Nombre)]
public class ApiPantallasPruebas(AgenteEnPruebas agente)
{
    [SkippableFact]
    public async Task Pantalla_del_cliente_obtiene_publicidad_y_se_conecta_al_hub_sin_sesion()
    {
        Skip.If(agente.MotivoOmision is not null, agente.MotivoOmision);
        using var cliente = agente.Fabrica!.CreateClient();

        var publicidad = await cliente.GetFromJsonAsync<DatosPublicidad>("/api/pantallas/publicidad", OpcionesJson.Predeterminadas);
        Assert.NotNull(publicidad);
        Assert.True(publicidad.SegundosPorImagen > 0);
        Assert.False(string.IsNullOrWhiteSpace(publicidad.EmpresaNombre));

        // Negociación de SignalR: el hub responde sin token.
        using var negociacion = await cliente.PostAsync($"{ContratoPantallaCliente.RutaHub}/negotiate?negotiateVersion=1", null);
        Assert.Equal(HttpStatusCode.OK, negociacion.StatusCode);
    }

    [SkippableFact]
    public async Task Catalogo_en_mosaicos_lista_solo_articulos_marcados_para_catalogo()
    {
        Skip.If(agente.MotivoOmision is not null, agente.MotivoOmision);
        using var cliente = agente.Fabrica!.CreateClient();

        using var ingreso = await cliente.PostAsJsonAsync("/api/sesion/ingreso", new SolicitudIngreso("C001", "Cajero.2026"), OpcionesJson.Predeterminadas);
        var sesion = await ingreso.Content.ReadFromJsonAsync<RespuestaIngreso>(OpcionesJson.Predeterminadas);
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", sesion!.Token);

        var catalogo = await cliente.GetFromJsonAsync<List<DatosArticuloResumen>>("/api/articulos/catalogo", OpcionesJson.Predeterminadas);

        Assert.Contains(catalogo!, a => a.Descripcion == "Tomate de ensalada");
        Assert.DoesNotContain(catalogo!, a => a.Codigo == "CEM-425");
    }
}
