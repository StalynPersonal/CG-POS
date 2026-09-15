using CgPos.Dominio.Seguridad;
using CgPos.Pos.Aplicacion.Catalogo;
using Microsoft.AspNetCore.Mvc;

namespace CgPos.Pos.Agente.Api;

public static class RutasApiCatalogo
{
    private const long TamanoMaximoPadron = 500L * 1024 * 1024;

    public static IEndpointRouteBuilder MapearApiCatalogo(this IEndpointRouteBuilder aplicacion)
    {
        var api = aplicacion.MapGroup("/api").RequireAuthorization();

        api.MapGet("/articulos/codigo/{codigo}", async (string codigo, IConsultaArticulos consulta, CancellationToken cancelacion) =>
            await consulta.BuscarPorCodigoAsync(codigo, cancelacion) is { } articulo ? Results.Ok(articulo) : Results.NotFound());

        api.MapGet("/articulos", async (string? texto, Guid? familiaId, int? maximo, IConsultaArticulos consulta, CancellationToken cancelacion) =>
            Results.Ok(await consulta.BuscarAsync(texto, familiaId, maximo ?? 50, cancelacion)));

        api.MapGet("/articulos/catalogo", async (Guid? familiaId, IConsultaArticulos consulta, CancellationToken cancelacion) =>
            Results.Ok(await consulta.ListarCatalogoAsync(familiaId, cancelacion)));

        api.MapGet("/articulos/no-codificados", async (Guid? familiaId, IConsultaArticulos consulta, CancellationToken cancelacion) =>
            Results.Ok(await consulta.ListarNoCodificadosAsync(familiaId, cancelacion)));

        api.MapGet("/articulos/{articuloId:guid}/precios", async (Guid articuloId, IConsultaArticulos consulta, CancellationToken cancelacion) =>
            Results.Ok(await consulta.ObtenerHistorialPreciosAsync(articuloId, cancelacion)));

        api.MapGet("/familias", async (IConsultaArticulos consulta, CancellationToken cancelacion) =>
            Results.Ok(await consulta.ListarFamiliasAsync(cancelacion)));

        api.MapGet("/documentos/{documento}", async (string documento, IConsultaDocumentos consulta, CancellationToken cancelacion) =>
            Results.Ok(await consulta.ConsultarAsync(documento, cancelacion)));

        api.MapGet("/catalogos/cobro", async (IConsultaCatalogoCobro consulta, CancellationToken cancelacion) =>
            Results.Ok(await consulta.ObtenerAsync(cancelacion)));

        // Importaciones manuales (sin Central todavía): solo con permiso de administración.
        api.MapPost("/maestros/articulos/csv", async (HttpRequest solicitud, IImportadorArticulos importador, CancellationToken cancelacion) =>
            {
                try
                {
                    return Results.Ok(await importador.ImportarCsvAsync(solicitud.Body, "Importación CSV", cancelacion));
                }
                catch (CargaMaestrosInvalidaExcepcion excepcion)
                {
                    return Results.BadRequest(new { errores = excepcion.Errores });
                }
            })
            .RequireAuthorization(CatalogoPermisos.AdministrarConfiguracion);

        api.MapPost("/maestros/padron-dgii", async (HttpRequest solicitud, IImportadorPadronDgii importador, CancellationToken cancelacion) =>
                Results.Ok(await importador.ImportarAsync(solicitud.Body, cancelacion)))
            .RequireAuthorization(CatalogoPermisos.AdministrarConfiguracion)
            .WithMetadata(new RequestSizeLimitAttribute(TamanoMaximoPadron));

        return aplicacion;
    }
}
