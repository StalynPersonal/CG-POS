using CgPos.Contratos.Ventas;
using CgPos.Pos.Aplicacion.Sincronizacion;

namespace CgPos.Pos.Agente.Api;

/// <summary>
/// Configuración inicial de la caja: qué caja es, su dirección, dónde está el Central y su credencial.
/// </summary>
/// <remarks>
/// No pide sesión, y no puede pedirla: los usuarios bajan del Central, y sin esta configuración la caja todavía no tiene
/// ninguno. Lo que protege esto es que el Agente solo escucha en este equipo y que sin la credencial que emite el Central no
/// se pasa de aquí.
/// </remarks>
public static class RutasApiConfiguracion
{
    public static IEndpointRouteBuilder MapearApiConfiguracion(this IEndpointRouteBuilder aplicacion)
    {
        var configuracion = aplicacion.MapGroup("/api/configuracion");

        configuracion.MapGet("", async (IConfiguracionCaja servicio, CancellationToken cancelacion) =>
        {
            var datos = await servicio.ObtenerAsync(cancelacion);
            return Results.Ok(new DatosConfiguracionPantalla(
                datos is not null,
                datos?.Sirve ?? false,
                datos?.SucursalCodigo,
                datos?.CajaCodigo,
                datos?.DireccionIp,
                datos?.UrlCentral,
                datos?.Problema));
        });

        configuracion.MapPost("", async (SolicitudConfigurarCajaPantalla solicitud, IConfiguracionCaja servicio, CancellationToken cancelacion) =>
        {
            if (solicitud is null)
                return Results.BadRequest(new RespuestaConfiguracion(false, "No llegaron los datos de la caja."));

            var problema = await servicio.GuardarAsync(new SolicitudConfigurarCaja(
                solicitud.SucursalCodigo ?? string.Empty,
                solicitud.CajaCodigo ?? string.Empty,
                solicitud.DireccionIp ?? string.Empty,
                solicitud.UrlCentral ?? string.Empty,
                solicitud.Secreto ?? string.Empty,
                solicitud.Usuario ?? string.Empty,
                solicitud.Contrasena ?? string.Empty), cancelacion);

            return problema is null
                ? Results.Ok(new RespuestaConfiguracion(true, "Caja configurada. En el próximo ciclo se comunicará con el Central."))
                : Results.BadRequest(new RespuestaConfiguracion(false, problema));
        });

        // Volver a bajarlo todo: la caja olvida hasta dónde llegó y el Central se lo manda desde cero.
        configuracion.MapPost("/reaprovisionar", async (SolicitudReaprovisionarCaja solicitud, IConfiguracionCaja servicio, CancellationToken cancelacion) =>
        {
            if (solicitud is null)
                return Results.BadRequest(new RespuestaConfiguracion(false, "No llegó quién autoriza."));

            var problema = await servicio.ReaprovisionarAsync(solicitud.Usuario ?? string.Empty, solicitud.Contrasena ?? string.Empty, cancelacion);
            return problema is null
                ? Results.Ok(new RespuestaConfiguracion(true,
                    "La caja va a bajar otra vez todos los datos del Central. Empieza en menos de un minuto y mientras dura no se puede vender."))
                : Results.BadRequest(new RespuestaConfiguracion(false, problema));
        });

        return aplicacion;
    }
}
