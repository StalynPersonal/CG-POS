using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CgPos.Contratos.Catalogo;
using CgPos.Contratos.Seguridad;
using CgPos.Contratos.Serializacion;

namespace CgPos.Pos.Pruebas.Api;

/// <summary>Todas las pruebas de API comparten un solo Agente en memoria (Serilog no admite varios anfitriones en el mismo proceso).</summary>
[CollectionDefinition(Nombre)]
public sealed class ColeccionAgente : ICollectionFixture<AgenteEnPruebas>
{
    public const string Nombre = "Agente en pruebas";
}

[Collection(ColeccionAgente.Nombre)]
public class ApiCatalogoPruebas(AgenteEnPruebas agente)
{
    [SkippableFact]
    public async Task Articulo_por_codigo_requiere_sesion_y_trae_precio_e_impuesto()
    {
        Skip.If(agente.MotivoOmision is not null, agente.MotivoOmision);
        using var cliente = agente.Fabrica!.CreateClient();

        using var sinSesion = await cliente.GetAsync("/api/articulos/codigo/7891114119695");
        Assert.Equal(HttpStatusCode.Unauthorized, sinSesion.StatusCode);

        await IniciarSesionAsync(cliente, "C001", "Cajero.2026");
        var articulo = await cliente.GetFromJsonAsync<DatosArticuloVenta>("/api/articulos/codigo/7891114119695", OpcionesJson.Predeterminadas);

        Assert.Contains("Cincel", articulo!.Descripcion);
        Assert.Equal(850.00m, articulo.PrecioDetalle);
        Assert.Equal(18m, articulo.PorcentajeImpuesto);

        using var inexistente = await cliente.GetAsync("/api/articulos/codigo/NO-EXISTE");
        Assert.Equal(HttpStatusCode.NotFound, inexistente.StatusCode);
    }

    [SkippableFact]
    public async Task Busqueda_documento_y_catalogo_de_cobro_desde_los_maestros_de_desarrollo()
    {
        Skip.If(agente.MotivoOmision is not null, agente.MotivoOmision);
        using var cliente = agente.Fabrica!.CreateClient();
        await IniciarSesionAsync(cliente, "C001", "Cajero.2026");

        var busqueda = await cliente.GetFromJsonAsync<List<DatosArticuloResumen>>("/api/articulos?texto=cemento%20gris", OpcionesJson.Predeterminadas);
        Assert.Contains(busqueda!, a => a.Codigo == "CEM-425");

        var documento = await cliente.GetFromJsonAsync<DatosConsultaDocumento>("/api/documentos/131-24679-6", OpcionesJson.Predeterminadas);
        Assert.True(documento!.EnPadron);
        Assert.Equal("CONSTRUCTORA EJEMPLO SRL", documento.RazonSocial);
        Assert.Equal("Constructora Ejemplo SRL", documento.Cliente!.Nombre);

        var cobro = await cliente.GetFromJsonAsync<DatosCatalogoCobro>("/api/catalogos/cobro", OpcionesJson.Predeterminadas);
        Assert.Equal("EFE", cobro!.FormasPago[0].Codigo);
        Assert.Contains(cobro.Denominaciones, d => d.Moneda == "DOP" && d.Valor == 2000m);
    }

    [SkippableFact]
    public async Task Importar_padron_requiere_permiso_de_administracion()
    {
        Skip.If(agente.MotivoOmision is not null, agente.MotivoOmision);
        using var cliente = agente.Fabrica!.CreateClient();
        await IniciarSesionAsync(cliente, "C001", "Cajero.2026");

        using var contenido = new StringContent("131246796|CONSTRUCTORA EJEMPLO SRL||||||||ACTIVO|NORMAL");
        using var respuesta = await cliente.PostAsync("/api/maestros/padron-dgii", contenido);

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    private static async Task IniciarSesionAsync(HttpClient cliente, string codigo, string clave)
    {
        using var respuesta = await cliente.PostAsJsonAsync("/api/sesion/ingreso", new SolicitudIngreso(codigo, clave), OpcionesJson.Predeterminadas);
        var ingreso = await respuesta.Content.ReadFromJsonAsync<RespuestaIngreso>(OpcionesJson.Predeterminadas);
        Assert.True(ingreso!.Exitoso, ingreso.Mensaje);
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ingreso.Token);
    }
}
