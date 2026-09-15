using System.Security.Claims;
using CgPos.Contratos.Ventas;
using CgPos.Pos.Agente.Seguridad;
using CgPos.Pos.Aplicacion.Seguridad;
using CgPos.Pos.Aplicacion.Ventas;

namespace CgPos.Pos.Agente.Api;

/// <summary>Módulo de despacho de pendientes de entrega (M12). Los rechazos responden 422 con el pendiente tal como quedó.</summary>
public static class RutasApiDespacho
{
    public static IEndpointRouteBuilder MapearApiDespacho(this IEndpointRouteBuilder aplicacion)
    {
        var api = aplicacion.MapGroup("/api/despacho/pendientes").RequireAuthorization();

        api.MapGet("/abiertos", (ClaimsPrincipal usuario, IServicioDespacho servicio, CancellationToken cancelacion) =>
            ConSesion(usuario, async sesion => Results.Ok(await servicio.ListarAbiertosAsync(sesion, cancelacion))));

        api.MapGet("/buscar/{codigo}", (string codigo, ClaimsPrincipal usuario, IServicioDespacho servicio, CancellationToken cancelacion) =>
            ConSesion(usuario, async sesion =>
            {
                var respuesta = await servicio.BuscarAsync(sesion, codigo, cancelacion);
                return respuesta.Exitosa ? Results.Ok(respuesta) : Results.UnprocessableEntity(respuesta);
            }));

        api.MapPost("/{pendienteId:guid}/estado", (Guid pendienteId, SolicitudEstadoPendiente solicitud, ClaimsPrincipal usuario, IServicioDespacho servicio,
                CancellationToken cancelacion) =>
            ConSesion(usuario, async sesion => Resultado(await servicio.CambiarEstadoAsync(sesion, pendienteId, solicitud, cancelacion))));

        api.MapPost("/{pendienteId:guid}/entregas", (Guid pendienteId, SolicitudEntregaPendiente solicitud, ClaimsPrincipal usuario, IServicioDespacho servicio,
                CancellationToken cancelacion) =>
            ConSesion(usuario, async sesion => Resultado(await servicio.EntregarAsync(sesion, pendienteId, solicitud, cancelacion))));

        api.MapPost("/{pendienteId:guid}/anular", (Guid pendienteId, SolicitudAnularPendiente solicitud, ClaimsPrincipal usuario, IServicioDespacho servicio,
                CancellationToken cancelacion) =>
            ConSesion(usuario, async sesion => Resultado(await servicio.AnularAsync(sesion, pendienteId, solicitud, cancelacion))));

        return aplicacion;
    }

    private static async Task<IResult> ConSesion(ClaimsPrincipal usuario, Func<SesionUsuario, Task<IResult>> accion) =>
        EmisorTokens.LeerSesion(usuario) is { } sesion ? await accion(sesion) : Results.Unauthorized();

    private static IResult Resultado(RespuestaPendiente respuesta) =>
        respuesta.Exitosa ? Results.Ok(respuesta) : Results.UnprocessableEntity(respuesta);
}
