using CgPos.Dominio.Seguridad;
using CgPos.Pos.Aplicacion.Catalogo;
using Microsoft.AspNetCore.Mvc;

namespace CgPos.Pos.Agente.Api;

public static class RutasApiCatalogo
{

    public static IEndpointRouteBuilder MapearApiCatalogo(this IEndpointRouteBuilder aplicacion)
    {
        var api = aplicacion.MapGroup("/api").RequireAuthorization();

        api.MapGet("/articulos/codigo/{codigo}", async (string codigo, IConsultaArticulos consulta, CancellationToken cancelacion) =>
            await consulta.BuscarPorCodigoAsync(codigo, cancelacion) is { } articulo ? Results.Ok(articulo) : Results.NotFound());

        api.MapGet("/articulos", async (string? texto, int? departamentoId, int? maximo, IConsultaArticulos consulta, CancellationToken cancelacion) =>
            Results.Ok(await consulta.BuscarAsync(texto, departamentoId, maximo ?? 50, cancelacion)));

        api.MapGet("/articulos/catalogo", async (int? departamentoId, IConsultaArticulos consulta, CancellationToken cancelacion) =>
            Results.Ok(await consulta.ListarCatalogoAsync(departamentoId, cancelacion)));

        api.MapGet("/articulos/no-codificados", async (int? departamentoId, IConsultaArticulos consulta, CancellationToken cancelacion) =>
            Results.Ok(await consulta.ListarNoCodificadosAsync(departamentoId, cancelacion)));

        api.MapGet("/departamentos", async (IConsultaArticulos consulta, CancellationToken cancelacion) =>
            Results.Ok(await consulta.ListarDepartamentosAsync(cancelacion)));

        api.MapGet("/clientes", async (string? texto, int? maximo, IConsultaDocumentos consulta, CancellationToken cancelacion) =>
            Results.Ok(await consulta.BuscarAsync(texto, maximo ?? 50, cancelacion)));

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

        return aplicacion;
    }
}
