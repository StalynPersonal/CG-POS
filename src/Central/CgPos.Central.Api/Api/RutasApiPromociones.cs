using System.Security.Claims;
using CgPos.Central.Aplicacion.Maestros;
using CgPos.Central.Aplicacion.Organizacion;
using CgPos.Central.Aplicacion.Seguridad;
using CgPos.Contratos.Catalogo;
using CgPos.Contratos.Central;
using CgPos.Dominio.Seguridad;
using CgPos.Dominio.Sincronizacion;
using static CgPos.Central.Api.Api.RespuestasAdministracion;

namespace CgPos.Central.Api.Api;

public static class RutasApiPromociones
{
    private const int TamanoPaginaPredeterminado = 25;

    public static IEndpointRouteBuilder MapearApiPromociones(this IEndpointRouteBuilder aplicacion)
    {
        var grupo = aplicacion.MapGroup("/api/promociones").RequireAuthorization(CatalogoPermisosCentral.AdministrarPromociones);

        grupo.MapGet("/", async (IServicioPromocionesCentral servicio, CancellationToken cancelacion) => Results.Ok(await servicio.ListarAsync(cancelacion)));

        grupo.MapPut("/{promocionId:guid}", async (Guid promocionId, PromocionCarga promocion, ClaimsPrincipal usuario, IServicioMaestrosCentral maestros,
                CancellationToken cancelacion) =>
            promocion.Id != promocionId
                ? Responder(ResultadoAdministracion.Error("El Id de la promoción no coincide con el de la ruta."))
                : Responder(await maestros.PublicarAsync(new PaqueteMaestros(Promociones: [promocion]), promocionId, Actor(usuario), cancelacion)));

        grupo.MapPost("/importar", async (SolicitudImportacionPromociones solicitud, ClaimsPrincipal usuario, IServicioPromocionesCentral servicio,
                CancellationToken cancelacion) =>
            Results.Ok(await servicio.ImportarAsync(solicitud, Actor(usuario), cancelacion)));

        grupo.MapPost("/simular", async (SolicitudSimulacionPromociones solicitud, IServicioPromocionesCentral servicio, CancellationToken cancelacion) =>
            await servicio.SimularAsync(solicitud, cancelacion) is { } resultado ? Results.Ok(resultado) : Results.NotFound());

        // Referencias para armar el alcance de una promoción sin exigir los permisos de maestros u organización.
        grupo.MapGet("/articulos", async (string? buscar, int? pagina, int? tamano, IServicioMaestrosCentral maestros, CancellationToken cancelacion) =>
            Results.Ok(await maestros.BuscarAsync<ArticuloCarga>(TipoMaestro.Articulo, buscar, pagina ?? 0, tamano ?? TamanoPaginaPredeterminado, cancelacion)));
        grupo.MapPost("/articulos/por-id", async (Guid[] ids, IServicioPromocionesCentral servicio, CancellationToken cancelacion) =>
            Results.Ok(await servicio.ArticulosPorIdAsync(ids, cancelacion)));
        grupo.MapGet("/departamentos", async (IServicioMaestrosCentral maestros, CancellationToken cancelacion) =>
            Results.Ok(await maestros.ListarAsync<DepartamentoCarga>(TipoMaestro.Departamento, cancelacion)));
        grupo.MapGet("/categorias", async (IServicioMaestrosCentral maestros, CancellationToken cancelacion) =>
            Results.Ok(await maestros.ListarAsync<CategoriaCarga>(TipoMaestro.Categoria, cancelacion)));
        grupo.MapGet("/marcas", async (IServicioMaestrosCentral maestros, CancellationToken cancelacion) =>
            Results.Ok(await maestros.ListarAsync<MarcaCarga>(TipoMaestro.Marca, cancelacion)));
        grupo.MapGet("/sucursales", async (IServicioOrganizacion organizacion, CancellationToken cancelacion) =>
            Results.Ok(await organizacion.ListarSucursalesAsync(cancelacion)));

        return aplicacion;
    }
}
