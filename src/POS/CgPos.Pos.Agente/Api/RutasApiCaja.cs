using System.Security.Claims;
using CgPos.Contratos.Ventas;
using CgPos.Pos.Agente.Seguridad;
using CgPos.Pos.Aplicacion.Seguridad;
using CgPos.Pos.Aplicacion.Ventas;

namespace CgPos.Pos.Agente.Api;

/// <summary>
/// Operaciones del turno (M13): resumen, pre-cierre, retiros, relevo, cierre y reapertura. Los permisos se validan en el servicio porque
/// todas aceptan autorización de supervisor; los rechazos responden 422 con la respuesta completa.
/// </summary>
public static class RutasApiCaja
{
    public static IEndpointRouteBuilder MapearApiCaja(this IEndpointRouteBuilder aplicacion)
    {
        var turno = aplicacion.MapGroup("/api/caja/turno").RequireAuthorization();

        turno.MapGet("/resumen", (ClaimsPrincipal usuario, IServicioCaja servicio, CancellationToken cancelacion) =>
            ConSesion(usuario, async sesion => Resultado(await servicio.ObtenerResumenAsync(sesion, cancelacion))));

        turno.MapPost("/precierre", (SolicitudConAutorizacion solicitud, ClaimsPrincipal usuario, IServicioCaja servicio, CancellationToken cancelacion) =>
            ConSesion(usuario, async sesion => Resultado(await servicio.PreCierreAsync(sesion, solicitud.AutorizacionId, cancelacion))));

        // Cuadre de las tarjetas contra el lote del terminal antes de cerrar (RF-215).
        turno.MapPost("/conciliacion-tarjetas", (ClaimsPrincipal usuario, IServicioCaja servicio, CancellationToken cancelacion) =>
            ConSesion(usuario, async sesion => Results.Ok(await servicio.ConciliarTarjetasAsync(sesion, cancelacion))));

        turno.MapPost("/retiros", (SolicitudRetiroEfectivo solicitud, ClaimsPrincipal usuario, IServicioCaja servicio, CancellationToken cancelacion) =>
            ConSesion(usuario, async sesion => Resultado(await servicio.RetirarEfectivoAsync(sesion, solicitud.Monto, solicitud.Motivo, solicitud.AutorizacionId, cancelacion))));

        turno.MapPost("/relevo", (SolicitudConAutorizacion solicitud, ClaimsPrincipal usuario, IServicioCaja servicio, CancellationToken cancelacion) =>
            ConSesion(usuario, async sesion => Resultado(await servicio.RelevarAsync(sesion, solicitud.AutorizacionId, cancelacion))));

        turno.MapPost("/cierre", (SolicitudCierreTurno solicitud, ClaimsPrincipal usuario, IServicioCaja servicio, CancellationToken cancelacion) =>
            ConSesion(usuario, async sesion => Resultado(await servicio.CerrarAsync(sesion, solicitud.AutorizacionId, cancelacion))));

        var cierres = aplicacion.MapGroup("/api/caja/cierres").RequireAuthorization();

        cierres.MapGet("/", (int? maximo, ClaimsPrincipal usuario, IServicioCaja servicio, CancellationToken cancelacion) =>
            ConSesion(usuario, async sesion => Results.Ok(await servicio.ListarCierresAsync(sesion, maximo ?? 20, cancelacion))));

        cierres.MapPost("/{cierreId:int}/reimprimir", (int cierreId, ClaimsPrincipal usuario, IServicioCaja servicio, CancellationToken cancelacion) =>
            ConSesion(usuario, async sesion => Resultado(await servicio.ReimprimirCierreAsync(sesion, cierreId, cancelacion))));

        return aplicacion;
    }

    private static async Task<IResult> ConSesion(ClaimsPrincipal usuario, Func<SesionUsuario, Task<IResult>> accion) =>
        EmisorTokens.LeerSesion(usuario) is { } sesion ? await accion(sesion) : Results.Unauthorized();

    private static IResult Resultado(RespuestaCaja respuesta) =>
        respuesta.Exitosa ? Results.Ok(respuesta) : Results.UnprocessableEntity(respuesta);
}
