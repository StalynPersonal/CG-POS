using System.Security.Claims;
using CgPos.Contratos.Ventas;
using CgPos.Dominio.Seguridad;
using CgPos.Pos.Agente.Pantallas;
using CgPos.Pos.Agente.Seguridad;
using CgPos.Pos.Aplicacion.Seguridad;
using CgPos.Pos.Aplicacion.Ventas;

namespace CgPos.Pos.Agente.Api;

/// <summary>
/// Turno y venta en curso. Los rechazos de negocio responden 422 (409 si ya hay turno abierto) con la
/// respuesta completa, para que la pantalla muestre el mensaje y la venta tal como quedó.
/// </summary>
public static class RutasApiVentas
{
    public static IEndpointRouteBuilder MapearApiVentas(this IEndpointRouteBuilder aplicacion)
    {
        var api = aplicacion.MapGroup("/api").RequireAuthorization();

        api.MapGet("/turnos/actual", (ClaimsPrincipal usuario, IServicioTurnos turnos, CancellationToken cancelacion) =>
            ConSesion(usuario, async sesion => Results.Ok(await turnos.ObtenerEstadoAsync(sesion, cancelacion))));

        api.MapPost("/turnos", (SolicitudAbrirTurno solicitud, ClaimsPrincipal usuario, IServicioTurnos turnos, CancellationToken cancelacion) =>
                ConSesion(usuario, async sesion =>
                {
                    var respuesta = await turnos.AbrirAsync(sesion, solicitud.FondoInicial, cancelacion);
                    return respuesta.Resultado switch
                    {
                        CodigoResultadoTurno.Correcto => Results.Ok(respuesta),
                        CodigoResultadoTurno.YaExisteTurnoAbierto => Results.Json(respuesta, statusCode: StatusCodes.Status409Conflict),
                        CodigoResultadoTurno.SinPermiso => Results.Json(respuesta, statusCode: StatusCodes.Status403Forbidden),
                        _ => Results.UnprocessableEntity(respuesta),
                    };
                }))
            .RequireAuthorization(CatalogoPermisos.AbrirTurno);

        var ventas = api.MapGroup("/ventas").AddEndpointFilter<FiltroPublicarVenta>();

        ventas.MapGet("/actual", (ClaimsPrincipal usuario, IServicioVentas servicio, CancellationToken cancelacion) =>
            ConSesion(usuario, async sesion => Resultado(await servicio.ObtenerActualAsync(sesion, cancelacion))));

        ventas.MapPost("/{ventaId:guid}/lineas", (Guid ventaId, SolicitudAgregarArticulo solicitud, ClaimsPrincipal usuario, IServicioVentas servicio, CancellationToken cancelacion) =>
            ConSesion(usuario, async sesion => Resultado(await servicio.AgregarArticuloAsync(sesion, ventaId, solicitud.Codigo ?? string.Empty, solicitud.Cantidad, solicitud.Serial,
                solicitud.SerialEnDespacho, cancelacion))));

        ventas.MapPost("/{ventaId:guid}/lineas/balanza", (Guid ventaId, SolicitudPesarArticulo solicitud, ClaimsPrincipal usuario, IServicioVentas servicio, CancellationToken cancelacion) =>
            ConSesion(usuario, async sesion => Resultado(await servicio.AgregarDesdeBalanzaAsync(sesion, ventaId, solicitud.Codigo ?? string.Empty, cancelacion))));

        ventas.MapPut("/{ventaId:guid}/lineas/{numeroLinea:int}/cantidad", (Guid ventaId, int numeroLinea, SolicitudCambiarCantidad solicitud, ClaimsPrincipal usuario, IServicioVentas servicio, CancellationToken cancelacion) =>
            ConSesion(usuario, async sesion => Resultado(await servicio.CambiarCantidadAsync(sesion, ventaId, numeroLinea, solicitud.Cantidad, cancelacion))));

        ventas.MapPost("/{ventaId:guid}/lineas/{numeroLinea:int}/eliminar", (Guid ventaId, int numeroLinea, SolicitudConAutorizacion solicitud, ClaimsPrincipal usuario, IServicioVentas servicio, CancellationToken cancelacion) =>
            ConSesion(usuario, async sesion => Resultado(await servicio.EliminarLineaAsync(sesion, ventaId, numeroLinea, solicitud.AutorizacionId, cancelacion))));

        ventas.MapPost("/{ventaId:guid}/eliminar-por-codigo", (Guid ventaId, SolicitudEliminarPorCodigo solicitud, ClaimsPrincipal usuario, IServicioVentas servicio, CancellationToken cancelacion) =>
            ConSesion(usuario, async sesion => Resultado(await servicio.EliminarPorCodigoAsync(sesion, ventaId, solicitud.Codigo ?? string.Empty, solicitud.AutorizacionId, cancelacion))));

        ventas.MapPost("/{ventaId:guid}/limpiar", (Guid ventaId, SolicitudConAutorizacion solicitud, ClaimsPrincipal usuario, IServicioVentas servicio, CancellationToken cancelacion) =>
            ConSesion(usuario, async sesion => Resultado(await servicio.LimpiarAsync(sesion, ventaId, solicitud.AutorizacionId, cancelacion))));

        // Cliente, comprobante y límite (C4)
        ventas.MapPost("/{ventaId:guid}/cliente", (Guid ventaId, SolicitudAsignarCliente solicitud, ClaimsPrincipal usuario, IServicioVentas servicio, CancellationToken cancelacion) =>
            ConSesion(usuario, async sesion => Resultado(await servicio.AsignarClienteAsync(sesion, ventaId, solicitud.Documento ?? string.Empty, solicitud.Nombre, cancelacion))));

        ventas.MapDelete("/{ventaId:guid}/cliente", (Guid ventaId, ClaimsPrincipal usuario, IServicioVentas servicio, CancellationToken cancelacion) =>
            ConSesion(usuario, async sesion => Resultado(await servicio.QuitarClienteAsync(sesion, ventaId, cancelacion))));

        // Pendientes de entrega y envíos (C10): destinos por línea con autorización; al cobrar generan su pendiente.
        ventas.MapPost("/{ventaId:guid}/entregas", (Guid ventaId, SolicitudMarcarEntrega solicitud, ClaimsPrincipal usuario, IServicioVentas servicio, CancellationToken cancelacion) =>
            ConSesion(usuario, async sesion => Resultado(await servicio.MarcarEntregaAsync(sesion, ventaId, solicitud, cancelacion))));

        ventas.MapDelete("/{ventaId:guid}/entregas/{numero:int}", (Guid ventaId, int numero, ClaimsPrincipal usuario, IServicioVentas servicio, CancellationToken cancelacion) =>
            ConSesion(usuario, async sesion => Resultado(await servicio.QuitarEntregaAsync(sesion, ventaId, numero, cancelacion))));

        api.MapGet("/entregas/almacenes", (ClaimsPrincipal usuario, IServicioVentas servicio, CancellationToken cancelacion) =>
            ConSesion(usuario, async sesion => Results.Ok(await servicio.ListarAlmacenesAsync(sesion, cancelacion))));

        // Programa de fidelidad (C10): la cédula del miembro habilita sus ofertas y acumula al cobrar.
        ventas.MapPost("/{ventaId:guid}/fidelidad", (Guid ventaId, SolicitudAsignarFidelidad solicitud, ClaimsPrincipal usuario, IServicioVentas servicio, CancellationToken cancelacion) =>
            ConSesion(usuario, async sesion => Resultado(await servicio.AsignarFidelidadAsync(sesion, ventaId, solicitud.Cedula ?? string.Empty, cancelacion))));

        ventas.MapDelete("/{ventaId:guid}/fidelidad", (Guid ventaId, ClaimsPrincipal usuario, IServicioVentas servicio, CancellationToken cancelacion) =>
            ConSesion(usuario, async sesion => Resultado(await servicio.QuitarFidelidadAsync(sesion, ventaId, cancelacion))));

        ventas.MapPut("/{ventaId:guid}/comprobante", (Guid ventaId, SolicitudCambiarComprobante solicitud, ClaimsPrincipal usuario, IServicioVentas servicio, CancellationToken cancelacion) =>
            ConSesion(usuario, async sesion => Resultado(await servicio.CambiarComprobanteAsync(sesion, ventaId, solicitud.TipoComprobante, solicitud.AutorizacionId, cancelacion))));

        ventas.MapPut("/{ventaId:guid}/limite", (Guid ventaId, SolicitudLimiteCompra solicitud, ClaimsPrincipal usuario, IServicioVentas servicio, CancellationToken cancelacion) =>
            ConSesion(usuario, async sesion => Resultado(await servicio.EstablecerLimiteCompraAsync(sesion, ventaId, solicitud.Limite, cancelacion))));

        // Espera, anulación y suspensión (C4)
        ventas.MapGet("/espera", (ClaimsPrincipal usuario, IServicioVentas servicio, CancellationToken cancelacion) =>
            ConSesion(usuario, async sesion => Results.Ok(await servicio.ListarEnEsperaAsync(sesion, cancelacion))));

        ventas.MapPost("/{ventaId:guid}/espera", (Guid ventaId, ClaimsPrincipal usuario, IServicioVentas servicio, CancellationToken cancelacion) =>
            ConSesion(usuario, async sesion => Resultado(await servicio.PonerEnEsperaAsync(sesion, ventaId, cancelacion))));

        ventas.MapPost("/{ventaId:guid}/retomar", (Guid ventaId, ClaimsPrincipal usuario, IServicioVentas servicio, CancellationToken cancelacion) =>
            ConSesion(usuario, async sesion => Resultado(await servicio.RetomarAsync(sesion, ventaId, cancelacion))));

        ventas.MapPost("/{ventaId:guid}/anular", (Guid ventaId, SolicitudAnularVenta solicitud, ClaimsPrincipal usuario, IServicioVentas servicio, CancellationToken cancelacion) =>
            ConSesion(usuario, async sesion => Resultado(await servicio.AnularAsync(sesion, ventaId, solicitud.Motivo, solicitud.AutorizacionId, cancelacion))));

        // Descuentos y ofertas (C5)
        ventas.MapPost("/{ventaId:guid}/lineas/{numeroLinea:int}/descuento", (Guid ventaId, int numeroLinea, SolicitudDescuentoLinea solicitud, ClaimsPrincipal usuario, IServicioVentas servicio, CancellationToken cancelacion) =>
            ConSesion(usuario, async sesion => Resultado(await servicio.AplicarDescuentoLineaAsync(sesion, ventaId, numeroLinea, solicitud.Tipo, solicitud.Valor, solicitud.Motivo, solicitud.AutorizacionId, cancelacion))));

        ventas.MapDelete("/{ventaId:guid}/lineas/{numeroLinea:int}/descuento", (Guid ventaId, int numeroLinea, ClaimsPrincipal usuario, IServicioVentas servicio, CancellationToken cancelacion) =>
            ConSesion(usuario, async sesion => Resultado(await servicio.QuitarDescuentoLineaAsync(sesion, ventaId, numeroLinea, cancelacion))));

        ventas.MapPost("/{ventaId:guid}/descuento", (Guid ventaId, SolicitudDescuentoFactura solicitud, ClaimsPrincipal usuario, IServicioVentas servicio, CancellationToken cancelacion) =>
            ConSesion(usuario, async sesion => Resultado(await servicio.AplicarDescuentoFacturaAsync(sesion, ventaId, solicitud.Tipo, solicitud.Valor, solicitud.Lineas, solicitud.Motivo, solicitud.AutorizacionId, cancelacion))));

        // Descuento del banco por la tarjeta con la que se va a pagar (RF-98), antes de cobrar y de emitir el e-CF.
        ventas.MapPost("/{ventaId:guid}/descuento-tarjeta", (Guid ventaId, SolicitudDescuentoTarjeta solicitud, ClaimsPrincipal usuario, IServicioVentas servicio, CancellationToken cancelacion) =>
            ConSesion(usuario, async sesion => Resultado(await servicio.AplicarDescuentoTarjetaAsync(sesion, ventaId, solicitud.Bin, cancelacion))));

        ventas.MapDelete("/{ventaId:guid}/descuento", (Guid ventaId, ClaimsPrincipal usuario, IServicioVentas servicio, CancellationToken cancelacion) =>
            ConSesion(usuario, async sesion => Resultado(await servicio.QuitarDescuentoFacturaAsync(sesion, ventaId, cancelacion))));

        ventas.MapPost("/{ventaId:guid}/lineas/{numeroLinea:int}/desactivar-oferta", (Guid ventaId, int numeroLinea, SolicitudConAutorizacion solicitud, ClaimsPrincipal usuario, IServicioVentas servicio, CancellationToken cancelacion) =>
            ConSesion(usuario, async sesion => Resultado(await servicio.DesactivarPromocionAsync(sesion, ventaId, numeroLinea, solicitud.AutorizacionId, cancelacion))));

        api.MapGet("/descuentos/motivos", async (IServicioVentas servicio, CancellationToken cancelacion) =>
            Results.Ok(await servicio.ListarMotivosDescuentoAsync(cancelacion)));

        api.MapGet("/articulos/{articuloId:guid}/promociones", (Guid articuloId, ClaimsPrincipal usuario, IServicioVentas servicio, CancellationToken cancelacion) =>
            ConSesion(usuario, async sesion => Results.Ok(await servicio.ListarPromocionesVigentesAsync(sesion, articuloId, cancelacion))));

        // Cobro y periféricos (C6)
        ventas.MapPost("/{ventaId:guid}/terminal", (Guid ventaId, SolicitudCobroTarjeta solicitud, ClaimsPrincipal usuario, IServicioCobro servicio, CancellationToken cancelacion) =>
            ConSesion(usuario, async sesion => ResultadoTerminal(await servicio.CobrarConTerminalAsync(sesion, ventaId, solicitud.Monto, cancelacion))));

        ventas.MapPost("/{ventaId:guid}/terminal/anular-ultima", (Guid ventaId, ClaimsPrincipal usuario, IServicioCobro servicio, CancellationToken cancelacion) =>
            ConSesion(usuario, async sesion => ResultadoTerminal(await servicio.AnularUltimaOperacionAsync(sesion, ventaId, cancelacion))));

        ventas.MapPost("/{ventaId:guid}/cobrar", (Guid ventaId, SolicitudCobro solicitud, ClaimsPrincipal usuario, IServicioCobro servicio, PublicadorPantallaCliente pantallaCliente,
                CancellationToken cancelacion) =>
            ConSesion(usuario, async sesion =>
            {
                var respuesta = await servicio.CobrarAsync(sesion, ventaId, solicitud.Pagos ?? [], solicitud.AutorizacionId, cancelacion);
                if (!respuesta.Exitosa)
                    return Results.UnprocessableEntity(respuesta);

                // La pantalla del cliente muestra el pago y la devuelta hasta que empiece la siguiente venta.
                await pantallaCliente.PublicarAsync(respuesta.Venta, cancelacion);
                return Results.Ok(respuesta);
            }));

        api.MapPost("/impresion/reimprimir-ultimo", (ClaimsPrincipal usuario, IServicioCobro servicio, CancellationToken cancelacion) =>
            ConSesion(usuario, async sesion => Results.Ok(await servicio.ReimprimirUltimoAsync(sesion, cancelacion))));

        api.MapPost("/caja/gaveta", (SolicitudConAutorizacion solicitud, ClaimsPrincipal usuario, IServicioCobro servicio, CancellationToken cancelacion) =>
            ConSesion(usuario, async sesion => Resultado(await servicio.AbrirGavetaAsync(sesion, solicitud.AutorizacionId, cancelacion))));

        api.MapPost("/caja/suspender", (SolicitudConAutorizacion solicitud, ClaimsPrincipal usuario, IServicioVentas servicio, CancellationToken cancelacion) =>
            ConSesion(usuario, async sesion => Resultado(await servicio.SuspenderAsync(sesion, solicitud.AutorizacionId, cancelacion))));

        api.MapGet("/sincronizacion/estado", async (IEstadoSincronizacion estado, CancellationToken cancelacion) =>
            Results.Ok(await estado.ObtenerAsync(cancelacion)));

        return aplicacion;
    }

    private static async Task<IResult> ConSesion(ClaimsPrincipal usuario, Func<SesionUsuario, Task<IResult>> accion) =>
        EmisorTokens.LeerSesion(usuario) is { } sesion ? await accion(sesion) : Results.Unauthorized();

    private static IResult Resultado(RespuestaVenta respuesta) =>
        respuesta.Exitosa ? Results.Ok(respuesta) : Results.UnprocessableEntity(respuesta);

    private static IResult ResultadoTerminal(RespuestaOperacionTerminal respuesta) =>
        respuesta.Exitosa ? Results.Ok(respuesta) : Results.UnprocessableEntity(respuesta);
}
