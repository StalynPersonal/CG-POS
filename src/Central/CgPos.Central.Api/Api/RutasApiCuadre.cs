using System.Security.Claims;
using CgPos.Central.Api.Seguridad;
using CgPos.Central.Aplicacion.Organizacion;
using CgPos.Central.Aplicacion.Reportes;
using CgPos.Contratos.Central;

namespace CgPos.Central.Api.Api;

/// <summary>
/// Módulo de cuadre: el supervisor de la tienda cuenta el dinero que le entregaron las cajeras y lo declara desde una sola
/// computadora. Entra con su usuario de caja; contabilidad entra con el suyo del Central y ve todas las sucursales.
/// </summary>
public static class RutasApiCuadre
{
    public static IEndpointRouteBuilder MapearApiCuadre(this IEndpointRouteBuilder aplicacion)
    {
        var cuadre = aplicacion.MapGroup("/api/cuadre").SinCache();

        cuadre.MapPost("/ingreso", async (SolicitudIngresoCuadre solicitud, IServicioSesionesCuadre servicio, EmisorTokensCentral emisor,
                IParametrosCentral parametros, CancellationToken cancelacion) =>
            {
                var resultado = await servicio.IngresarAsync(solicitud.Usuario ?? string.Empty, solicitud.Clave ?? string.Empty, cancelacion);
                if (resultado.Sesion is not { } sesion)
                    return Results.Json(new RespuestaSesionCuadre(false, resultado.Mensaje), statusCode: StatusCodes.Status401Unauthorized);

                // Sesión corta: la computadora del cuadre está en la tienda y la usa más de uno.
                var duracion = TimeSpan.FromMinutes(await parametros.ObtenerEnteroPositivoAsync(ClavesParametrosCentral.MinutosTokenAcceso, cancelacion));
                var (token, expira) = emisor.EmitirCuadre(sesion, duracion);
                return Results.Ok(new RespuestaSesionCuadre(true, TokenAcceso: token, ExpiraEn: expira, Sesion: sesion));
            });

        // Las denominaciones con las que cuenta el efectivo.
        cuadre.MapGet("/denominaciones", async (IServicioCierresCaja servicio, CancellationToken cancelacion) =>
                Results.Ok(await servicio.ListarDenominacionesAsync(cancelacion)))
            .RequireAuthorization(PoliticasCentral.CuadreConsultar);

        // Lo que está esperando al supervisor en su sucursal.
        cuadre.MapGet("/pendientes", async (int sucursalId, ClaimsPrincipal usuario, IServicioCierresCaja servicio, CancellationToken cancelacion) =>
                SinAcceso(usuario, sucursalId)
                    ? Results.Forbid()
                    : Results.Ok(await servicio.ListarPendientesDeCuadreAsync(sucursalId, cancelacion)))
            .RequireAuthorization(PoliticasCentral.CuadreConsultar);

        // Los cierres del día de una sucursal, cuadrados o no, para ver quién falta.
        cuadre.MapGet("/cierres", async (int sucursalId, DateOnly desde, DateOnly hasta, ClaimsPrincipal usuario, IServicioCierresCaja servicio,
                CancellationToken cancelacion) =>
                SinAcceso(usuario, sucursalId)
                    ? Results.Forbid()
                    : Results.Ok(await servicio.ListarAsync(sucursalId, null, desde, hasta, cancelacion)))
            .RequireAuthorization(PoliticasCentral.CuadreConsultar);

        // El día de la sucursal sumado por forma de pago: lo que mira la gerencia antes del depósito.
        cuadre.MapGet("/resumen", async (int sucursalId, DateOnly dia, ClaimsPrincipal usuario, IServicioCierresCaja servicio, CancellationToken cancelacion) =>
                SinAcceso(usuario, sucursalId)
                    ? Results.Forbid()
                    : Results.Ok(await servicio.ResumenDelDiaAsync(sucursalId, dia, cancelacion)))
            .RequireAuthorization(PoliticasCentral.CuadreConsultar);

        // Faltantes y sobrantes de cada cajera en el período.
        cuadre.MapGet("/diferencias", async (int sucursalId, DateOnly desde, DateOnly hasta, ClaimsPrincipal usuario, IServicioCierresCaja servicio,
                CancellationToken cancelacion) =>
                SinAcceso(usuario, sucursalId)
                    ? Results.Forbid()
                    : Results.Ok(await servicio.DiferenciasPorCajeroAsync(sucursalId, desde, hasta, cancelacion)))
            .RequireAuthorization(PoliticasCentral.CuadreConsultar);

        // Retiros, reembolsos y relevos de los turnos, con su motivo y quién autorizó.
        cuadre.MapGet("/movimientos", async (int sucursalId, DateOnly desde, DateOnly hasta, ClaimsPrincipal usuario, IServicioCierresCaja servicio,
                CancellationToken cancelacion) =>
                SinAcceso(usuario, sucursalId)
                    ? Results.Forbid()
                    : Results.Ok(await servicio.MovimientosAsync(sucursalId, desde, hasta, cancelacion)))
            .RequireAuthorization(PoliticasCentral.CuadreConsultar);

        // Cuánto estuvieron paradas las cajas y por qué: el reporte de tiempos de caja parada.
        cuadre.MapGet("/tiempos-parada", async (int sucursalId, DateOnly desde, DateOnly hasta, ClaimsPrincipal usuario, IServicioCierresCaja servicio,
                CancellationToken cancelacion) =>
                SinAcceso(usuario, sucursalId)
                    ? Results.Forbid()
                    : Results.Ok(await servicio.TiemposParadaAsync(sucursalId, desde, hasta, cancelacion)))
            .RequireAuthorization(PoliticasCentral.CuadreConsultar);

        // Reimpresión del cuadre: el papel que se archiva con el depósito.
        cuadre.MapGet("/{cierreId:int}/pdf", async (int cierreId, int sucursalId, ClaimsPrincipal usuario, IServicioCierresCaja servicio,
                CancellationToken cancelacion) =>
            {
                if (SinAcceso(usuario, sucursalId))
                    return Results.Forbid();

                // La sucursal va en la consulta y el servicio comprueba que el cierre sea de ella: así el supervisor no
                // imprime el cuadre de otra tienda cambiando el número en la dirección.
                return await servicio.CuadreEnPdfAsync(cierreId, sucursalId, cancelacion) is { } archivo
                    ? Results.File(archivo.Contenido, archivo.TipoContenido, archivo.Nombre)
                    : Results.NotFound();
            })
            .RequireAuthorization(PoliticasCentral.CuadreConsultar);

        cuadre.MapPost("/{cierreId:int}", async (int cierreId, SolicitudCuadreCierre solicitud, ClaimsPrincipal usuario, IServicioCierresCaja servicio,
                CancellationToken cancelacion) =>
                RespuestasAdministracion.Responder(
                    await servicio.CuadrarAsync(cierreId, solicitud, EmisorTokensCentral.ActorDelCuadre(usuario), cancelacion)))
            .RequireAuthorization(PoliticasCentral.CuadreDeclarar);

        cuadre.MapPost("/{cierreId:int}/correccion", async (int cierreId, SolicitudAjusteCierre solicitud, ClaimsPrincipal usuario,
                IServicioCierresCaja servicio, CancellationToken cancelacion) =>
                RespuestasAdministracion.Responder(
                    await servicio.AjustarAsync(cierreId, solicitud, EmisorTokensCentral.ActorDelCuadre(usuario), cancelacion)))
            .RequireAuthorization(PoliticasCentral.CuadreCorregir);

        return aplicacion;
    }

    /// <summary>El supervisor solo ve sus sucursales; el usuario del Central, que no trae ninguna, las ve todas.</summary>
    private static bool SinAcceso(ClaimsPrincipal usuario, int sucursalId)
    {
        var suyas = EmisorTokensCentral.SucursalesDelCuadre(usuario);
        return suyas.Count > 0 && !suyas.Contains(sucursalId);
    }
}
