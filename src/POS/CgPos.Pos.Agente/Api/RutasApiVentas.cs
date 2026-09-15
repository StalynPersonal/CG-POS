using System.Security.Claims;
using CgPos.Contratos.Ventas;
using CgPos.Dominio.Seguridad;
using CgPos.Pos.Agente.Pantallas;
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

        var ventas = api.MapGroup("/ventas").AddEndpointFilter<FiltroPublicarVenta>();

        ventas.MapGet("/actual", (ClaimsPrincipal usuario, IServicioVentas servicio, CancellationToken cancelacion) =>
            ConSesion(usuario, async sesion => Resultado(await servicio.ObtenerActualAsync(sesion, cancelacion))));

        ventas.MapPost("/{ventaId:guid}/lineas", (Guid ventaId, SolicitudAgregarArticulo solicitud, ClaimsPrincipal usuario, IServicioVentas servicio, CancellationToken cancelacion) =>
            ConSesion(usuario, async sesion => Resultado(await servicio.AgregarArticuloAsync(sesion, ventaId, solicitud.Codigo ?? string.Empty, solicitud.Cantidad, solicitud.Serial, cancelacion))));

        ventas.MapPost("/{ventaId:guid}/lineas/balanza", (Guid ventaId, SolicitudPesarArticulo solicitud, ClaimsPrincipal usuario, IServicioVentas servicio, CancellationToken cancelacion) =>
            ConSesion(usuario, async sesion => Resultado(await servicio.AgregarDesdeBalanzaAsync(sesion, ventaId, solicitud.Codigo ?? string.Empty, cancelacion))));

        ventas.MapPut("/{ventaId:guid}/lineas/{numeroLinea:int}/cantidad", (Guid ventaId, int numeroLinea, SolicitudCambiarCantidad solicitud, ClaimsPrincipal usuario, IServicioVentas servicio, CancellationToken cancelacion) =>
            ConSesion(usuario, async sesion => Resultado(await servicio.CambiarCantidadAsync(sesion, ventaId, numeroLinea, solicitud.Cantidad, cancelacion))));

        ventas.MapPost("/{ventaId:guid}/lineas/{numeroLinea:int}/eliminar", (Guid ventaId, int numeroLinea, SolicitudConAutorizacion solicitud, ClaimsPrincipal usuario, IServicioVentas servicio, CancellationToken cancelacion) =>
            ConSesion(usuario, async sesion => Resultado(await servicio.EliminarLineaAsync(sesion, ventaId, numeroLinea, solicitud.AutorizacionId, cancelacion))));

        ventas.MapPost("/{ventaId:guid}/eliminar-por-codigo", (Guid ventaId, SolicitudEliminarPorCodigo solicitud, ClaimsPrincipal usuario, IServicioVentas servicio, CancellationToken cancelacion) =>
            ConSesion(usuario, async sesion => Resultado(await servicio.EliminarPorCodigoAsync(sesion, ventaId, solicitud.Codigo ?? string.Empty, solicitud.AutorizacionId, cancelacion))));

        ventas.MapPost("/{ventaId:guid}/limpiar", (Guid ventaId, SolicitudConAutorizacion solicitud, ClaimsPrincipal usuario, IServicioVentas servicio, CancellationToken cancelacion) =>
            ConSesion(usuario, async sesion => Resultado(await servicio.LimpiarAsync(sesion, ventaId, solicitud.AutorizacionId, cancelacion))));

        // Cliente, comprobante y límite (C4)
        ventas.MapPost("/{ventaId:guid}/cliente", (Guid ventaId, SolicitudAsignarCliente solicitud, ClaimsPrincipal usuario, IServicioVentas servicio, CancellationToken cancelacion) =>
            ConSesion(usuario, async sesion => Resultado(await servicio.AsignarClienteAsync(sesion, ventaId, solicitud.Documento ?? string.Empty, solicitud.Nombre, cancelacion))));

        ventas.MapDelete("/{ventaId:guid}/cliente", (Guid ventaId, ClaimsPrincipal usuario, IServicioVentas servicio, CancellationToken cancelacion) =>
            ConSesion(usuario, async sesion => Resultado(await servicio.QuitarClienteAsync(sesion, ventaId, cancelacion))));

        ventas.MapPut("/{ventaId:guid}/comprobante", (Guid ventaId, SolicitudCambiarComprobante solicitud, ClaimsPrincipal usuario, IServicioVentas servicio, CancellationToken cancelacion) =>
            ConSesion(usuario, async sesion => Resultado(await servicio.CambiarComprobanteAsync(sesion, ventaId, solicitud.TipoComprobante, solicitud.AutorizacionId, cancelacion))));

        ventas.MapPut("/{ventaId:guid}/limite", (Guid ventaId, SolicitudLimiteCompra solicitud, ClaimsPrincipal usuario, IServicioVentas servicio, CancellationToken cancelacion) =>
            ConSesion(usuario, async sesion => Resultado(await servicio.EstablecerLimiteCompraAsync(sesion, ventaId, solicitud.Limite, cancelacion))));

        // Espera, anulación y suspensión (C4)
        ventas.MapGet("/espera", (ClaimsPrincipal usuario, IServicioVentas servicio, CancellationToken cancelacion) =>
            ConSesion(usuario, async sesion => Results.Ok(await servicio.ListarEnEsperaAsync(sesion, cancelacion))));

        ventas.MapPost("/{ventaId:guid}/espera", (Guid ventaId, ClaimsPrincipal usuario, IServicioVentas servicio, CancellationToken cancelacion) =>
            ConSesion(usuario, async sesion => Resultado(await servicio.PonerEnEsperaAsync(sesion, ventaId, cancelacion))));

        ventas.MapPost("/{ventaId:guid}/retomar", (Guid ventaId, ClaimsPrincipal usuario, IServicioVentas servicio, CancellationToken cancelacion) =>
            ConSesion(usuario, async sesion => Resultado(await servicio.RetomarAsync(sesion, ventaId, cancelacion))));

        ventas.MapPost("/{ventaId:guid}/anular", (Guid ventaId, SolicitudAnularVenta solicitud, ClaimsPrincipal usuario, IServicioVentas servicio, CancellationToken cancelacion) =>
            ConSesion(usuario, async sesion => Resultado(await servicio.AnularAsync(sesion, ventaId, solicitud.Motivo, solicitud.AutorizacionId, cancelacion))));

        api.MapPost("/caja/suspender", (SolicitudConAutorizacion solicitud, ClaimsPrincipal usuario, IServicioVentas servicio, CancellationToken cancelacion) =>
            ConSesion(usuario, async sesion => Resultado(await servicio.SuspenderAsync(sesion, solicitud.AutorizacionId, cancelacion))));

        api.MapGet("/sincronizacion/estado", async (IEstadoSincronizacion estado, CancellationToken cancelacion) =>
            Results.Ok(await estado.ObtenerAsync(cancelacion)));

        return aplicacion;
    }

    private static async Task<IResult> ConSesion(ClaimsPrincipal usuario, Func<SesionUsuario, Task<IResult>> accion) =>
        EmisorTokens.LeerSesion(usuario) is { } sesion ? await accion(sesion) : Results.Unauthorized();

    private static IResult Resultado(RespuestaVenta respuesta) =>
        respuesta.Exitosa ? Results.Ok(respuesta) : Results.UnprocessableEntity(respuesta);
}
