using System.Security.Claims;
using CgPos.Central.Api.Seguridad;
using CgPos.Central.Aplicacion.Organizacion;
using CgPos.Central.Aplicacion.Seguridad;
using CgPos.Contratos.Central;

namespace CgPos.Central.Api.Api;

public static class RutasApiSesion
{
    public static IEndpointRouteBuilder MapearApiSesion(this IEndpointRouteBuilder aplicacion)
    {
        var sesion = aplicacion.MapGroup("/api/sesion").SinCache();

        sesion.MapPost("/ingreso", async (SolicitudIngresoCentral solicitud, HttpContext http, IServicioSesionesCentral servicio, EmisorTokensCentral emisor,
                IParametrosCentral parametros, CancellationToken cancelacion) =>
            {
                var duracion = await DuracionTokenAsync(parametros, cancelacion);
                var resultado = await servicio.IngresarAsync(solicitud.Usuario ?? string.Empty, solicitud.Contrasena ?? string.Empty, http.OrigenSolicitud(), cancelacion);
                return Responder(resultado, emisor, duracion, StatusCodes.Status401Unauthorized);
            });

        sesion.MapPost("/renovar", async (SolicitudRenovacionSesion solicitud, HttpContext http, IServicioSesionesCentral servicio, EmisorTokensCentral emisor,
                IParametrosCentral parametros, CancellationToken cancelacion) =>
            {
                var duracion = await DuracionTokenAsync(parametros, cancelacion);
                var resultado = await servicio.RenovarAsync(solicitud.TokenRenovacion ?? string.Empty, http.OrigenSolicitud(), cancelacion);
                return Responder(resultado, emisor, duracion, StatusCodes.Status401Unauthorized);
            });

        sesion.MapGet("/actual", (ClaimsPrincipal usuario) =>
                EmisorTokensCentral.LeerSesion(usuario) is { } actual ? Results.Ok(actual) : Results.Unauthorized())
            .RequireAuthorization(PoliticasCentral.Usuario);

        sesion.MapPost("/cerrar", async (ClaimsPrincipal usuario, IServicioSesionesCentral servicio, CancellationToken cancelacion) =>
            {
                if (EmisorTokensCentral.LeerSesion(usuario) is { } actual)
                    await servicio.CerrarAsync(actual.SesionId, actual.UsuarioId, cancelacion);

                return Results.NoContent();
            })
            .RequireAuthorization(PoliticasCentral.Usuario);

        sesion.MapPost("/contrasena", async (SolicitudCambioContrasena solicitud, ClaimsPrincipal usuario, HttpContext http, IServicioSesionesCentral servicio,
                EmisorTokensCentral emisor, IParametrosCentral parametros, CancellationToken cancelacion) =>
            {
                if (EmisorTokensCentral.LeerSesion(usuario) is not { } actual)
                    return Results.Unauthorized();

                var duracion = await DuracionTokenAsync(parametros, cancelacion);
                var resultado = await servicio.CambiarContrasenaAsync(actual.UsuarioId, solicitud.ContrasenaActual ?? string.Empty,
                    solicitud.ContrasenaNueva ?? string.Empty, http.OrigenSolicitud(), cancelacion);
                return Responder(resultado, emisor, duracion, StatusCodes.Status400BadRequest);
            })
            .RequireAuthorization(PoliticasCentral.Usuario);

        return aplicacion;
    }

    private static async Task<TimeSpan> DuracionTokenAsync(IParametrosCentral parametros, CancellationToken cancelacion) =>
        TimeSpan.FromMinutes(await parametros.ObtenerEnteroPositivoAsync(ClavesParametrosCentral.MinutosTokenAcceso, cancelacion));

    private static IResult Responder(ResultadoSesionCentral resultado, EmisorTokensCentral emisor, TimeSpan duracionAcceso, int codigoRechazo)
    {
        if (resultado.Sesion is { } sesion)
        {
            var (token, expiraEn) = emisor.EmitirUsuario(sesion, duracionAcceso);
            return Results.Ok(new RespuestaSesionCentral(true, TokenAcceso: token, AccesoExpiraEn: expiraEn, TokenRenovacion: resultado.TokenRenovacion,
                RenovacionExpiraEn: resultado.RenovacionExpiraEn, Sesion: EmisorTokensCentral.ConvertirDto(sesion)));
        }

        var mensaje = MensajesSeguridadCentral.Para(resultado.Motivo!.Value, resultado.BloqueadoHasta, resultado.Detalle);
        return Results.Json(new RespuestaSesionCentral(false, mensaje, BloqueadoHasta: resultado.BloqueadoHasta), statusCode: codigoRechazo);
    }
}
