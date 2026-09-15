using System.Security.Claims;
using CgPos.Contratos.Ventas;
using CgPos.Dominio.Seguridad;
using CgPos.Pos.Agente.Seguridad;
using CgPos.Pos.Aplicacion.Seguridad;
using CgPos.Pos.Aplicacion.Ventas;

namespace CgPos.Pos.Agente.Api;

/// <summary>
/// Turno y venta en curso. Los rechazos de negocio responden 422 (409 si ya hay turno abierto) con la
/// respuesta completa, para que la pantalla muestre el mensaje y la venta tal como quedó.
/// </summary>
public static class RutasApiVentas
{
    public static IEndpointRouteBuilder MapearApiVentas(this IEndpointRouteBuilder aplicacion)
    {
        var api = aplicacion.MapGroup("/api").RequireAuthorization();

        api.MapGet("/turnos/actual", (ClaimsPrincipal usuario, IServicioTurnos turnos, CancellationToken cancelacion) =>
            ConSesion(usuario, async sesion => Results.Ok(await turnos.ObtenerEstadoAsync(sesion, cancelacion))));

        api.MapPost("/turnos", (SolicitudAbrirTurno solicitud, ClaimsPrincipal usuario, IServicioTurnos turnos, CancellationToken cancelacion) =>
                ConSesion(usuario, async sesion =>
                {
                    var respuesta = await turnos.AbrirAsync(sesion, solicitud.FondoInicial, cancelacion);
                    return respuesta.Resultado switch
                    {
                        CodigoResultadoTurno.Correcto => Results.Ok(respuesta),
                        CodigoResultadoTurno.YaExisteTurnoAbierto => Results.Json(respuesta, statusCode: StatusCodes.Status409Conflict),
                        CodigoResultadoTurno.SinPermiso => Results.Json(respuesta, statusCode: StatusCodes.Status403Forbidden),
                        _ => Results.UnprocessableEntity(respuesta),
                    };
                }))
            .RequireAuthorization(CatalogoPermisos.AbrirTurno);

        var ventas = api.MapGroup("/ventas");

        ventas.MapGet("/actual", (ClaimsPrincipal usuario, IServicioVentas servicio, CancellationToken cancelacion) =>
            ConSesion(usuario, async sesion => Resultado(await servicio.ObtenerActualAsync(sesion, cancelacion))));

        ventas.MapPost("/{ventaId:guid}/lineas", (Guid ventaId, SolicitudAgregarArticulo solicitud, ClaimsPrincipal usuario, IServicioVentas servicio, CancellationToken cancelacion) =>
            ConSesion(usuario, async sesion => Resultado(await servicio.AgregarArticuloAsync(sesion, ventaId, solicitud.Codigo ?? string.Empty, solicitud.Cantidad, cancelacion))));

        ventas.MapPut("/{ventaId:guid}/lineas/{numeroLinea:int}/cantidad", (Guid ventaId, int numeroLinea, SolicitudCambiarCantidad solicitud, ClaimsPrincipal usuario, IServicioVentas servicio, CancellationToken cancelacion) =>
            ConSesion(usuario, async sesion => Resultado(await servicio.CambiarCantidadAsync(sesion, ventaId, numeroLinea, solicitud.Cantidad, cancelacion))));

        ventas.MapPost("/{ventaId:guid}/lineas/{numeroLinea:int}/eliminar", (Guid ventaId, int numeroLinea, SolicitudConAutorizacion solicitud, ClaimsPrincipal usuario, IServicioVentas servicio, CancellationToken cancelacion) =>
            ConSesion(usuario, async sesion => Resultado(await servicio.EliminarLineaAsync(sesion, ventaId, numeroLinea, solicitud.AutorizacionId, cancelacion))));

        ventas.MapPost("/{ventaId:guid}/eliminar-por-codigo", (Guid ventaId, SolicitudEliminarPorCodigo solicitud, ClaimsPrincipal usuario, IServicioVentas servicio, CancellationToken cancelacion) =>
            ConSesion(usuario, async sesion => Resultado(await servicio.EliminarPorCodigoAsync(sesion, ventaId, solicitud.Codigo ?? string.Empty, solicitud.AutorizacionId, cancelacion))));

        ventas.MapPost("/{ventaId:guid}/limpiar", (Guid ventaId, SolicitudConAutorizacion solicitud, ClaimsPrincipal usuario, IServicioVentas servicio, CancellationToken cancelacion) =>
            ConSesion(usuario, async sesion => Resultado(await servicio.LimpiarAsync(sesion, ventaId, solicitud.AutorizacionId, cancelacion))));

        api.MapGet("/sincronizacion/estado", async (IEstadoSincronizacion estado, CancellationToken cancelacion) =>
            Results.Ok(await estado.ObtenerAsync(cancelacion)));

        return aplicacion;
    }

    private static async Task<IResult> ConSesion(ClaimsPrincipal usuario, Func<SesionUsuario, Task<IResult>> accion) =>
        EmisorTokens.LeerSesion(usuario) is { } sesion ? await accion(sesion) : Results.Unauthorized();

    private static IResult Resultado(RespuestaVenta respuesta) =>
        respuesta.Exitosa ? Results.Ok(respuesta) : Results.UnprocessableEntity(respuesta);
}
