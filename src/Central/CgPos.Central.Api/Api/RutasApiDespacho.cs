using System.Security.Claims;
using CgPos.Central.Aplicacion.Entregas;
using CgPos.Contratos.Ventas;
using CgPos.Dominio.Entregas;
using CgPos.Dominio.Seguridad;
using static CgPos.Central.Api.Api.RespuestasAdministracion;

namespace CgPos.Central.Api.Api;

/// <summary>
/// Pendientes de entrega y envíos de todas las sucursales. El despacho se opera desde el Central: no emite comprobante fiscal
/// ni toca la gaveta, y quien atiende a un cliente que llama o que llega a otra tienda tiene que verlos todos.
/// </summary>
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

        // Avanza la preparación: en preparación, preparado y, para envíos, despachado con el transportista (RF-252).
        manager.MapPost("/pendientes/{pendienteId:int}/estado", async (int pendienteId, SolicitudEstadoPendiente solicitud, ClaimsPrincipal usuario,
                IServicioDespachoCentral servicio, CancellationToken cancelacion) =>
            Responder(await servicio.CambiarEstadoAsync(pendienteId, solicitud, Actor(usuario), cancelacion)));

        // Entrega total o parcial, con quien recibe y el serial de los serializados (RF-253, RF-254).
        manager.MapPost("/pendientes/{pendienteId:int}/entregas", async (int pendienteId, SolicitudEntregaPendiente solicitud, ClaimsPrincipal usuario,
                IServicioDespachoCentral servicio, CancellationToken cancelacion) =>
            Responder(await servicio.EntregarAsync(pendienteId, solicitud, Actor(usuario), cancelacion)));

        // La constancia que firma quien recibe, en carta: el almacén imprime en una impresora normal (RF-254).
        manager.MapGet("/pendientes/{pendienteId:int}/entregas/{numero:int}/pdf", async (int pendienteId, int numero, IServicioDespachoCentral servicio,
                CancellationToken cancelacion) =>
            await servicio.ConstanciaAsync(pendienteId, numero, cancelacion) is { } archivo
                ? Results.File(archivo, "application/pdf", $"constancia-{pendienteId}-{numero}.pdf")
                : Results.NotFound());

        // Anular libera mercancía ya facturada: lleva su propio permiso, como en la caja (RF-255).
        aplicacion.MapPost("/api/manager/despacho/pendientes/{pendienteId:int}/anular", async (int pendienteId, SolicitudAnularPendiente solicitud,
                ClaimsPrincipal usuario, IServicioDespachoCentral servicio, CancellationToken cancelacion) =>
            Responder(await servicio.AnularAsync(pendienteId, solicitud, Actor(usuario), cancelacion)))
            .RequireAuthorization(CatalogoPermisosCentral.AnularPendientes);

        return aplicacion;
    }
}
