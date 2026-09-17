using CgPos.Central.Aplicacion.Catalogo;
using CgPos.Contratos.Central;

namespace CgPos.Central.Api.Api;

/// <summary>
/// Chequeador de precios (RF-95): la consulta que hace el cliente desde la pantalla de la tienda. No pide sesión, porque el
/// equipo está a la vista del público, y solo entrega descripción, precio y ofertas. Se enciende con
/// <c>Central.Chequeador.Habilitado</c>; apagado, no responde nada.
/// </summary>
public static class RutasApiChequeador
{
    public static IEndpointRouteBuilder MapearApiChequeador(this IEndpointRouteBuilder aplicacion)
    {
        var chequeador = aplicacion.MapGroup("/api/chequeador").AllowAnonymous();

        chequeador.MapGet("/configuracion", async (IServicioChequeadorPrecios servicio, CancellationToken cancelacion) =>
        {
            var habilitado = await servicio.HabilitadoAsync(cancelacion);
            return Results.Ok(new DatosConfiguracionChequeador(habilitado, habilitado ? await servicio.ListarSucursalesAsync(cancelacion) : []));
        });

        chequeador.MapGet("/articulos/{codigo}", async (string codigo, int? sucursalId, IServicioChequeadorPrecios servicio, CancellationToken cancelacion) =>
            await servicio.ConsultarAsync(codigo, sucursalId, cancelacion) is { } articulo ? Results.Ok(articulo) : Results.NotFound());

        return aplicacion;
    }
}
