using System.Security.Claims;
using CgPos.Contratos.Ventas;
using CgPos.Dominio.Fiscal;
using CgPos.Pos.Agente.Seguridad;
using CgPos.Pos.Aplicacion.Ecf;

namespace CgPos.Pos.Agente.Api;

/// <summary>Estado fiscal de la caja, carga del certificado con su PIN y consulta de e-CF emitidos.</summary>
public static class RutasApiEcf
{
    public static IEndpointRouteBuilder MapearApiEcf(this IEndpointRouteBuilder aplicacion)
    {
        var api = aplicacion.MapGroup("/api/ecf").RequireAuthorization();

        api.MapGet("/estado", async (ClaimsPrincipal usuario, IServicioEcf servicio, CancellationToken cancelacion) =>
            EmisorTokens.LeerSesion(usuario) is { } sesion ? Results.Ok(await servicio.ObtenerEstadoAsync(sesion, cancelacion)) : Results.Unauthorized());

        // Conocer el PIN del certificado es la autorización: no se exige un permiso adicional (RF-217).
        api.MapPost("/certificado", async (SolicitudCargarCertificado solicitud, ClaimsPrincipal usuario, IServicioEcf servicio, CancellationToken cancelacion) =>
        {
            if (EmisorTokens.LeerSesion(usuario) is not { } sesion)
                return Results.Unauthorized();

            var respuesta = await servicio.CargarCertificadoAsync(sesion, solicitud.Pin ?? string.Empty, cancelacion);
            return respuesta.Correcto ? Results.Ok(respuesta) : Results.UnprocessableEntity(respuesta);
        });

        api.MapGet("/documentos", async (EstadoDocumentoElectronico? estado, int? maximo, ClaimsPrincipal usuario, IServicioEcf servicio, CancellationToken cancelacion) =>
            EmisorTokens.LeerSesion(usuario) is { } sesion
                ? Results.Ok(await servicio.ListarDocumentosAsync(sesion, estado, maximo ?? 100, cancelacion))
                : Results.Unauthorized());

        return aplicacion;
    }
}
