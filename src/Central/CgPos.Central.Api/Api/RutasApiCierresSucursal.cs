using System.Security.Claims;
using CgPos.Central.Aplicacion.Maestros;
using CgPos.Central.Aplicacion.Organizacion;
using CgPos.Central.Aplicacion.Reportes;
using CgPos.Contratos.Catalogo;
using CgPos.Contratos.Central;
using CgPos.Dominio.Seguridad;
using static CgPos.Central.Api.Api.RespuestasAdministracion;

namespace CgPos.Central.Api.Api;

/// <summary>Cierre consolidado de sucursal (RF-264): preparar el del día, consolidarlo con sus depósitos y consultar los hechos.</summary>
public static class RutasApiCierresSucursal
{
    public static IEndpointRouteBuilder MapearApiCierresSucursal(this IEndpointRouteBuilder aplicacion)
    {
        var manager = aplicacion.MapGroup("/api/manager/cierres-sucursal").RequireAuthorization(CatalogoPermisosCentral.CerrarSucursal);

        // Referencias para la pantalla, sin exigir los permisos de organización ni de maestros.
        manager.MapGet("/sucursales", async (IServicioOrganizacion servicio, CancellationToken cancelacion) =>
            Results.Ok(await servicio.ListarSucursalesAsync(cancelacion)));
        manager.MapGet("/bancos", async (IServicioMaestrosCentral servicio, CancellationToken cancelacion) =>
            Results.Ok((await servicio.ListarAsync<BancoCarga>(cancelacion)).Select(b => b.Dato).Where(b => b.Activo).ToList()));

        manager.MapGet("/", async (int? sucursalId, DateOnly desde, DateOnly hasta, IServicioCierresSucursal servicio, CancellationToken cancelacion) =>
            Results.Ok(await servicio.ListarAsync(sucursalId, desde, hasta, cancelacion)));

        manager.MapGet("/preparar", async (int sucursalId, DateOnly fecha, IServicioCierresSucursal servicio, CancellationToken cancelacion) =>
            await servicio.PrepararAsync(sucursalId, fecha, cancelacion) is { } preparacion ? Results.Ok(preparacion) : Results.NotFound());

        manager.MapGet("/{cierreSucursalId:int}", async (int cierreSucursalId, IServicioCierresSucursal servicio, CancellationToken cancelacion) =>
            await servicio.ObtenerAsync(cierreSucursalId, cancelacion) is { } cierre ? Results.Ok(cierre) : Results.NotFound());

        manager.MapPost("/", async (SolicitudCierreSucursal solicitud, ClaimsPrincipal usuario, IServicioCierresSucursal servicio, CancellationToken cancelacion) =>
            Responder(await servicio.ConsolidarAsync(solicitud, Actor(usuario), cancelacion)));

        return aplicacion;
    }
}
