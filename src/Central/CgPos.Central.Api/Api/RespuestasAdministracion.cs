using System.Security.Claims;
using CgPos.Central.Api.Seguridad;
using CgPos.Central.Aplicacion.Abstracciones;
using CgPos.Central.Aplicacion.Seguridad;
using CgPos.Contratos.Central;

namespace CgPos.Central.Api.Api;

/// <summary>Respuestas comunes de las API de administración del Central Manager.</summary>
internal static class RespuestasAdministracion
{
    public static UsuarioAuditoria Actor(ClaimsPrincipal usuario) =>
        EmisorTokensCentral.LeerSesion(usuario) is { } sesion
            ? new UsuarioAuditoria(sesion.UsuarioId, sesion.Nombre)
            : throw new InvalidOperationException("La operación requiere un usuario del Central.");

    public static IResult Responder(ResultadoAdministracion resultado) =>
        resultado switch
        {
            { Exitosa: true } => Results.Ok(new RespuestaAdministracion(true, resultado.Mensaje, resultado.Id, resultado.Advertencia)),
            { NoEncontrado: true } => Results.Json(new RespuestaAdministracion(false, resultado.Mensaje), statusCode: StatusCodes.Status404NotFound),
            _ => Results.Json(new RespuestaAdministracion(false, resultado.Mensaje), statusCode: StatusCodes.Status400BadRequest),
        };
}
