using System.Security.Claims;
using System.Text;
using CgPos.Central.Aplicacion.Sincronizacion;
using CgPos.Contratos.Central;
using CgPos.Dominio.Seguridad;
using CgPos.Dominio.Sincronizacion;
using static CgPos.Central.Api.Api.RespuestasAdministracion;

namespace CgPos.Central.Api.Api;

public static class RutasApiMonitor
{
    private const int TamanoPaginaPredeterminado = 10;

    public static IEndpointRouteBuilder MapearApiMonitor(this IEndpointRouteBuilder aplicacion)
    {
        var grupo = aplicacion.MapGroup("/api/monitor").RequireAuthorization(CatalogoPermisosCentral.MonitorearSincronizacion);

        grupo.MapGet("/", async (IServicioMonitorCentral servicio, CancellationToken cancelacion) => Results.Ok(await servicio.ObtenerAsync(cancelacion)));

        grupo.MapGet("/comprobantes", async (EstadoEnvioDgii? estado, int? sucursalId, int? cajaId, string? buscar, bool? soloConFallo, int? pagina, int? tamano,
                IServicioMonitorCentral servicio, CancellationToken cancelacion) =>
            Results.Ok(await servicio.BuscarComprobantesAsync(estado, sucursalId, cajaId, buscar, soloConFallo ?? false, pagina ?? 0, tamano ?? TamanoPaginaPredeterminado, cancelacion)));

        grupo.MapGet("/comprobantes/{comprobanteId:int}/xml", async (int comprobanteId, IServicioMonitorCentral servicio, CancellationToken cancelacion) =>
            await servicio.ObtenerXmlAsync(comprobanteId, cancelacion) is { } xml ? Results.Text(xml, "application/xml", Encoding.UTF8) : Results.NotFound());

        grupo.MapPost("/comprobantes/{comprobanteId:int}/reenviar", async (int comprobanteId, ClaimsPrincipal usuario, IServicioMonitorCentral servicio,
                CancellationToken cancelacion) =>
            Responder(await servicio.ReenviarAsync(comprobanteId, Actor(usuario), cancelacion)));

        grupo.MapGet("/conflictos", async (bool? abiertos, IServicioMonitorCentral servicio, CancellationToken cancelacion) =>
            Results.Ok(await servicio.ListarConflictosAsync(abiertos ?? true, cancelacion)));

        grupo.MapPost("/conflictos/{conflictoId:int}/resolver", async (int conflictoId, SolicitudResolverConflicto solicitud, ClaimsPrincipal usuario,
                IServicioMonitorCentral servicio, CancellationToken cancelacion) =>
            Responder(await servicio.ResolverConflictoAsync(conflictoId, solicitud.Resolucion, Actor(usuario), cancelacion)));

        return aplicacion;
    }
}
