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
    private const int TamanoPaginaPredeterminado = 25;

    public static IEndpointRouteBuilder MapearApiNotasCredito(this IEndpointRouteBuilder aplicacion)
    {
        var cajas = aplicacion.MapGroup("/api/notas-credito").RequireAuthorization(PoliticasCentral.Dispositivo);

        // La caja consulta el saldo de una nota emitida en cualquier sucursal (RF-43).
        cajas.MapGet("/{codigo}", async (string codigo, IServicioNotasCreditoCentral servicio, CancellationToken cancelacion) =>
            await servicio.BuscarAsync(codigo, cancelacion) is { } nota ? Results.Ok(nota) : Results.NotFound());

        // Retiene el saldo mientras la caja cobra; si no cobra, lo libera o la reserva vence sola.
        cajas.MapPost("/{notaCreditoId:guid}/reservas", async (Guid notaCreditoId, SolicitudReservaNotaCredito solicitud, ClaimsPrincipal usuario,
                IServicioNotasCreditoCentral servicio, CancellationToken cancelacion) =>
            EmisorTokensCentral.LeerDispositivo(usuario) is { } caja
                ? Results.Ok(await servicio.ReservarAsync(notaCreditoId, caja.CajaId, solicitud.Monto, cancelacion))
                : Results.Unauthorized());

        cajas.MapDelete("/reservas/{reservaId:guid}", async (Guid reservaId, ClaimsPrincipal usuario, IServicioNotasCreditoCentral servicio,
                CancellationToken cancelacion) =>
            EmisorTokensCentral.LeerDispositivo(usuario) is not { } caja ? Results.Unauthorized()
                : await servicio.LiberarReservaAsync(reservaId, caja.CajaId, cancelacion) ? Results.NoContent()
                : Results.NotFound());

        var manager = aplicacion.MapGroup("/api/manager/notas-credito").RequireAuthorization(CatalogoPermisosCentral.AdministrarNotasCredito);

        manager.MapGet("/", async (string? buscar, EstadoNotaCreditoCentral? estado, bool? soloSobregiradas, int? pagina, int? tamano,
                IServicioNotasCreditoCentral servicio, CancellationToken cancelacion) =>
            Results.Ok(await servicio.ListarAsync(buscar, estado, soloSobregiradas ?? false, pagina ?? 0, tamano ?? TamanoPaginaPredeterminado, cancelacion)));

        manager.MapGet("/{notaCreditoId:guid}/movimientos", async (Guid notaCreditoId, IServicioNotasCreditoCentral servicio, CancellationToken cancelacion) =>
            Results.Ok(await servicio.ListarMovimientosAsync(notaCreditoId, cancelacion)));

        // Habilitar una nota vencida (RF-40): exige el motivo y queda en la auditoría.
        manager.MapPost("/{notaCreditoId:guid}/prorrogar", async (Guid notaCreditoId, SolicitudProrrogaNotaCredito solicitud, ClaimsPrincipal usuario,
                IServicioNotasCreditoCentral servicio, CancellationToken cancelacion) =>
            Responder(await servicio.ProrrogarAsync(notaCreditoId, solicitud.VenceEn, solicitud.Motivo, Actor(usuario), cancelacion)));

        return aplicacion;
    }
}
