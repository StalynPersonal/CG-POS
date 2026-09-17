using System.Security.Claims;
using CgPos.Central.Aplicacion.Fidelidad;
using CgPos.Contratos.Central;
using CgPos.Dominio.Seguridad;
using static CgPos.Central.Api.Api.RespuestasAdministracion;

namespace CgPos.Central.Api.Api;

/// <summary>Programa de fidelidad en el Central Manager: saldo oficial de puntos, movimientos de todas las sucursales y ajustes.</summary>
public static class RutasApiFidelidad
{
    private const int TamanoPaginaPredeterminado = 25;

    public static IEndpointRouteBuilder MapearApiFidelidad(this IEndpointRouteBuilder aplicacion)
    {
        var manager = aplicacion.MapGroup("/api/manager/fidelidad").RequireAuthorization(CatalogoPermisosCentral.AdministrarFidelidad);

        manager.MapGet("/miembros", async (string? buscar, bool? soloConPuntos, int? pagina, int? tamano, IServicioFidelidadCentral servicio,
                CancellationToken cancelacion) =>
            Results.Ok(await servicio.ListarAsync(buscar, soloConPuntos ?? false, pagina ?? 0, tamano ?? TamanoPaginaPredeterminado, cancelacion)));

        manager.MapGet("/miembros/{miembroId:int}", async (int miembroId, IServicioFidelidadCentral servicio, CancellationToken cancelacion) =>
            await servicio.ObtenerAsync(miembroId, cancelacion) is { } miembro ? Results.Ok(miembro) : Results.NotFound());

        manager.MapGet("/miembros/{miembroId:int}/movimientos", async (int miembroId, IServicioFidelidadCentral servicio, CancellationToken cancelacion) =>
            Results.Ok(await servicio.ListarMovimientosAsync(miembroId, cancelacion)));

        // Ajuste manual a favor o en contra: exige motivo y queda en la auditoría.
        manager.MapPost("/miembros/{miembroId:int}/ajustes", async (int miembroId, SolicitudAjustePuntos solicitud, ClaimsPrincipal usuario,
                IServicioFidelidadCentral servicio, CancellationToken cancelacion) =>
            Results.Ok(await servicio.AjustarAsync(miembroId, solicitud.Puntos, solicitud.Motivo, Actor(usuario), cancelacion)));

        return aplicacion;
    }
}
