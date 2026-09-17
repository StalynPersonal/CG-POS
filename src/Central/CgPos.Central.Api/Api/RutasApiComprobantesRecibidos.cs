using CgPos.Central.Aplicacion.Ventas;
using CgPos.Dominio.Reportes;
using CgPos.Dominio.Seguridad;

namespace CgPos.Central.Api.Api;

/// <summary>Facturas y notas de crédito que subieron las cajas (M16): listado con filtros y detalle de cada comprobante.</summary>
public static class RutasApiComprobantesRecibidos
{
    public static IEndpointRouteBuilder MapearApiComprobantesRecibidos(this IEndpointRouteBuilder aplicacion)
    {
        var manager = aplicacion.MapGroup("/api/manager/facturas").RequireAuthorization(CatalogoPermisosCentral.ConsultarReportes);

        manager.MapGet("/", async (DateOnly desde, DateOnly hasta, int? sucursalId, int? cajaId, TipoComprobanteVenta? tipo, string? buscar, int? pagina,
                int? tamano, IServicioComprobantesRecibidos servicio, CancellationToken cancelacion) =>
            Results.Ok(await servicio.ListarAsync(new FiltroComprobantesRecibidos(desde, hasta, sucursalId, cajaId, tipo, buscar, pagina ?? 0, tamano ?? 25),
                cancelacion)));

        manager.MapGet("/{comprobanteId:int}", async (int comprobanteId, IServicioComprobantesRecibidos servicio, CancellationToken cancelacion) =>
            await servicio.ObtenerAsync(comprobanteId, cancelacion) is { } comprobante ? Results.Ok(comprobante) : Results.NotFound());

        return aplicacion;
    }
}
