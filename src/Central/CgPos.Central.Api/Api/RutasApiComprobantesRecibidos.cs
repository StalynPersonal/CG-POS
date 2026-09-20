using System.Security.Claims;
using CgPos.Central.Api.Seguridad;
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

        // Las cajas consultan facturas de cualquier sucursal para devolverlas: el Central es el único que ve todas las
        // devoluciones de la empresa, así que también dice cuánto queda disponible de cada línea.
        var cajas = aplicacion.MapGroup("/api/facturas").RequireAuthorization(PoliticasCentral.Dispositivo);

        cajas.MapGet("/{numero}", async (string numero, ClaimsPrincipal usuario, IServicioFacturasParaCaja servicio, CancellationToken cancelacion) =>
            EmisorTokensCentral.LeerDispositivo(usuario) is not { } caja
                ? Results.Unauthorized()
                : await servicio.BuscarAsync(numero, caja.CajaId, cancelacion) is { } factura
                    ? Results.Ok(factura)
                    : Results.NotFound());

        cajas.MapGet("/", async (string? buscar, DateOnly? desde, DateOnly? hasta, IServicioFacturasParaCaja servicio, CancellationToken cancelacion) =>
        {
            var hoy = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(-4).Date);
            return Results.Ok(await servicio.ListarAsync(buscar, desde ?? hoy.AddMonths(-3), hasta ?? hoy, cancelacion));
        });

        return aplicacion;
    }
}
