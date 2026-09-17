using CgPos.Central.Aplicacion.Entregas;
using CgPos.Dominio.Entregas;
using CgPos.Dominio.Seguridad;

namespace CgPos.Central.Api.Api;

/// <summary>Pendientes de entrega y envíos de todas las sucursales vistos desde el Central Manager.</summary>
public static class RutasApiDespacho
{
    private const int TamanoPaginaPredeterminado = 25;

    public static IEndpointRouteBuilder MapearApiDespacho(this IEndpointRouteBuilder aplicacion)
    {
        var manager = aplicacion.MapGroup("/api/manager/despacho").RequireAuthorization(CatalogoPermisosCentral.OperarDespacho);

        manager.MapGet("/pendientes", async (string? buscar, EstadoPendiente? estado, MetodoEntrega? metodo, int? sucursalId, bool? soloAtrasados,
                bool? soloAbiertos, int? pagina, int? tamano, IServicioDespachoCentral servicio, CancellationToken cancelacion) =>
            Results.Ok(await servicio.ListarAsync(buscar, estado, metodo, sucursalId, soloAtrasados ?? false, soloAbiertos ?? false, pagina ?? 0,
                tamano ?? TamanoPaginaPredeterminado, cancelacion)));

        manager.MapGet("/pendientes/{pendienteId:int}", async (int pendienteId, IServicioDespachoCentral servicio, CancellationToken cancelacion) =>
            await servicio.ObtenerAsync(pendienteId, cancelacion) is { } detalle ? Results.Ok(detalle) : Results.NotFound());

        manager.MapGet("/resumen", async (IServicioDespachoCentral servicio, CancellationToken cancelacion) =>
            Results.Ok(await servicio.ResumenAsync(cancelacion)));

        return aplicacion;
    }
}
