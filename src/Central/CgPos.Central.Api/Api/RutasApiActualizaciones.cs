using System.Security.Claims;
using CgPos.Central.Api.Seguridad;
using CgPos.Central.Aplicacion.Actualizaciones;
using CgPos.Contratos.Central;
using CgPos.Dominio.Seguridad;

namespace CgPos.Central.Api.Api;

/// <summary>Actualización remota del Agente de las cajas: la caja consulta la versión publicada, descarga el paquete y reporta lo que instaló.</summary>
public static class RutasApiActualizaciones
{
    public static IEndpointRouteBuilder MapearApiActualizaciones(this IEndpointRouteBuilder aplicacion)
    {
        var cajas = aplicacion.MapGroup("/api/actualizaciones/caja").RequireAuthorization(PoliticasCentral.Dispositivo);

        cajas.MapGet("/", async (IServicioActualizacionesCaja servicio, CancellationToken cancelacion) =>
            await servicio.PublicadaAsync(cancelacion) is { } actualizacion ? Results.Ok(actualizacion) : Results.NoContent());

        cajas.MapGet("/paquete", async (IServicioActualizacionesCaja servicio, CancellationToken cancelacion) =>
        {
            var publicada = await servicio.PublicadaAsync(cancelacion);
            var paquete = publicada is null ? null : await servicio.AbrirPaqueteAsync(cancelacion);
            return paquete is null ? Results.NotFound() : Results.File(paquete, "application/zip", publicada!.Archivo);
        });

        cajas.MapPost("/version", async (SolicitudVersionCaja solicitud, ClaimsPrincipal usuario, IServicioActualizacionesCaja servicio,
                CancellationToken cancelacion) =>
        {
            if (EmisorTokensCentral.LeerDispositivo(usuario) is not { } caja)
                return Results.Unauthorized();
            if (string.IsNullOrWhiteSpace(solicitud.Version))
                return Results.BadRequest("La versión es obligatoria.");

            await servicio.ReportarVersionAsync(caja.CajaId, solicitud.Version.Trim(), cancelacion);
            return Results.NoContent();
        });

        // Avance del despliegue, para el Central Manager.
        aplicacion.MapGet("/api/manager/actualizaciones/cajas", async (IServicioActualizacionesCaja servicio, CancellationToken cancelacion) =>
            Results.Ok(await servicio.VersionesAsync(cancelacion)))
            .RequireAuthorization(CatalogoPermisosCentral.AdministrarOrganizacion);

        aplicacion.MapGet("/api/manager/actualizaciones/publicada", async (IServicioActualizacionesCaja servicio, CancellationToken cancelacion) =>
            await servicio.PublicadaAsync(cancelacion) is { } actualizacion ? Results.Ok(actualizacion) : Results.NoContent())
            .RequireAuthorization(CatalogoPermisosCentral.AdministrarOrganizacion);

        return aplicacion;
    }
}
