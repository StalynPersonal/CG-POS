using System.Security.Claims;
using CgPos.Contratos.Fidelidad;
using CgPos.Pos.Agente.Seguridad;
using CgPos.Pos.Aplicacion.Fidelidad;
using CgPos.Pos.Aplicacion.Seguridad;

namespace CgPos.Pos.Agente.Api;

/// <summary>Programa de fidelidad (M11): consulta de saldo e inscripción desde la caja. Los rechazos responden 422.</summary>
public static class RutasApiFidelidad
{
    public static IEndpointRouteBuilder MapearApiFidelidad(this IEndpointRouteBuilder aplicacion)
    {
        var api = aplicacion.MapGroup("/api/fidelidad").RequireAuthorization();

        api.MapGet("/miembros/{cedula}", (string cedula, ClaimsPrincipal usuario, IServicioFidelidad servicio, CancellationToken cancelacion) =>
            ConSesion(usuario, async sesion =>
            {
                var respuesta = await servicio.ConsultarAsync(sesion, cedula, cancelacion);
                return respuesta.Exitosa ? Results.Ok(respuesta) : Results.UnprocessableEntity(respuesta);
            }));

        api.MapPost("/miembros", (SolicitudInscripcionFidelidad solicitud, ClaimsPrincipal usuario, IServicioFidelidad servicio, CancellationToken cancelacion) =>
            ConSesion(usuario, async sesion =>
            {
                var respuesta = await servicio.InscribirAsync(sesion, solicitud, cancelacion);
                return respuesta.Exitosa ? Results.Ok(respuesta) : Results.UnprocessableEntity(respuesta);
            }));

        return aplicacion;
    }

    private static async Task<IResult> ConSesion(ClaimsPrincipal usuario, Func<SesionUsuario, Task<IResult>> accion) =>
        EmisorTokens.LeerSesion(usuario) is { } sesion ? await accion(sesion) : Results.Unauthorized();
}
