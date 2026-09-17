using System.Security.Claims;
using CgPos.Central.Api.Seguridad;
using CgPos.Central.Aplicacion.Abstracciones;
using CgPos.Central.Aplicacion.Seguridad;
using CgPos.Contratos.Central;
using CgPos.Dominio.Seguridad;
using static CgPos.Central.Api.Api.RespuestasAdministracion;

namespace CgPos.Central.Api.Api;

public static class RutasApiAdministracionSeguridad
{
    public static IEndpointRouteBuilder MapearApiAdministracionSeguridad(this IEndpointRouteBuilder aplicacion)
    {
        var seguridad = aplicacion.MapGroup("/api/seguridad").RequireAuthorization(CatalogoPermisosCentral.AdministrarSeguridad);

        seguridad.MapGet("/permisos", () =>
            Results.Ok(CatalogoPermisosCentral.Todos.Select(p => new DatosPermisoCentral(p.Codigo, p.Modulo, p.Descripcion)).ToList()));

        seguridad.MapGet("/roles", async (IServicioAdministracionSeguridad servicio, CancellationToken cancelacion) =>
            Results.Ok(await servicio.ListarRolesAsync(cancelacion)));

        seguridad.MapPost("/roles", async (SolicitudRolCentral solicitud, ClaimsPrincipal usuario, IServicioAdministracionSeguridad servicio, CancellationToken cancelacion) =>
            Responder(await servicio.CrearRolAsync(solicitud, Actor(usuario), cancelacion)));

        seguridad.MapPut("/roles/{rolId:int}", async (int rolId, SolicitudRolCentral solicitud, ClaimsPrincipal usuario, IServicioAdministracionSeguridad servicio,
                CancellationToken cancelacion) =>
            Responder(await servicio.ActualizarRolAsync(rolId, solicitud, Actor(usuario), cancelacion)));

        seguridad.MapPost("/roles/{rolId:int}/activar", async (int rolId, ClaimsPrincipal usuario, IServicioAdministracionSeguridad servicio, CancellationToken cancelacion) =>
            Responder(await servicio.CambiarEstadoRolAsync(rolId, true, Actor(usuario), cancelacion)));

        seguridad.MapPost("/roles/{rolId:int}/desactivar", async (int rolId, ClaimsPrincipal usuario, IServicioAdministracionSeguridad servicio, CancellationToken cancelacion) =>
            Responder(await servicio.CambiarEstadoRolAsync(rolId, false, Actor(usuario), cancelacion)));

        seguridad.MapGet("/usuarios", async (IServicioAdministracionSeguridad servicio, CancellationToken cancelacion) =>
            Results.Ok(await servicio.ListarUsuariosAsync(cancelacion)));

        seguridad.MapPost("/usuarios", async (SolicitudUsuarioCentral solicitud, ClaimsPrincipal usuario, IServicioAdministracionSeguridad servicio, CancellationToken cancelacion) =>
            Responder(await servicio.CrearUsuarioAsync(solicitud, Actor(usuario), cancelacion)));

        seguridad.MapPut("/usuarios/{usuarioId:int}", async (int usuarioId, SolicitudActualizarUsuarioCentral solicitud, ClaimsPrincipal usuario,
                IServicioAdministracionSeguridad servicio, CancellationToken cancelacion) =>
            Responder(await servicio.ActualizarUsuarioAsync(usuarioId, solicitud, Actor(usuario), cancelacion)));

        seguridad.MapPost("/usuarios/{usuarioId:int}/contrasena", async (int usuarioId, SolicitudContrasenaTemporal solicitud, ClaimsPrincipal usuario,
                IServicioAdministracionSeguridad servicio, CancellationToken cancelacion) =>
            Responder(await servicio.RestablecerContrasenaAsync(usuarioId, solicitud.ContrasenaTemporal ?? string.Empty, Actor(usuario), cancelacion)))
            .SinCache();

        seguridad.MapPost("/usuarios/{usuarioId:int}/desbloquear", async (int usuarioId, ClaimsPrincipal usuario, IServicioAdministracionSeguridad servicio,
                CancellationToken cancelacion) =>
            Responder(await servicio.DesbloquearAsync(usuarioId, Actor(usuario), cancelacion)));

        seguridad.MapPost("/usuarios/{usuarioId:int}/activar", async (int usuarioId, ClaimsPrincipal usuario, IServicioAdministracionSeguridad servicio,
                CancellationToken cancelacion) =>
            Responder(await servicio.CambiarEstadoUsuarioAsync(usuarioId, true, Actor(usuario), cancelacion)));

        seguridad.MapPost("/usuarios/{usuarioId:int}/desactivar", async (int usuarioId, ClaimsPrincipal usuario, IServicioAdministracionSeguridad servicio,
                CancellationToken cancelacion) =>
            Responder(await servicio.CambiarEstadoUsuarioAsync(usuarioId, false, Actor(usuario), cancelacion)));

        return aplicacion;
    }
}
