using System.Security.Claims;
using CgPos.Central.Aplicacion.Organizacion;
using CgPos.Contratos.Central;
using CgPos.Dominio.Organizacion;
using CgPos.Dominio.Seguridad;
using static CgPos.Central.Api.Api.RespuestasAdministracion;

namespace CgPos.Central.Api.Api;

public static class RutasApiOrganizacion
{
    public static IEndpointRouteBuilder MapearApiOrganizacion(this IEndpointRouteBuilder aplicacion)
    {
        var organizacion = aplicacion.MapGroup("/api/organizacion").RequireAuthorization(CatalogoPermisosCentral.AdministrarOrganizacion);

        organizacion.MapGet("/empresa", async (IServicioOrganizacion servicio, CancellationToken cancelacion) =>
            await servicio.ObtenerEmpresaAsync(cancelacion) is { } empresa ? Results.Ok(empresa) : Results.NotFound());

        organizacion.MapPut("/empresa", async (SolicitudEmpresa solicitud, ClaimsPrincipal usuario, IServicioOrganizacion servicio, CancellationToken cancelacion) =>
            Responder(await servicio.ActualizarEmpresaAsync(solicitud, Actor(usuario), cancelacion)));

        organizacion.MapGet("/sucursales", async (IServicioOrganizacion servicio, CancellationToken cancelacion) =>
            Results.Ok(await servicio.ListarSucursalesAsync(cancelacion)));

        organizacion.MapPost("/sucursales", async (SolicitudSucursal solicitud, ClaimsPrincipal usuario, IServicioOrganizacion servicio, CancellationToken cancelacion) =>
            Responder(await servicio.CrearSucursalAsync(solicitud, Actor(usuario), cancelacion)));

        organizacion.MapPut("/sucursales/{sucursalId:int}", async (int sucursalId, SolicitudSucursal solicitud, ClaimsPrincipal usuario, IServicioOrganizacion servicio,
                CancellationToken cancelacion) =>
            Responder(await servicio.ActualizarSucursalAsync(sucursalId, solicitud, Actor(usuario), cancelacion)));

        organizacion.MapPost("/sucursales/{sucursalId:int}/activar", async (int sucursalId, ClaimsPrincipal usuario, IServicioOrganizacion servicio, CancellationToken cancelacion) =>
            Responder(await servicio.CambiarEstadoSucursalAsync(sucursalId, true, Actor(usuario), cancelacion)));

        organizacion.MapPost("/sucursales/{sucursalId:int}/desactivar", async (int sucursalId, ClaimsPrincipal usuario, IServicioOrganizacion servicio, CancellationToken cancelacion) =>
            Responder(await servicio.CambiarEstadoSucursalAsync(sucursalId, false, Actor(usuario), cancelacion)));

        organizacion.MapGet("/cajas", async (IServicioOrganizacion servicio, CancellationToken cancelacion) =>
            Results.Ok(await servicio.ListarCajasAsync(cancelacion)));

        organizacion.MapPost("/cajas", async (SolicitudCaja solicitud, ClaimsPrincipal usuario, IServicioOrganizacion servicio, CancellationToken cancelacion) =>
            Responder(await servicio.CrearCajaAsync(solicitud, Actor(usuario), cancelacion)));

        organizacion.MapPut("/cajas/{cajaId:int}", async (int cajaId, SolicitudActualizarCaja solicitud, ClaimsPrincipal usuario, IServicioOrganizacion servicio,
                CancellationToken cancelacion) =>
            Responder(await servicio.ActualizarCajaAsync(cajaId, solicitud, Actor(usuario), cancelacion)));

        organizacion.MapPost("/cajas/{cajaId:int}/habilitar", async (int cajaId, ClaimsPrincipal usuario, IServicioOrganizacion servicio, CancellationToken cancelacion) =>
            Responder(await servicio.CambiarEstadoCajaAsync(cajaId, true, Actor(usuario), cancelacion)));

        organizacion.MapPost("/cajas/{cajaId:int}/deshabilitar", async (int cajaId, ClaimsPrincipal usuario, IServicioOrganizacion servicio, CancellationToken cancelacion) =>
            Responder(await servicio.CambiarEstadoCajaAsync(cajaId, false, Actor(usuario), cancelacion)));

        organizacion.MapGet("/parametros/catalogo", () => Results.Ok(CatalogoParametros.Todos));

        organizacion.MapGet("/parametros", async (IServicioOrganizacion servicio, CancellationToken cancelacion) =>
            Results.Ok(await servicio.ListarParametrosAsync(cancelacion)));

        organizacion.MapPost("/parametros", async (SolicitudParametro solicitud, ClaimsPrincipal usuario, IServicioOrganizacion servicio, CancellationToken cancelacion) =>
            Responder(await servicio.CrearParametroAsync(solicitud, Actor(usuario), cancelacion)));

        organizacion.MapPut("/parametros/{parametroId:int}", async (int parametroId, SolicitudValorParametro solicitud, ClaimsPrincipal usuario, IServicioOrganizacion servicio,
                CancellationToken cancelacion) =>
            Responder(await servicio.CambiarValorParametroAsync(parametroId, solicitud.Valor ?? string.Empty, Actor(usuario), cancelacion)));

        return aplicacion;
    }
}
