using CgPos.Central.Aplicacion.Auditoria;
using CgPos.Dominio.Seguridad;

namespace CgPos.Central.Api.Api;

/// <summary>Auditoría del Central (M02): qué se hizo, quién lo hizo y el antes y el después de cada campo que cambió.</summary>
public static class RutasApiAuditoria
{
    private const int TamanoPaginaPredeterminado = 10;

    public static IEndpointRouteBuilder MapearApiAuditoria(this IEndpointRouteBuilder aplicacion)
    {
        var manager = aplicacion.MapGroup("/api/manager/auditoria").RequireAuthorization(CatalogoPermisosCentral.ConsultarAuditoria);

        manager.MapGet("/", async (DateOnly? desde, DateOnly? hasta, string? accion, string? tipoEntidad, string? usuario, string? buscar,
                bool? soloConCambios, int? pagina, int? tamano, IServicioConsultaAuditoria servicio, CancellationToken cancelacion) =>
            Results.Ok(await servicio.ListarAsync(
                new FiltroAuditoria(desde, hasta, accion, tipoEntidad, usuario, buscar, soloConCambios ?? false, pagina ?? 0, tamano ?? TamanoPaginaPredeterminado),
                cancelacion)));

        // El antes y el después de un movimiento: se piden al abrirlo, no en el listado.
        manager.MapGet("/{registroId:int}", async (int registroId, IServicioConsultaAuditoria servicio, CancellationToken cancelacion) =>
            await servicio.ObtenerAsync(registroId, cancelacion) is { } detalle ? Results.Ok(detalle) : Results.NotFound());

        manager.MapGet("/opciones", async (IServicioConsultaAuditoria servicio, CancellationToken cancelacion) =>
            Results.Ok(await servicio.OpcionesAsync(cancelacion)));

        return aplicacion;
    }
}
