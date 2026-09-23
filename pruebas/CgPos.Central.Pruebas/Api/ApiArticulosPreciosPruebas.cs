using System.Net;
using System.Net.Http.Json;
using CgPos.Central.Pruebas.Soporte;
using CgPos.Contratos.Catalogo;
using CgPos.Contratos.Central;
using CgPos.Contratos.Serializacion;
using CgPos.Contratos.Sincronizacion;
using CgPos.Dominio.Catalogo;
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
        var unidad = (await ListarAsync<DatosMaestroCentral<UnidadMedidaCarga>>(cliente, admin, "/api/maestros/unidades-medida")).First().Dato;
        var impuesto = (await ListarAsync<DatosMaestroCentral<ImpuestoCarga>>(cliente, admin, "/api/maestros/impuestos")).First(i => i.Dato.Activo).Dato;
        var sufijo = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        var barras = $"99{Random.Shared.NextInt64(1_000_000_000, 9_999_999_999)}";

        var articulo = new ArticuloCarga($"A{sufijo}", $"Jabón de prueba {sufijo}", categoria.DepartamentoCodigo, unidad.Codigo, impuesto.Codigo, 150m,
            PrecioMayor: 140m, CantidadMinimaMayor: 12m, CodigosBarras: [barras], CategoriaCodigo: categoria.Codigo);
        var creado = await EnviarAsync(cliente, admin, HttpMethod.Post, "/api/maestros/articulos", articulo);
        Assert.True(creado.Cuerpo!.Exitosa, creado.Cuerpo.Mensaje);

        // Se busca por código de barras y por descripción; los nombres de las propiedades del registro no cuentan.
        var porBarras = await ObtenerAsync<PaginaMaestros<ArticuloCarga>>(cliente, admin, $"/api/maestros/articulos?buscar={barras}");
        Assert.Equal(articulo.Codigo, Assert.Single(porBarras.Elementos).Dato.Codigo);
        Assert.Equal(1, porBarras.Total);
        var porDescripcion = await ObtenerAsync<PaginaMaestros<ArticuloCarga>>(cliente, admin, $"/api/precios/articulos?buscar={Uri.EscapeDataString($"prueba {sufijo.ToLowerInvariant()}")}");
        Assert.Contains(porDescripcion.Elementos, e => e.Dato.Codigo == articulo.Codigo);
        Assert.Equal(0, (await ObtenerAsync<PaginaMaestros<ArticuloCarga>>(cliente, admin, "/api/maestros/articulos?buscar=preciodetalle")).Total);

        // Los códigos se buscan completos: un pedazo del código interno o del de barras no trae el artículo, porque buscar
        // «040100» devolvía cientos de artículos que solo empiezan igual. La descripción sí sigue encontrándose por parecido.
        Assert.DoesNotContain(
            (await ObtenerAsync<PaginaMaestros<ArticuloCarga>>(cliente, admin, $"/api/maestros/articulos?buscar={articulo.Codigo[..4]}")).Elementos,
            e => e.Dato.Codigo == articulo.Codigo);
        Assert.DoesNotContain(
            (await ObtenerAsync<PaginaMaestros<ArticuloCarga>>(cliente, admin, $"/api/maestros/articulos?buscar={barras[..6]}")).Elementos,
            e => e.Dato.Codigo == articulo.Codigo);
        Assert.Equal(articulo.Codigo,
            Assert.Single((await ObtenerAsync<PaginaMaestros<ArticuloCarga>>(cliente, admin, $"/api/maestros/articulos?buscar={articulo.Codigo}")).Elementos).Dato.Codigo);

        // El tipo acota además del texto: el jabón es normal, así que buscándolo entre los pesados no aparece.
        Assert.Equal(articulo.Codigo, Assert.Single((await ObtenerAsync<PaginaMaestros<ArticuloCarga>>(cliente, admin,
            $"/api/maestros/articulos?buscar={barras}&tipo={TipoArticulo.Normal}")).Elementos).Dato.Codigo);
        Assert.Equal(0, (await ObtenerAsync<PaginaMaestros<ArticuloCarga>>(cliente, admin,
            $"/api/maestros/articulos?buscar={barras}&tipo={TipoArticulo.Pesado}")).Total);

        // Un tipo que no existe no filtra nada: mejor devolver de más que dejar la pantalla en blanco sin explicar por qué.
        Assert.Equal(1, (await ObtenerAsync<PaginaMaestros<ArticuloCarga>>(cliente, admin,
            $"/api/maestros/articulos?buscar={barras}&tipo=NoExiste")).Total);

        // Desde maestros no se cambian los precios; un código ya usado no se vuelve a crear y un código de barras es de un solo artículo.
        Assert.True((await EnviarAsync(cliente, admin, HttpMethod.Put, "/api/maestros/articulos", articulo with { Descripcion = "Jabón renombrado", PrecioDetalle = 1m })).Cuerpo!.Exitosa);
        var renombrado = Assert.Single((await ObtenerAsync<PaginaMaestros<ArticuloCarga>>(cliente, admin, $"/api/maestros/articulos?buscar={barras}")).Elementos).Dato;
        Assert.Equal(("Jabón renombrado", 150m), (renombrado.Descripcion, renombrado.PrecioDetalle));
        Assert.Contains("Ya existe", (await EnviarAsync(cliente, admin, HttpMethod.Post, "/api/maestros/articulos", articulo)).Cuerpo!.Mensaje);
        var otro = articulo with { Codigo = $"C{sufijo}" };
        Assert.Contains("ya lo usa el artículo", (await EnviarAsync(cliente, admin, HttpMethod.Post, "/api/maestros/articulos", otro)).Cuerpo!.Mensaje);

        var marca = (await BajarAsync(cliente, tokenCaja, 0)).Hasta;
        var vigencia = new DateTimeOffset(2026, 10, 1, 6, 0, 0, TimeSpan.FromHours(-4));
        var precios = await EnviarAsync(cliente, admin, HttpMethod.Put, $"/api/precios/articulos/{articulo.Codigo}", new SolicitudPreciosArticulo(175m, 160m, 12m, 100m, 90m, vigencia));
        Assert.True(precios.Cuerpo!.Exitosa, precios.Cuerpo.Mensaje);

        var bajado = Assert.Single((await BajarAsync(cliente, tokenCaja, marca)).Maestros!.Articulos!, a => a.Codigo == articulo.Codigo);
        Assert.Equal((175m, 160m, 100m, 90m, "Jabón renombrado"), (bajado.PrecioDetalle, bajado.PrecioMayor!.Value, bajado.PrecioMinimo!.Value, bajado.Costo!.Value, bajado.Descripcion));
        Assert.Equal(vigencia, bajado.PreciosVigentesDesde);

        var sinPrecio = await EnviarAsync(cliente, admin, HttpMethod.Put, $"/api/precios/articulos/{articulo.Codigo}", new SolicitudPreciosArticulo(0m));
        Assert.Equal(HttpStatusCode.BadRequest, sinPrecio.Estado);
        Assert.Contains("precio detalle", sinPrecio.Cuerpo!.Mensaje);
        Assert.Equal(HttpStatusCode.NotFound, (await EnviarAsync(cliente, admin, HttpMethod.Put, $"/api/precios/articulos/NOEXISTE{sufijo}", new SolicitudPreciosArticulo(10m))).Estado);
    }

    [SkippableFact]
    public async Task Los_topes_validan_su_alcance_y_no_se_repiten_por_nivel()
    {
        Skip.If(central.MotivoOmision is not null, central.MotivoOmision);
        using var cliente = central.CrearCliente();
        var admin = await CentralEnPruebas.TokenAdministradorAsync(cliente);
        var sufijo = Guid.NewGuid().ToString("N")[..5].ToUpperInvariant();
        var departamento = new DepartamentoCarga(Codigos.Siguiente(), $"Departamento topes {sufijo}");
        Assert.True((await EnviarAsync(cliente, admin, HttpMethod.Post, "/api/maestros/departamentos", departamento)).Cuerpo!.Exitosa);

        var tope = new TopeDescuentoCarga(Codigos.Siguiente(), 2, 15m, null, DepartamentoCodigo: departamento.Codigo);
        var guardado = await EnviarAsync(cliente, admin, HttpMethod.Post, "/api/precios/topes", tope);
        Assert.True(guardado.Cuerpo!.Exitosa, guardado.Cuerpo.Mensaje);
        Assert.True((await EnviarAsync(cliente, admin, HttpMethod.Put, "/api/precios/topes", tope with { PorcentajeMaximo = 20m })).Cuerpo!.Exitosa);

        var repetido = tope with { Codigo = Codigos.Siguiente(), PorcentajeMaximo = 5m };
        Assert.Contains("Ya hay un tope de descuento de nivel 2 para ese departamento", (await EnviarAsync(cliente, admin, HttpMethod.Post, "/api/precios/topes", repetido)).Cuerpo!.Mensaje);
        var sinDepartamento = tope with { Codigo = Codigos.Siguiente(), DepartamentoCodigo = 999_999 };
        Assert.Contains("No existe el departamento", (await EnviarAsync(cliente, admin, HttpMethod.Post, "/api/precios/topes", sinDepartamento)).Cuerpo!.Mensaje);
        var ambos = tope with { Codigo = Codigos.Siguiente(), Nivel = 3, ArticuloCodigo = $"NOEXISTE{sufijo}" };
        Assert.Equal(HttpStatusCode.BadRequest, (await EnviarAsync(cliente, admin, HttpMethod.Post, "/api/precios/topes", ambos)).Estado);

        var listado = Assert.Single(await ListarAsync<DatosTopeDescuentoCentral>(cliente, admin, "/api/precios/topes"), t => t.Tope.Codigo == tope.Codigo);
        Assert.Equal((20m, $"Departamento {departamento.Codigo} · Departamento topes {sufijo}"), (listado.Tope.PorcentajeMaximo!.Value, listado.Alcance));
        Assert.Contains(await ListarAsync<DatosMaestroCentral<DepartamentoCarga>>(cliente, admin, "/api/precios/departamentos"), f => f.Dato.Codigo == departamento.Codigo);
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

    private static async Task<(HttpStatusCode Estado, RespuestaAdministracion? Cuerpo)> EnviarAsync(HttpClient cliente, string token, HttpMethod metodo, string ruta, object cuerpo)
    {
        using var respuesta = await cliente.SendAsync(CentralEnPruebas.Solicitud(metodo, ruta, token, cuerpo));
        var datos = respuesta.Content.Headers.ContentType?.MediaType == "application/json"
            ? await respuesta.Content.ReadFromJsonAsync<RespuestaAdministracion>(OpcionesJson.Predeterminadas)
            : null;
        return (respuesta.StatusCode, datos);
    }
}
