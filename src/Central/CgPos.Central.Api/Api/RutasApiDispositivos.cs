using System.Security.Claims;
using CgPos.Central.Api.Seguridad;
using CgPos.Central.Aplicacion.Abstracciones;
using CgPos.Central.Aplicacion.Dispositivos;
using CgPos.Central.Aplicacion.Organizacion;
using CgPos.Contratos.Central;
using CgPos.Dominio.Seguridad;

namespace CgPos.Central.Api.Api;

public static class RutasApiDispositivos
{
    public static IEndpointRouteBuilder MapearApiDispositivos(this IEndpointRouteBuilder aplicacion)
    {
        var dispositivos = aplicacion.MapGroup("/api/dispositivos");

        // Público: la caja presenta su credencial y recibe un token de pocos minutos.
        dispositivos.MapPost("/token", async (SolicitudTokenDispositivo solicitud, HttpContext http, IServicioDispositivos servicio, EmisorTokensCentral emisor,
                IParametrosCentral parametros, CancellationToken cancelacion) =>
            {
                var minutos = await parametros.ObtenerEnteroPositivoAsync(ClavesParametrosCentral.MinutosTokenDispositivo, cancelacion);
                var resultado = await servicio.AutenticarAsync(solicitud.SucursalCodigo, solicitud.CajaCodigo, solicitud.Secreto ?? string.Empty, http.OrigenSolicitud(), cancelacion);
                if (resultado.Dispositivo is not { } dispositivo)
                    return Results.Json(new RespuestaTokenDispositivo(false, MensajesDispositivos.Para(resultado.Motivo!.Value)), statusCode: StatusCodes.Status401Unauthorized);

                var (token, expiraEn) = emisor.EmitirDispositivo(dispositivo, TimeSpan.FromMinutes(minutos));
                return Results.Ok(new RespuestaTokenDispositivo(true, Token: token, ExpiraEn: expiraEn));
            })
            .SinCache();

        dispositivos.MapGet("/actual", (ClaimsPrincipal usuario) =>
                EmisorTokensCentral.LeerDispositivo(usuario) is { } dispositivo ? Results.Ok(dispositivo) : Results.Unauthorized())
            .RequireAuthorization(PoliticasCentral.Dispositivo);

        var credencial = aplicacion.MapGroup("/api/cajas/{cajaId:int}/credencial")
            .RequireAuthorization(CatalogoPermisosCentral.AdministrarDispositivos)
            .SinCache();

        credencial.MapPost("", async (int cajaId, ClaimsPrincipal usuario, IServicioDispositivos servicio, CancellationToken cancelacion) =>
        {
            if (EmisorTokensCentral.LeerSesion(usuario) is not { } sesion)
                return Results.Unauthorized();

            var emitida = await servicio.EmitirCredencialAsync(cajaId, new UsuarioAuditoria(sesion.UsuarioId, sesion.Nombre), cancelacion);
            return emitida is null
                ? Results.NotFound()
                : Results.Ok(new DatosCredencialDispositivo(emitida.CajaId, emitida.SucursalCodigo, emitida.CajaCodigo, emitida.Secreto, emitida.EmitidaEn));
        });

        credencial.MapPost("/revocar", async (int cajaId, SolicitudRevocacionCredencial solicitud, ClaimsPrincipal usuario, IServicioDispositivos servicio,
            CancellationToken cancelacion) =>
        {
            if (EmisorTokensCentral.LeerSesion(usuario) is not { } sesion)
                return Results.Unauthorized();
            if (string.IsNullOrWhiteSpace(solicitud.Motivo))
                return Results.Text("Indique el motivo de la revocación.", statusCode: StatusCodes.Status400BadRequest);

            return await servicio.RevocarCredencialAsync(cajaId, solicitud.Motivo, new UsuarioAuditoria(sesion.UsuarioId, sesion.Nombre), cancelacion)
                ? Results.NoContent()
                : Results.NotFound();
        });

        return aplicacion;
    }
}
