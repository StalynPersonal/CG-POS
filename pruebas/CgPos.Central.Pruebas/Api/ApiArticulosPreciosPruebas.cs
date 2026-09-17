using System.Net;
using System.Net.Http.Json;
using CgPos.Central.Pruebas.Soporte;
using CgPos.Contratos.Catalogo;
using CgPos.Contratos.Central;
using CgPos.Contratos.Serializacion;
using CgPos.Contratos.Sincronizacion;
using CgPos.Dominio.Seguridad;

namespace CgPos.Central.Pruebas.Api;

[Collection(ColeccionCentral.Nombre)]
public class ApiArticulosPreciosPruebas(CentralEnPruebas central)
{
    [SkippableFact]
    public async Task Los_articulos_se_buscan_conservan_sus_precios_y_los_precios_bajan_con_su_vigencia()
    {
        Skip.If(central.MotivoOmision is not null, central.MotivoOmision);
        using var cliente = central.CrearCliente();
        var admin = await CentralEnPruebas.TokenAdministradorAsync(cliente);
        var tokenCaja = await CentralEnPruebas.TokenCajaAsync(cliente, CentralEnPruebas.CajaUno);
        var categoria = (await ListarAsync<DatosMaestroCentral<CategoriaCarga>>(cliente, admin, "/api/maestros/categorias")).First(c => c.Dato.Activa).Dato;
        var departamento = (await ListarAsync<DatosMaestroCentral<DepartamentoCarga>>(cliente, admin, "/api/maestros/departamentos")).First(f => f.Dato.Id == categoria.DepartamentoId).Dato;
        var unidad = (await ListarAsync<DatosMaestroCentral<UnidadMedidaCarga>>(cliente, admin, "/api/maestros/unidades-medida")).First().Dato;
        var impuesto = (await ListarAsync<DatosMaestroCentral<ImpuestoCarga>>(cliente, admin, "/api/maestros/impuestos")).First(i => i.Dato.Activo).Dato;
        var sufijo = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        var barras = $"99{Random.Shared.NextInt64(1_000_000_000, 9_999_999_999)}";

        var articulo = new ArticuloCarga(Guid.CreateVersion7(), $"A{sufijo}", $"Jabón de prueba {sufijo}", departamento.Id, unidad.Id, impuesto.Id, 150m,
            PrecioMayor: 140m, CantidadMinimaMayor: 12m, CodigosBarras: [barras], CategoriaId: categoria.Id);
        var creado = await EnviarAsync(cliente, admin, $"/api/maestros/articulos/{articulo.Id}", articulo);
        Assert.True(creado.Cuerpo!.Exitosa, creado.Cuerpo.Mensaje);

        // Se busca por código de barras y por descripción sin importar mayúsculas ni acentos; los nombres de las propiedades del registro no cuentan.
        var porBarras = await ObtenerAsync<PaginaMaestros<ArticuloCarga>>(cliente, admin, $"/api/maestros/articulos?buscar={barras}");
        Assert.Equal(articulo.Id, Assert.Single(porBarras.Elementos).Dato.Id);
        Assert.Equal(1, porBarras.Total);
        var porDescripcion = await ObtenerAsync<PaginaMaestros<ArticuloCarga>>(cliente, admin, $"/api/precios/articulos?buscar={Uri.EscapeDataString($"JABON de prueba {sufijo.ToLowerInvariant()}")}");
        Assert.Contains(porDescripcion.Elementos, e => e.Dato.Id == articulo.Id);
        Assert.Equal(0, (await ObtenerAsync<PaginaMaestros<ArticuloCarga>>(cliente, admin, "/api/maestros/articulos?buscar=preciodetalle")).Total);

        // Desde maestros no se cambian los precios; el código no cambia y un código de barras es de un solo artículo.
        Assert.True((await EnviarAsync(cliente, admin, $"/api/maestros/articulos/{articulo.Id}", articulo with { Descripcion = "Jabón renombrado", PrecioDetalle = 1m })).Cuerpo!.Exitosa);
        var renombrado = Assert.Single((await ObtenerAsync<PaginaMaestros<ArticuloCarga>>(cliente, admin, $"/api/maestros/articulos?buscar={barras}")).Elementos).Dato;
        Assert.Equal(("Jabón renombrado", 150m), (renombrado.Descripcion, renombrado.PrecioDetalle));
        Assert.Contains("No se puede cambiar el código del artículo",
            (await EnviarAsync(cliente, admin, $"/api/maestros/articulos/{articulo.Id}", articulo with { Codigo = $"B{sufijo}" })).Cuerpo!.Mensaje);
        var otro = articulo with { Id = Guid.CreateVersion7(), Codigo = $"C{sufijo}" };
        Assert.Contains("ya lo usa el artículo", (await EnviarAsync(cliente, admin, $"/api/maestros/articulos/{otro.Id}", otro)).Cuerpo!.Mensaje);

        var marca = (await BajarAsync(cliente, tokenCaja, 0)).Hasta;
        var vigencia = new DateTimeOffset(2026, 10, 1, 6, 0, 0, TimeSpan.FromHours(-4));
        var precios = await EnviarAsync(cliente, admin, $"/api/precios/articulos/{articulo.Id}", new SolicitudPreciosArticulo(175m, 160m, 12m, 100m, 90m, vigencia));
        Assert.True(precios.Cuerpo!.Exitosa, precios.Cuerpo.Mensaje);

        var bajado = Assert.Single((await BajarAsync(cliente, tokenCaja, marca)).Maestros!.Articulos!, a => a.Id == articulo.Id);
        Assert.Equal((175m, 160m, 100m, 90m, "Jabón renombrado"), (bajado.PrecioDetalle, bajado.PrecioMayor!.Value, bajado.PrecioMinimo!.Value, bajado.Costo!.Value, bajado.Descripcion));
        Assert.Equal(vigencia, bajado.PreciosVigentesDesde);

        var sinPrecio = await EnviarAsync(cliente, admin, $"/api/precios/articulos/{articulo.Id}", new SolicitudPreciosArticulo(0m));
        Assert.Equal(HttpStatusCode.BadRequest, sinPrecio.Estado);
        Assert.Contains("precio detalle", sinPrecio.Cuerpo!.Mensaje);
        Assert.Equal(HttpStatusCode.NotFound, (await EnviarAsync(cliente, admin, $"/api/precios/articulos/{Guid.CreateVersion7()}", new SolicitudPreciosArticulo(10m))).Estado);
    }

    [SkippableFact]
    public async Task Los_topes_validan_su_alcance_y_no_se_repiten_por_nivel()
    {
        Skip.If(central.MotivoOmision is not null, central.MotivoOmision);
        using var cliente = central.CrearCliente();
        var admin = await CentralEnPruebas.TokenAdministradorAsync(cliente);
        var sufijo = Guid.NewGuid().ToString("N")[..5].ToUpperInvariant();
        var departamento = new DepartamentoCarga(Guid.CreateVersion7(), $"T{sufijo}", $"Departamento topes {sufijo}");
        Assert.True((await EnviarAsync(cliente, admin, $"/api/maestros/departamentos/{departamento.Id}", departamento)).Cuerpo!.Exitosa);

        var tope = new TopeDescuentoCarga(Guid.CreateVersion7(), 2, 15m, null, DepartamentoId: departamento.Id);
        var guardado = await EnviarAsync(cliente, admin, $"/api/precios/topes/{tope.Id}", tope);
        Assert.True(guardado.Cuerpo!.Exitosa, guardado.Cuerpo.Mensaje);
        Assert.True((await EnviarAsync(cliente, admin, $"/api/precios/topes/{tope.Id}", tope with { PorcentajeMaximo = 20m })).Cuerpo!.Exitosa);

        var repetido = tope with { Id = Guid.CreateVersion7(), PorcentajeMaximo = 5m };
        Assert.Contains("Ya hay un tope de descuento de nivel 2 para ese departamento", (await EnviarAsync(cliente, admin, $"/api/precios/topes/{repetido.Id}", repetido)).Cuerpo!.Mensaje);
        var sinDepartamento = tope with { Id = Guid.CreateVersion7(), DepartamentoId = Guid.CreateVersion7() };
        Assert.Contains("departamento inexistente", (await EnviarAsync(cliente, admin, $"/api/precios/topes/{sinDepartamento.Id}", sinDepartamento)).Cuerpo!.Mensaje);
        var ambos = tope with { Id = Guid.CreateVersion7(), Nivel = 3, ArticuloId = Guid.CreateVersion7() };
        Assert.Equal(HttpStatusCode.BadRequest, (await EnviarAsync(cliente, admin, $"/api/precios/topes/{ambos.Id}", ambos)).Estado);

        var listado = Assert.Single(await ListarAsync<DatosTopeDescuentoCentral>(cliente, admin, "/api/precios/topes"), t => t.Tope.Id == tope.Id);
        Assert.Equal((20m, $"Departamento T{sufijo} · Departamento topes {sufijo}"), (listado.Tope.PorcentajeMaximo!.Value, listado.Alcance));
        Assert.Contains(await ListarAsync<DatosMaestroCentral<DepartamentoCarga>>(cliente, admin, "/api/precios/departamentos"), f => f.Dato.Id == departamento.Id);
    }

    [SkippableFact]
    public async Task Maestros_y_precios_exigen_cada_uno_su_permiso()
    {
        Skip.If(central.MotivoOmision is not null, central.MotivoOmision);
        using var cliente = central.CrearCliente();
        var sufijo = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        await central.CrearUsuarioAsync($"SOLMAE{sufijo}", "Solo.Maestros#2026", false, CatalogoPermisosCentral.AdministrarMaestros);
        await central.CrearUsuarioAsync($"SOLPRE{sufijo}", "Solo.Precios#2026", false, CatalogoPermisosCentral.AdministrarPrecios);
        var maestros = (await CentralEnPruebas.IngresarAsync(cliente, $"SOLMAE{sufijo}", "Solo.Maestros#2026")).Cuerpo!.TokenAcceso!;
        var precios = (await CentralEnPruebas.IngresarAsync(cliente, $"SOLPRE{sufijo}", "Solo.Precios#2026")).Cuerpo!.TokenAcceso!;

        async Task<HttpStatusCode> EstadoAsync(string ruta, string token)
        {
            using var respuesta = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Get, ruta, token));
            return respuesta.StatusCode;
        }

        Assert.Equal(HttpStatusCode.OK, await EstadoAsync("/api/maestros/articulos", maestros));
        Assert.Equal(HttpStatusCode.Forbidden, await EstadoAsync("/api/precios/topes", maestros));
        Assert.Equal(HttpStatusCode.OK, await EstadoAsync("/api/precios/articulos", precios));
        Assert.Equal(HttpStatusCode.Forbidden, await EstadoAsync("/api/maestros/articulos", precios));
    }

    private static async Task<PaqueteBajadaMaestros> BajarAsync(HttpClient cliente, string token, long desde) =>
        await ObtenerAsync<PaqueteBajadaMaestros>(cliente, token, $"/api/sincronizacion/maestros?desde={desde}");

    private static async Task<T> ObtenerAsync<T>(HttpClient cliente, string token, string ruta)
    {
        using var respuesta = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Get, ruta, token));
        respuesta.EnsureSuccessStatusCode();
        return (await respuesta.Content.ReadFromJsonAsync<T>(OpcionesJson.Predeterminadas))!;
    }

    private static Task<List<T>> ListarAsync<T>(HttpClient cliente, string token, string ruta) => ObtenerAsync<List<T>>(cliente, token, ruta);

    private static async Task<(HttpStatusCode Estado, RespuestaAdministracion? Cuerpo)> EnviarAsync(HttpClient cliente, string token, string ruta, object cuerpo)
    {
        using var respuesta = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Put, ruta, token, cuerpo));
        var datos = respuesta.Content.Headers.ContentType?.MediaType == "application/json"
            ? await respuesta.Content.ReadFromJsonAsync<RespuestaAdministracion>(OpcionesJson.Predeterminadas)
            : null;
        return (respuesta.StatusCode, datos);
    }
}
