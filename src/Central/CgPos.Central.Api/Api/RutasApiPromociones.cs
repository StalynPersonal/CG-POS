using System.Security.Claims;
using CgPos.Central.Aplicacion.Maestros;
using CgPos.Central.Aplicacion.Organizacion;
using CgPos.Contratos.Catalogo;
using CgPos.Contratos.Central;
using CgPos.Dominio.Seguridad;
using static CgPos.Central.Api.Api.RespuestasAdministracion;

namespace CgPos.Central.Api.Api;

public static class RutasApiPromociones
{
    private const int TamanoPaginaPredeterminado = 10;

    public static IEndpointRouteBuilder MapearApiPromociones(this IEndpointRouteBuilder aplicacion)
    {
        var grupo = aplicacion.MapGroup("/api/promociones").RequireAuthorization(CatalogoPermisosCentral.AdministrarPromociones);

        grupo.MapGet("/", async (IServicioPromocionesCentral servicio, CancellationToken cancelacion) => Results.Ok(await servicio.ListarAsync(cancelacion)));

        grupo.MapPost("/", async (PromocionCarga promocion, ClaimsPrincipal usuario, IServicioMaestrosCentral maestros, CancellationToken cancelacion) =>
            Responder(await maestros.GuardarAsync(promocion, nuevo: true, Actor(usuario), cancelacion)));
        // No hay PUT: una promoción publicada no se cambia. Lo único que admite es apagarse o volver a encenderse.
        grupo.MapPost("/{codigo}/estado", async (string codigo, SolicitudEstadoPromocion solicitud, ClaimsPrincipal usuario,
                IServicioPromocionesCentral servicio, CancellationToken cancelacion) =>
            Responder(await servicio.CambiarEstadoAsync(codigo, solicitud.Activa, Actor(usuario), cancelacion)));

        grupo.MapPost("/importar", async (SolicitudImportacionPromociones solicitud, ClaimsPrincipal usuario, IServicioPromocionesCentral servicio,
                CancellationToken cancelacion) =>
            Results.Ok(await servicio.ImportarAsync(solicitud, Actor(usuario), cancelacion)));

        grupo.MapPost("/simular", async (SolicitudSimulacionPromociones solicitud, IServicioPromocionesCentral servicio, CancellationToken cancelacion) =>
            await servicio.SimularAsync(solicitud, cancelacion) is { } resultado ? Results.Ok(resultado) : Results.NotFound());

        // Referencias para armar el alcance de una promoción sin exigir los permisos de maestros u organización.
        grupo.MapGet("/articulos", async (string? buscar, int? pagina, int? tamano, IServicioMaestrosCentral maestros, CancellationToken cancelacion) =>
            Results.Ok(await maestros.BuscarAsync<ArticuloCarga>(buscar, pagina ?? 0, tamano ?? TamanoPaginaPredeterminado, cancelacion: cancelacion)));
        grupo.MapPost("/articulos/por-codigo", async (string[] codigos, IServicioPromocionesCentral servicio, CancellationToken cancelacion) =>
            Results.Ok(await servicio.ArticulosPorCodigoAsync(codigos, cancelacion)));
        grupo.MapGet("/departamentos", async (IServicioMaestrosCentral maestros, CancellationToken cancelacion) =>
            Results.Ok(await maestros.ListarAsync<DepartamentoCarga>(cancelacion)));
        grupo.MapGet("/categorias", async (IServicioMaestrosCentral maestros, CancellationToken cancelacion) =>
            Results.Ok(await maestros.ListarAsync<CategoriaCarga>(cancelacion)));
        grupo.MapGet("/marcas", async (IServicioMaestrosCentral maestros, CancellationToken cancelacion) =>
            Results.Ok(await maestros.ListarAsync<MarcaCarga>(cancelacion)));
        grupo.MapGet("/sucursales", async (IServicioOrganizacion organizacion, CancellationToken cancelacion) =>
            Results.Ok(await organizacion.ListarSucursalesAsync(cancelacion)));

        return aplicacion;
    }
}
