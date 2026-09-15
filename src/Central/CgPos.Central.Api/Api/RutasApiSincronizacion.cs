using System.Security.Claims;
using CgPos.Central.Api.Seguridad;
using CgPos.Central.Aplicacion.Sincronizacion;
using CgPos.Contratos.Sincronizacion;

namespace CgPos.Central.Api.Api;

public static class RutasApiSincronizacion
{
    public const string EncabezadoIdempotencia = "Idempotency-Key";

    public static IEndpointRouteBuilder MapearApiSincronizacion(this IEndpointRouteBuilder aplicacion)
    {
        var sincronizacion = aplicacion.MapGroup("/api/sincronizacion").RequireAuthorization(PoliticasCentral.Dispositivo);

        // Subida de la bandeja de salida de la caja: Recibido y Duplicado confirman; Rechazado (422) no, y la caja reintenta más tarde.
        sincronizacion.MapPost("/mensajes", async (MensajeSincronizacion mensaje, HttpContext http, ClaimsPrincipal usuario, IServicioRecepcion servicio,
                CancellationToken cancelacion) =>
            {
                if (EmisorTokensCentral.LeerDispositivo(usuario) is not { } caja)
                    return Results.Unauthorized();

                if (http.Request.Headers.TryGetValue(EncabezadoIdempotencia, out var clave)
                    && !string.Equals(clave.ToString(), mensaje.Id.ToString(), StringComparison.OrdinalIgnoreCase))
                    return Results.Json(new RespuestaRecepcionCentral(EstadoRecepcion.Rechazado, "La clave de idempotencia no coincide con el Id del mensaje."),
                        statusCode: StatusCodes.Status422UnprocessableEntity);

                var respuesta = await servicio.RecibirAsync(mensaje, new CajaRemitente(caja.CajaId, caja.SucursalId), cancelacion);
                return respuesta.Estado == EstadoRecepcion.Rechazado
                    ? Results.Json(respuesta, statusCode: StatusCodes.Status422UnprocessableEntity)
                    : Results.Ok(respuesta);
            });

        return aplicacion;
    }
}
