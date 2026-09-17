using System.Security.Claims;
using CgPos.Central.Api.Seguridad;
using CgPos.Central.Aplicacion.ListasBoda;
using CgPos.Central.Aplicacion.Organizacion;
using CgPos.Contratos.Central;
using CgPos.Dominio.ListasBoda;
using CgPos.Dominio.Seguridad;
using static CgPos.Central.Api.Api.RespuestasAdministracion;

namespace CgPos.Central.Api.Api;

/// <summary>Listas de boda y de regalos (RF-73): las cajas las consultan por su número y el Central Manager las administra.</summary>
public static class RutasApiListasBoda
{
    public static IEndpointRouteBuilder MapearApiListasBoda(this IEndpointRouteBuilder aplicacion)
    {
        // La caja consulta la lista al vender: necesita conexión con el Central, no se guarda en la caja.
        var cajas = aplicacion.MapGroup("/api/listas-boda").RequireAuthorization(PoliticasCentral.Dispositivo);

        cajas.MapGet("/{numero}", async (string numero, IServicioListasBoda servicio, CancellationToken cancelacion) =>
            await servicio.BuscarParaCajaAsync(numero, cancelacion) is { } lista ? Results.Ok(lista) : Results.NotFound());

        var manager = aplicacion.MapGroup("/api/manager/listas-boda").RequireAuthorization(CatalogoPermisosCentral.AdministrarListasBoda);

        manager.MapGet("/", async (string? buscar, EstadoListaBoda? estado, IServicioListasBoda servicio, CancellationToken cancelacion) =>
            Results.Ok(await servicio.ListarAsync(buscar, estado, cancelacion)));

        manager.MapGet("/{listaBodaId:int}", async (int listaBodaId, IServicioListasBoda servicio, CancellationToken cancelacion) =>
            await servicio.ObtenerAsync(listaBodaId, cancelacion) is { } lista ? Results.Ok(lista) : Results.NotFound());

        manager.MapPost("/", async (SolicitudListaBoda solicitud, ClaimsPrincipal usuario, IServicioListasBoda servicio, CancellationToken cancelacion) =>
            Responder(await servicio.CrearAsync(solicitud, Actor(usuario), cancelacion)));

        manager.MapPut("/{listaBodaId:int}", async (int listaBodaId, SolicitudListaBoda solicitud, ClaimsPrincipal usuario, IServicioListasBoda servicio,
                CancellationToken cancelacion) =>
            Responder(await servicio.ActualizarAsync(listaBodaId, solicitud, Actor(usuario), cancelacion)));

        manager.MapPost("/{listaBodaId:int}/estado", async (int listaBodaId, bool cerrar, ClaimsPrincipal usuario, IServicioListasBoda servicio,
                CancellationToken cancelacion) =>
            Responder(await servicio.CambiarEstadoAsync(listaBodaId, cerrar, Actor(usuario), cancelacion)));

        // Si lo comprado se descuenta de la lista lo decide el negocio; la pantalla lo muestra para que no haya dudas.
        manager.MapGet("/configuracion", async (IParametrosCentral parametros, CancellationToken cancelacion) =>
            Results.Ok(new DatosConfiguracionListasBoda(
                await parametros.ObtenerBooleanoOpcionalAsync(ClavesParametrosCentral.ListasBodaDescontarCompras, cancelacion))));

        return aplicacion;
    }
}
