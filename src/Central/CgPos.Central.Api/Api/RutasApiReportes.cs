using CgPos.Central.Aplicacion.Organizacion;
using CgPos.Central.Aplicacion.Reportes;
using CgPos.Contratos.Central;
using CgPos.Dominio.Seguridad;

namespace CgPos.Central.Api.Api;

/// <summary>Reportes del Central (M16) y su descarga en Excel, PDF o el archivo del formato 607 de la DGII.</summary>
public static class RutasApiReportes
{
    public static IEndpointRouteBuilder MapearApiReportes(this IEndpointRouteBuilder aplicacion)
    {
        var manager = aplicacion.MapGroup("/api/manager/reportes").RequireAuthorization(CatalogoPermisosCentral.ConsultarReportes);

        manager.MapGet("/{tipo}", async (TipoReporteCentral tipo, DateOnly desde, DateOnly hasta, Guid? sucursalId, Guid? cajaId,
                IServicioReportesCentral servicio, CancellationToken cancelacion) =>
            Results.Ok(await servicio.TablaAsync(tipo, new FiltroReporte(desde, hasta, sucursalId, cajaId), cancelacion)));

        manager.MapGet("/{tipo}/excel", async (TipoReporteCentral tipo, DateOnly desde, DateOnly hasta, Guid? sucursalId, Guid? cajaId,
                IServicioReportesCentral servicio, IExportadorReportes exportador, CancellationToken cancelacion) =>
            Descargar(exportador.AExcel(await servicio.TablaAsync(tipo, new FiltroReporte(desde, hasta, sucursalId, cajaId), cancelacion))));

        manager.MapGet("/{tipo}/pdf", async (TipoReporteCentral tipo, DateOnly desde, DateOnly hasta, Guid? sucursalId, Guid? cajaId,
                IServicioReportesCentral servicio, IExportadorReportes exportador, CancellationToken cancelacion) =>
            Descargar(exportador.APdf(await servicio.TablaAsync(tipo, new FiltroReporte(desde, hasta, sucursalId, cajaId), cancelacion))));

        // Archivo de envío del 607: el RNC del emisor sale de la empresa configurada en el Central.
        manager.MapGet("/formato607/archivo", async (DateOnly desde, DateOnly hasta, Guid? sucursalId, Guid? cajaId, IServicioReportesCentral servicio,
                IExportadorReportes exportador, IServicioOrganizacion organizacion, CancellationToken cancelacion) =>
        {
            var empresa = await organizacion.ObtenerEmpresaAsync(cancelacion);
            if (empresa is null)
                return Results.Problem("No hay empresa configurada en el Central.", statusCode: StatusCodes.Status422UnprocessableEntity);

            var filas = await servicio.Formato607Async(new FiltroReporte(desde, hasta, sucursalId, cajaId), cancelacion);
            return Descargar(exportador.A607(empresa.Rnc, desde, filas));
        });

        return aplicacion;
    }

    private static IResult Descargar(ArchivoReporte archivo) => Results.File(archivo.Contenido, archivo.TipoContenido, archivo.Nombre);
}
