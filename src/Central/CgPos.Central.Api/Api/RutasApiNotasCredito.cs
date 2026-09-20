using System.Security.Claims;
using CgPos.Central.Api.Seguridad;
using CgPos.Central.Aplicacion.Devoluciones;
using CgPos.Contratos.Central;
using CgPos.Dominio.Devoluciones;
using CgPos.Dominio.Seguridad;
using static CgPos.Central.Api.Api.RespuestasAdministracion;

namespace CgPos.Central.Api.Api;

/// <summary>Notas de crédito de todas las sucursales: consulta y reserva de saldo para las cajas, y revisión en el Central Manager.</summary>
public static class RutasApiNotasCredito
{
    private const int TamanoPaginaPredeterminado = 10;

    public static IEndpointRouteBuilder MapearApiNotasCredito(this IEndpointRouteBuilder aplicacion)
    {
        var cajas = aplicacion.MapGroup("/api/notas-credito").RequireAuthorization(PoliticasCentral.Dispositivo);

        // La caja consulta el saldo de una nota emitida en cualquier sucursal (RF-43).
        cajas.MapGet("/{codigo}", async (string codigo, IServicioNotasCreditoCentral servicio, CancellationToken cancelacion) =>
            await servicio.BuscarParaCajaAsync(codigo, cancelacion) is { } nota ? Results.Ok(nota) : Results.NotFound());

        // Retiene el saldo para una factura mientras la caja cobra; si no cobra, lo libera con el mismo número o la reserva vence sola.
        cajas.MapPost("/{numero}/reservas", async (string numero, SolicitudReservaNotaCredito solicitud, ClaimsPrincipal usuario,
                IServicioNotasCreditoCentral servicio, CancellationToken cancelacion) =>
            EmisorTokensCentral.LeerDispositivo(usuario) is { } caja
                ? Results.Ok(await servicio.ReservarAsync(numero, caja.CajaId, solicitud.VentaNumero, solicitud.Monto, cancelacion))
                : Results.Unauthorized());

        cajas.MapDelete("/{numero}/reservas/{ventaNumero}", async (string numero, string ventaNumero, ClaimsPrincipal usuario,
                IServicioNotasCreditoCentral servicio, CancellationToken cancelacion) =>
            EmisorTokensCentral.LeerDispositivo(usuario) is not { } caja ? Results.Unauthorized()
                : await servicio.LiberarReservaAsync(numero, caja.CajaId, ventaNumero, cancelacion) ? Results.NoContent()
                : Results.NotFound());

        var manager = aplicacion.MapGroup("/api/manager/notas-credito").RequireAuthorization(CatalogoPermisosCentral.AdministrarNotasCredito);

        manager.MapGet("/", async (string? buscar, EstadoNotaCreditoCentral? estado, bool? soloSobregiradas, int? pagina, int? tamano,
                DateOnly? desde, DateOnly? hasta, IServicioNotasCreditoCentral servicio, CancellationToken cancelacion) =>
            Results.Ok(await servicio.ListarAsync(buscar, estado, soloSobregiradas ?? false, pagina ?? 0, tamano ?? TamanoPaginaPredeterminado,
                desde, hasta, cancelacion)));

        manager.MapGet("/{notaCreditoId:int}/movimientos", async (int notaCreditoId, IServicioNotasCreditoCentral servicio, CancellationToken cancelacion) =>
            Results.Ok(await servicio.ListarMovimientosAsync(notaCreditoId, cancelacion)));


        return aplicacion;
    }
}
