using System.Security.Claims;
using CgPos.Central.Aplicacion.Dgii;
using CgPos.Central.Aplicacion.Organizacion;
using CgPos.Contratos.Central;
using CgPos.Dominio.Seguridad;
using static CgPos.Central.Api.Api.RespuestasAdministracion;

namespace CgPos.Central.Api.Api;

public static class RutasApiConfiguracionCajas
{
    public static IEndpointRouteBuilder MapearApiConfiguracionCajas(this IEndpointRouteBuilder aplicacion)
    {
        var fiscal = aplicacion.MapGroup("/api/fiscal").RequireAuthorization(CatalogoPermisosCentral.AdministrarFiscal);

        fiscal.MapGet("/secuencias", async (IServicioConfiguracionCajas servicio, CancellationToken cancelacion) =>
            Results.Ok(await servicio.ListarSecuenciasAsync(cancelacion)));

        fiscal.MapPost("/secuencias", async (SolicitudSecuenciaEcf solicitud, ClaimsPrincipal usuario, IServicioConfiguracionCajas servicio, CancellationToken cancelacion) =>
            Responder(await servicio.AsignarSecuenciaAsync(solicitud, Actor(usuario), cancelacion)));

        fiscal.MapPut("/secuencias/{secuenciaId:int}", async (int secuenciaId, SolicitudActualizarSecuenciaEcf solicitud, ClaimsPrincipal usuario,
                IServicioConfiguracionCajas servicio, CancellationToken cancelacion) =>
            Responder(await servicio.ActualizarSecuenciaAsync(secuenciaId, solicitud, Actor(usuario), cancelacion)));

        fiscal.MapGet("/anulaciones", async (IServicioAnulacionesEcf servicio, CancellationToken cancelacion) =>
            Results.Ok(await servicio.ListarAsync(cancelacion)));

        fiscal.MapPost("/secuencias/{secuenciaId:int}/anulaciones", async (int secuenciaId, SolicitudAnulacionEcf solicitud, ClaimsPrincipal usuario,
                IServicioAnulacionesEcf servicio, CancellationToken cancelacion) =>
            Responder(await servicio.AnularAsync(secuenciaId, solicitud, Actor(usuario), cancelacion)));

        var usuariosCaja = aplicacion.MapGroup("/api/usuarios-caja").RequireAuthorization(CatalogoPermisosCentral.AdministrarUsuariosCaja);

        usuariosCaja.MapGet("/permisos", () => Results.Ok(CatalogoPermisos.Todos));

        usuariosCaja.MapGet("/roles", async (IServicioConfiguracionCajas servicio, CancellationToken cancelacion) =>
            Results.Ok(await servicio.ListarRolesCajaAsync(cancelacion)));

        usuariosCaja.MapPost("/roles", async (SolicitudRolCaja solicitud, ClaimsPrincipal usuario, IServicioConfiguracionCajas servicio, CancellationToken cancelacion) =>
            Responder(await servicio.GuardarRolCajaAsync(null, solicitud, Actor(usuario), cancelacion)));

        usuariosCaja.MapPut("/roles/{rolId:int}", async (int rolId, SolicitudRolCaja solicitud, ClaimsPrincipal usuario, IServicioConfiguracionCajas servicio,
                CancellationToken cancelacion) =>
            Responder(await servicio.GuardarRolCajaAsync(rolId, solicitud, Actor(usuario), cancelacion)));

        usuariosCaja.MapGet("/usuarios", async (IServicioConfiguracionCajas servicio, CancellationToken cancelacion) =>
            Results.Ok(await servicio.ListarUsuariosCajaAsync(cancelacion)));

        usuariosCaja.MapPost("/usuarios", async (SolicitudUsuarioCaja solicitud, ClaimsPrincipal usuario, IServicioConfiguracionCajas servicio, CancellationToken cancelacion) =>
            Responder(await servicio.GuardarUsuarioCajaAsync(null, solicitud, Actor(usuario), cancelacion)));

        usuariosCaja.MapPut("/usuarios/{usuarioId:int}", async (int usuarioId, SolicitudUsuarioCaja solicitud, ClaimsPrincipal usuario, IServicioConfiguracionCajas servicio,
                CancellationToken cancelacion) =>
            Responder(await servicio.GuardarUsuarioCajaAsync(usuarioId, solicitud, Actor(usuario), cancelacion)));

        return aplicacion;
    }
}
