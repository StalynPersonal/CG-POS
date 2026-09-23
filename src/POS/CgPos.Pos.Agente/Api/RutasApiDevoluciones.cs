using System.Security.Claims;
using CgPos.Contratos.Ventas;
using CgPos.Pos.Agente.Seguridad;
using CgPos.Pos.Aplicacion.Devoluciones;
using CgPos.Pos.Aplicacion.Seguridad;
using CgPos.Dominio.Seguridad;

namespace CgPos.Pos.Agente.Api;

/// <summary>Devoluciones y notas de crédito (M10). La autorización del encargado se valida en el servicio; los rechazos responden 422.</summary>
public static class RutasApiDevoluciones
{
    public static IEndpointRouteBuilder MapearApiDevoluciones(this IEndpointRouteBuilder aplicacion)
    {
        // Sin el permiso no se entra al módulo: una caja queda dedicada a ventas quitándoselo a su rol.
        var api = aplicacion.MapGroup("/api/devoluciones").RequireAuthorization(CatalogoPermisos.RegistrarDevolucion);

        api.MapGet("/motivos", async (IServicioDevoluciones servicio, CancellationToken cancelacion) =>
            Results.Ok(await servicio.ListarMotivosAsync(cancelacion)));

        api.MapGet("/factura/{numero}", (string numero, ClaimsPrincipal usuario, IServicioDevoluciones servicio, CancellationToken cancelacion) =>
            ConSesion(usuario, async sesion =>
            {
                var respuesta = await servicio.BuscarFacturaAsync(sesion, numero, cancelacion);
                return respuesta.Factura is not null ? Results.Ok(respuesta) : Results.UnprocessableEntity(respuesta);
            }));

        api.MapPost("/", (SolicitudDevolucion solicitud, ClaimsPrincipal usuario, IServicioDevoluciones servicio, CancellationToken cancelacion) =>
            ConSesion(usuario, async sesion =>
            {
                var respuesta = await servicio.RegistrarAsync(sesion, solicitud, cancelacion);
                return respuesta.Exitosa ? Results.Ok(respuesta) : Results.UnprocessableEntity(respuesta);
            }));

        api.MapGet("/notas-credito/{codigo}", (string codigo, ClaimsPrincipal usuario, IServicioDevoluciones servicio, CancellationToken cancelacion) =>
            ConSesion(usuario, async sesion =>
            {
                var respuesta = await servicio.ConsultarNotaCreditoAsync(sesion, codigo, cancelacion);
                return respuesta.NotaCredito is not null ? Results.Ok(respuesta) : Results.UnprocessableEntity(respuesta);
            }));

        api.MapPost("/{devolucionId:int}/reimprimir", (int devolucionId, ClaimsPrincipal usuario, IServicioDevoluciones servicio, CancellationToken cancelacion) =>
            ConSesion(usuario, async sesion =>
            {
                var respuesta = await servicio.ReimprimirAsync(sesion, devolucionId, cancelacion);
                return respuesta.Exitosa ? Results.Ok(respuesta) : Results.UnprocessableEntity(respuesta);
            }));

        return aplicacion;
    }

    private static async Task<IResult> ConSesion(ClaimsPrincipal usuario, Func<SesionUsuario, Task<IResult>> accion) =>
        EmisorTokens.LeerSesion(usuario) is { } sesion ? await accion(sesion) : Results.Unauthorized();
}
