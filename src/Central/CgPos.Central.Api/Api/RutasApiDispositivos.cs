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
                var resultado = await servicio.AutenticarAsync(solicitud.SucursalCodigo, solicitud.CajaCodigo, solicitud.Secreto ?? string.Empty,
                    solicitud.HuellaEquipo, http.OrigenSolicitud(), cancelacion);
                if (resultado.Dispositivo is not { } dispositivo)
                    return Results.Json(new RespuestaTokenDispositivo(false, MensajesDispositivos.Para(resultado.Motivo!.Value)), statusCode: StatusCodes.Status401Unauthorized);

                var (token, expiraEn) = emisor.EmitirDispositivo(dispositivo, TimeSpan.FromMinutes(minutos));
                return Results.Ok(new RespuestaTokenDispositivo(true, Token: token, ExpiraEn: expiraEn));
            })
            .SinCache();

        // Público, como el token: una caja que todavía no tiene credencial no puede autenticarse para pedirla. Lo que la
        // protege es que nadie recibe nada hasta que una persona acepte la solicitud en el Central.
        dispositivos.MapPost("/solicitud", async (SolicitudEnrolamientoCaja solicitud, HttpContext http, IServicioDispositivos servicio,
                CancellationToken cancelacion) =>
            {
                if (solicitud is null || string.IsNullOrWhiteSpace(solicitud.HuellaEquipo) || string.IsNullOrWhiteSpace(solicitud.Token))
                    return Results.Text("La solicitud debe traer la huella del equipo y su token.", statusCode: StatusCodes.Status400BadRequest);

                ResultadoEnrolamiento resultado;
                try
                {
                    resultado = await servicio.SolicitarEnrolamientoAsync(solicitud, http.OrigenSolicitud(), cancelacion);
                }
                catch (ArgumentException excepcion)
                {
                    return Results.Text(excepcion.Message, statusCode: StatusCodes.Status400BadRequest);
                }

                var respuesta = new RespuestaEnrolamientoCaja(resultado.Estado, resultado.Secreto, resultado.Mensaje);
                return resultado.Estado switch
                {
                    // Aceptada: aquí va la credencial, y solo aquí.
                    EstadoEnrolamientoCaja.Entregada => Results.Ok(respuesta),

                    // Registrada y esperando que alguien la mire: la caja vuelve a preguntar más tarde.
                    EstadoEnrolamientoCaja.Pendiente => Results.Json(respuesta, statusCode: StatusCodes.Status202Accepted),

                    _ => Results.Json(respuesta, statusCode: StatusCodes.Status403Forbidden),
                };
            })
            .SinCache();

        var solicitudes = aplicacion.MapGroup("/api/enrolamiento")
            .RequireAuthorization(CatalogoPermisosCentral.AdministrarDispositivos)
            .SinCache();

        solicitudes.MapGet("", async (bool? pendientes, IServicioDispositivos servicio, CancellationToken cancelacion) =>
            Results.Ok(await servicio.ListarSolicitudesAsync(pendientes ?? false, cancelacion)));

        solicitudes.MapPost("/{solicitudId:int}/aprobar", async (int solicitudId, ClaimsPrincipal usuario, IServicioDispositivos servicio,
            CancellationToken cancelacion) =>
        {
            if (EmisorTokensCentral.LeerSesion(usuario) is not { } sesion)
                return Results.Unauthorized();

            var problema = await servicio.AprobarSolicitudAsync(solicitudId, new UsuarioAuditoria(sesion.UsuarioId, sesion.Nombre), cancelacion);
            return problema is null ? Results.NoContent() : Results.Text(problema, statusCode: StatusCodes.Status409Conflict);
        });

        solicitudes.MapPost("/{solicitudId:int}/rechazar", async (int solicitudId, SolicitudMotivo solicitud, ClaimsPrincipal usuario,
            IServicioDispositivos servicio, CancellationToken cancelacion) =>
        {
            if (EmisorTokensCentral.LeerSesion(usuario) is not { } sesion)
                return Results.Unauthorized();
            if (string.IsNullOrWhiteSpace(solicitud?.Motivo))
                return Results.Text("Indique el motivo del rechazo.", statusCode: StatusCodes.Status400BadRequest);

            return await servicio.RechazarSolicitudAsync(solicitudId, solicitud.Motivo, new UsuarioAuditoria(sesion.UsuarioId, sesion.Nombre), cancelacion)
                ? Results.NoContent()
                : Results.NotFound();
        });

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

        credencial.MapPost("/liberar-equipo", async (int cajaId, SolicitudMotivo solicitud, ClaimsPrincipal usuario, IServicioDispositivos servicio,
            CancellationToken cancelacion) =>
        {
            if (EmisorTokensCentral.LeerSesion(usuario) is not { } sesion)
                return Results.Unauthorized();
            if (string.IsNullOrWhiteSpace(solicitud?.Motivo))
                return Results.Text("Indique por qué se libera el equipo.", statusCode: StatusCodes.Status400BadRequest);

            return await servicio.LiberarEquipoAsync(cajaId, solicitud.Motivo, new UsuarioAuditoria(sesion.UsuarioId, sesion.Nombre), cancelacion)
                ? Results.NoContent()
                : Results.NotFound();
        });

        return aplicacion;
    }
}
