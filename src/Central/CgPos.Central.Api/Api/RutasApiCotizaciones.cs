using System.Security.Claims;
using CgPos.Central.Aplicacion.Cotizaciones;
using CgPos.Central.Api.Seguridad;
using CgPos.Contratos.Central;
using CgPos.Dominio.Cotizaciones;
using CgPos.Dominio.Seguridad;
using static CgPos.Central.Api.Api.RespuestasAdministracion;

namespace CgPos.Central.Api.Api;

/// <summary>
/// Cotizaciones: el presupuesto que se le arma a un cliente desde el Central. El Manager las crea y las consulta; las cajas
/// solo las buscan por su número para convertirlas en factura.
/// </summary>
public static class RutasApiCotizaciones
{
    public static IEndpointRouteBuilder MapearApiCotizaciones(this IEndpointRouteBuilder aplicacion)
    {
        var manager = aplicacion.MapGroup("/api/manager/cotizaciones").RequireAuthorization(CatalogoPermisosCentral.AdministrarCotizaciones);

        manager.MapGet("/", async (string? buscar, EstadoCotizacion? estado, IServicioCotizaciones servicio, CancellationToken cancelacion) =>
            Results.Ok(await servicio.ListarAsync(buscar, estado, cancelacion)));

        manager.MapGet("/{cotizacionId:int}", async (int cotizacionId, IServicioCotizaciones servicio, CancellationToken cancelacion) =>
            await servicio.ObtenerAsync(cotizacionId, cancelacion) is { } cotizacion ? Results.Ok(cotizacion) : Results.NotFound());

        manager.MapPost("/", async (SolicitudCotizacion solicitud, ClaimsPrincipal usuario, IServicioCotizaciones servicio, CancellationToken cancelacion) =>
            Responder(await servicio.CrearAsync(solicitud, Actor(usuario), cancelacion)));

        manager.MapPut("/{cotizacionId:int}", async (int cotizacionId, SolicitudCotizacion solicitud, ClaimsPrincipal usuario, IServicioCotizaciones servicio,
                CancellationToken cancelacion) =>
            Responder(await servicio.ActualizarAsync(cotizacionId, solicitud, Actor(usuario), cancelacion)));

        manager.MapPost("/{cotizacionId:int}/anular", async (int cotizacionId, SolicitudAnularCotizacion solicitud, ClaimsPrincipal usuario,
                IServicioCotizaciones servicio, CancellationToken cancelacion) =>
            Responder(await servicio.AnularAsync(cotizacionId, solicitud.Motivo ?? string.Empty, Actor(usuario), cancelacion)));

        // La caja la consulta al facturar: necesita conexión con el Central, no se guarda en la caja.
        var cajas = aplicacion.MapGroup("/api/cotizaciones").RequireAuthorization(PoliticasCentral.Dispositivo);

        cajas.MapGet("/{numero}", async (string numero, IServicioCotizaciones servicio, CancellationToken cancelacion) =>
            await servicio.BuscarParaCajaAsync(numero, cancelacion) is { } cotizacion ? Results.Ok(cotizacion) : Results.NotFound());

        return aplicacion;
    }
}
