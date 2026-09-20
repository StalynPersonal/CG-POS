using System.Security.Claims;
using CgPos.Central.Aplicacion.Organizacion;
using CgPos.Central.Aplicacion.Reportes;
using CgPos.Contratos.Central;
using CgPos.Dominio.Seguridad;
using static CgPos.Central.Api.Api.RespuestasAdministracion;

namespace CgPos.Central.Api.Api;

/// <summary>
/// Cierres de caja informados por las terminales y su corrección desde el Central. En la caja, cerrar es definitivo: aquí
/// es donde se arregla un cuadre que salió mal, con motivo y auditoría.
/// </summary>
public static class RutasApiCierresCaja
{
    public static IEndpointRouteBuilder MapearApiCierresCaja(this IEndpointRouteBuilder aplicacion)
    {
        var manager = aplicacion.MapGroup("/api/manager/cierres-caja").RequireAuthorization(CatalogoPermisosCentral.AjustarCierres);

        // Referencias para los filtros de la pantalla, sin exigir los permisos de organización.
        manager.MapGet("/sucursales", async (IServicioOrganizacion servicio, CancellationToken cancelacion) =>
            Results.Ok(await servicio.ListarSucursalesAsync(cancelacion)));
        manager.MapGet("/cajas", async (IServicioOrganizacion servicio, CancellationToken cancelacion) =>
            Results.Ok(await servicio.ListarCajasAsync(cancelacion)));

        manager.MapGet("/", async (int? sucursalId, int? cajaId, DateOnly desde, DateOnly hasta, IServicioCierresCaja servicio, CancellationToken cancelacion) =>
            Results.Ok(await servicio.ListarAsync(sucursalId, cajaId, desde, hasta, cancelacion)));

        manager.MapPost("/{cierreId:int}/ajustes", async (int cierreId, SolicitudAjusteCierre solicitud, ClaimsPrincipal usuario, IServicioCierresCaja servicio,
                CancellationToken cancelacion) =>
            Responder(await servicio.AjustarAsync(cierreId, solicitud, Actor(usuario), cancelacion)));

        return aplicacion;
    }
}
