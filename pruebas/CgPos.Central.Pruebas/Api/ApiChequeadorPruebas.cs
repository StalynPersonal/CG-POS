using System.Net;
using System.Net.Http.Json;
using CgPos.Central.Aplicacion.Organizacion;
using CgPos.Central.Pruebas.Soporte;
using CgPos.Contratos.Catalogo;
using CgPos.Contratos.Central;
using CgPos.Contratos.Serializacion;

namespace CgPos.Central.Pruebas.Api;

[Collection(ColeccionCentral.Nombre)]
public class ApiChequeadorPruebas(CentralEnPruebas central)
{
    [SkippableFact]
    public async Task El_chequeador_responde_precio_y_ofertas_sin_sesion_y_solo_si_esta_encendido()
    {
        Skip.If(central.MotivoOmision is not null, central.MotivoOmision);
        using var cliente = central.CrearCliente();
        var admin = await CentralEnPruebas.TokenAdministradorAsync(cliente);
        var sufijo = Guid.NewGuid().ToString("N")[..5].ToUpperInvariant();
        var barras = $"98{Random.Shared.NextInt64(1_000_000_000, 9_999_999_999)}";

        // Un artículo con su código de barras y su precio publicado, sobre los maestros que ya trae el Central.
        var categoria = (await ListarAsync<DatosMaestroCentral<CategoriaCarga>>(cliente, admin, "/api/maestros/categorias")).First(c => c.Dato.Activa).Dato;
        var unidad = (await ListarAsync<DatosMaestroCentral<UnidadMedidaCarga>>(cliente, admin, "/api/maestros/unidades-medida")).First().Dato;
        var impuesto = (await ListarAsync<DatosMaestroCentral<ImpuestoCarga>>(cliente, admin, "/api/maestros/impuestos")).First(i => i.Dato.Activo && i.Dato.Porcentaje == 18m).Dato;

        var articulo = new ArticuloCarga($"CHQ{sufijo}", $"Cemento chequeador {sufijo}", categoria.DepartamentoCodigo, unidad.Codigo, impuesto.Codigo, 500m,
            PrecioMayor: 470m, CantidadMinimaMayor: 10m, CodigosBarras: [barras], CategoriaCodigo: categoria.Codigo);
        var guardado = await GuardarAsync(cliente, admin, "/api/maestros/articulos", articulo);
        Assert.True(guardado.Exitosa, guardado.Mensaje);

        // Apagado no responde nada, aunque el artículo exista.
        await central.CambiarParametroAsync(ClavesParametrosCentral.ChequeadorHabilitado, "false");
        Assert.False((await ObtenerAsync<DatosConfiguracionChequeador>(cliente, "/api/chequeador/configuracion")).Habilitado);
        using (var apagado = await cliente.GetAsync($"/api/chequeador/articulos/{articulo.Codigo}"))
            Assert.Equal(HttpStatusCode.NotFound, apagado.StatusCode);

        // Encendido responde sin token, por código interno y por código de barras.
        await central.CambiarParametroAsync(ClavesParametrosCentral.ChequeadorHabilitado, "true");
        var configuracion = await ObtenerAsync<DatosConfiguracionChequeador>(cliente, "/api/chequeador/configuracion");
        Assert.True(configuracion.Habilitado);
        Assert.NotEmpty(configuracion.Sucursales);

        var porCodigo = await ObtenerAsync<DatosPrecioChequeador>(cliente, $"/api/chequeador/articulos/{articulo.Codigo}");
        // El maestro guarda el precio sin ITBIS; el cliente ve lo que paga: 500 + 90 y 470 + 84.60.
        Assert.Equal((articulo.Codigo, articulo.Descripcion, 590m), (porCodigo.Codigo, porCodigo.Descripcion, porCodigo.Precio));
        Assert.Equal((554.60m, 10m), (porCodigo.PrecioMayor, porCodigo.CantidadMinimaMayor));

        var porBarras = await ObtenerAsync<DatosPrecioChequeador>(cliente, $"/api/chequeador/articulos/{barras}");
        Assert.Equal(articulo.Codigo, porBarras.Codigo);

        // Un código que no existe se informa como no encontrado, sin filtrar nada del negocio.
        using var inexistente = await cliente.GetAsync("/api/chequeador/articulos/NOEXISTE00000");
        Assert.Equal(HttpStatusCode.NotFound, inexistente.StatusCode);
    }

    private static async Task<IReadOnlyList<T>> ListarAsync<T>(HttpClient cliente, string token, string ruta)
    {
        using var respuesta = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Get, ruta, token));
        respuesta.EnsureSuccessStatusCode();
        return (await respuesta.Content.ReadFromJsonAsync<List<T>>(OpcionesJson.Predeterminadas))!;
    }

    private static async Task<RespuestaAdministracion> GuardarAsync<T>(HttpClient cliente, string token, string ruta, T cuerpo)
    {
        using var respuesta = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Post, ruta, token, cuerpo));
        return (await respuesta.Content.ReadFromJsonAsync<RespuestaAdministracion>(OpcionesJson.Predeterminadas))!;
    }

    /// <summary>El chequeador no manda token: la pantalla de la tienda no tiene sesión.</summary>
    private static async Task<T> ObtenerAsync<T>(HttpClient cliente, string ruta)
    {
        using var respuesta = await cliente.GetAsync(ruta);
        respuesta.EnsureSuccessStatusCode();
        return (await respuesta.Content.ReadFromJsonAsync<T>(OpcionesJson.Predeterminadas))!;
    }
}
