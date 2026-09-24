using System.Security.Claims;
using CgPos.Contratos.Ventas;
using CgPos.Dominio.Seguridad;
using CgPos.Pos.Agente.Pantallas;
using CgPos.Pos.Agente.Seguridad;
using CgPos.Pos.Aplicacion.Seguridad;
using CgPos.Pos.Aplicacion.Sincronizacion;
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

        // Vender exige el permiso: al que solo hace devoluciones se le cierra aquí, no solo en la pantalla. El grupo de
        // arriba queda abierto a propósito, porque su turno lo abre y lo cierra igual que cualquier cajero.
        var ventas = api.MapGroup("/ventas").RequireAuthorization(CatalogoPermisos.RegistrarVenta).AddEndpointFilter<FiltroPublicarVenta>();

        ventas.MapGet("/actual", (ClaimsPrincipal usuario, IServicioVentas servicio, CancellationToken cancelacion) =>
            ConSesion(usuario, async sesion => Resultado(await servicio.ObtenerActualAsync(sesion, cancelacion))));

        ventas.MapPost("/{ventaId:int}/lineas", (int ventaId, SolicitudAgregarArticulo solicitud, ClaimsPrincipal usuario, IServicioVentas servicio, CancellationToken cancelacion) =>
            ConSesion(usuario, async sesion => Resultado(await servicio.AgregarArticuloAsync(sesion, ventaId, solicitud.Codigo ?? string.Empty, solicitud.Cantidad, solicitud.Serial,
                solicitud.SerialEnDespacho, cancelacion))));

        ventas.MapPost("/{ventaId:int}/lineas/balanza", (int ventaId, SolicitudPesarArticulo solicitud, ClaimsPrincipal usuario, IServicioVentas servicio, CancellationToken cancelacion) =>
            ConSesion(usuario, async sesion => Resultado(await servicio.AgregarDesdeBalanzaAsync(sesion, ventaId, solicitud.Codigo ?? string.Empty, cancelacion))));

        ventas.MapPut("/{ventaId:int}/lineas/{numeroLinea:int}/cantidad", (int ventaId, int numeroLinea, SolicitudCambiarCantidad solicitud, ClaimsPrincipal usuario, IServicioVentas servicio, CancellationToken cancelacion) =>
            ConSesion(usuario, async sesion => Resultado(await servicio.CambiarCantidadAsync(sesion, ventaId, numeroLinea, solicitud.Cantidad, cancelacion))));

        ventas.MapPost("/{ventaId:int}/lineas/{numeroLinea:int}/eliminar", (int ventaId, int numeroLinea, SolicitudConAutorizacion solicitud, ClaimsPrincipal usuario, IServicioVentas servicio, CancellationToken cancelacion) =>
            ConSesion(usuario, async sesion => Resultado(await servicio.EliminarLineaAsync(sesion, ventaId, numeroLinea, solicitud.AutorizacionId, cancelacion))));

        ventas.MapPost("/{ventaId:int}/eliminar-por-codigo", (int ventaId, SolicitudEliminarPorCodigo solicitud, ClaimsPrincipal usuario, IServicioVentas servicio, CancellationToken cancelacion) =>
            ConSesion(usuario, async sesion => Resultado(await servicio.EliminarPorCodigoAsync(sesion, ventaId, solicitud.Codigo ?? string.Empty, solicitud.AutorizacionId, cancelacion))));

        ventas.MapPost("/{ventaId:int}/limpiar", (int ventaId, SolicitudLimpiarVenta solicitud, ClaimsPrincipal usuario, IServicioVentas servicio, CancellationToken cancelacion) =>
            ConSesion(usuario, async sesion => Resultado(await servicio.LimpiarAsync(sesion, ventaId, solicitud.Motivo, solicitud.AutorizacionId, cancelacion))));

        // Cliente, comprobante y límite (C4)
        ventas.MapPost("/{ventaId:int}/cliente", (int ventaId, SolicitudAsignarCliente solicitud, ClaimsPrincipal usuario, IServicioVentas servicio, CancellationToken cancelacion) =>
            ConSesion(usuario, async sesion => Resultado(await servicio.AsignarClienteAsync(sesion, ventaId, solicitud.Documento ?? string.Empty, solicitud.Nombre, cancelacion))));

        ventas.MapDelete("/{ventaId:int}/cliente", (int ventaId, ClaimsPrincipal usuario, IServicioVentas servicio, CancellationToken cancelacion) =>
            ConSesion(usuario, async sesion => Resultado(await servicio.QuitarClienteAsync(sesion, ventaId, cancelacion))));

        // Pendientes de entrega y envíos (C10): destinos por línea con autorización; al cobrar generan su pendiente.
        ventas.MapPost("/{ventaId:int}/entregas", (int ventaId, SolicitudMarcarEntrega solicitud, ClaimsPrincipal usuario, IServicioVentas servicio, CancellationToken cancelacion) =>
            ConSesion(usuario, async sesion => Resultado(await servicio.MarcarEntregaAsync(sesion, ventaId, solicitud, cancelacion))));

        ventas.MapDelete("/{ventaId:int}/entregas/{numero:int}", (int ventaId, int numero, ClaimsPrincipal usuario, IServicioVentas servicio, CancellationToken cancelacion) =>
            ConSesion(usuario, async sesion => Resultado(await servicio.QuitarEntregaAsync(sesion, ventaId, numero, cancelacion))));

        api.MapGet("/entregas/sucursales", (ClaimsPrincipal usuario, IServicioVentas servicio, CancellationToken cancelacion) =>
            ConSesion(usuario, async sesion => Results.Ok(await servicio.ListarSucursalesRetiroAsync(sesion, cancelacion))));

        // Programa de fidelidad (C10): la cédula del miembro habilita sus ofertas y acumula al cobrar.
        ventas.MapPost("/{ventaId:int}/fidelidad", (int ventaId, SolicitudAsignarFidelidad solicitud, ClaimsPrincipal usuario, IServicioVentas servicio, CancellationToken cancelacion) =>
            ConSesion(usuario, async sesion => Resultado(await servicio.AsignarFidelidadAsync(sesion, ventaId, solicitud.Cedula ?? string.Empty, cancelacion))));

        ventas.MapDelete("/{ventaId:int}/fidelidad", (int ventaId, ClaimsPrincipal usuario, IServicioVentas servicio, CancellationToken cancelacion) =>
            ConSesion(usuario, async sesion => Resultado(await servicio.QuitarFidelidadAsync(sesion, ventaId, cancelacion))));

        ventas.MapPut("/{ventaId:int}/comprobante", (int ventaId, SolicitudCambiarComprobante solicitud, ClaimsPrincipal usuario, IServicioVentas servicio, CancellationToken cancelacion) =>
            ConSesion(usuario, async sesion => Resultado(await servicio.CambiarComprobanteAsync(sesion, ventaId, solicitud.TipoComprobante, solicitud.AutorizacionId, cancelacion))));

        // Cotización hecha en el Central: se trae por su número y se vuelca en la venta con sus precios congelados.
        ventas.MapPut("/{ventaId:int}/cotizacion", (int ventaId, SolicitudFacturarCotizacion solicitud, ClaimsPrincipal usuario, IServicioVentas servicio,
                CancellationToken cancelacion) =>
            ConSesion(usuario, async sesion =>
            {
                var respuesta = await servicio.FacturarCotizacionAsync(sesion, ventaId, solicitud.Numero ?? string.Empty, solicitud.AutorizacionId, cancelacion);
                return respuesta.Exitosa ? Results.Ok(respuesta) : Results.BadRequest(respuesta);
            }));

        // Exención de ITBIS de una entidad del Estado (E45): se guarda el número de su certificación.
        ventas.MapPut("/{ventaId:int}/certificacion-exencion", (int ventaId, SolicitudCertificacionExencion solicitud, ClaimsPrincipal usuario,
                IServicioVentas servicio, CancellationToken cancelacion) =>
            ConSesion(usuario, async sesion => Resultado(await servicio.RegistrarCertificacionExencionAsync(sesion, ventaId, solicitud.Certificacion, cancelacion))));

        // Lista de boda (RF-73): se consulta en el Central, así que esta ruta necesita conexión.
        ventas.MapPut("/{ventaId:int}/lista-boda", (int ventaId, SolicitudListaBodaVenta solicitud, ClaimsPrincipal usuario, IServicioVentas servicio,
                CancellationToken cancelacion) =>
            ConSesion(usuario, async sesion =>
            {
                var respuesta = await servicio.AsignarListaBodaAsync(sesion, ventaId, solicitud.Numero, cancelacion);
                return respuesta.Exitosa ? Results.Ok(respuesta) : Results.BadRequest(respuesta);
            }));

        ventas.MapPut("/{ventaId:int}/limite", (int ventaId, SolicitudLimiteCompra solicitud, ClaimsPrincipal usuario, IServicioVentas servicio, CancellationToken cancelacion) =>
            ConSesion(usuario, async sesion => Resultado(await servicio.EstablecerLimiteCompraAsync(sesion, ventaId, solicitud.Limite, cancelacion))));

        // Espera, anulación y suspensión (C4)
        ventas.MapGet("/espera", (ClaimsPrincipal usuario, IServicioVentas servicio, CancellationToken cancelacion) =>
            ConSesion(usuario, async sesion => Results.Ok(await servicio.ListarEnEsperaAsync(sesion, cancelacion))));

        ventas.MapPost("/{ventaId:int}/espera", (int ventaId, SolicitudPonerEnEspera solicitud, ClaimsPrincipal usuario, IServicioVentas servicio,
                CancellationToken cancelacion) =>
            ConSesion(usuario, async sesion => Resultado(await servicio.PonerEnEsperaAsync(sesion, ventaId, solicitud.Referencia, cancelacion))));

        ventas.MapPost("/{ventaId:int}/retomar", (int ventaId, SolicitudRetomarVenta solicitud, ClaimsPrincipal usuario, IServicioVentas servicio,
                CancellationToken cancelacion) =>
            ConSesion(usuario, async sesion => Resultado(await servicio.RetomarAsync(sesion, ventaId, solicitud.ReferenciaActual, cancelacion))));

        // Descuentos y ofertas (C5)
        ventas.MapPost("/{ventaId:int}/lineas/{numeroLinea:int}/descuento", (int ventaId, int numeroLinea, SolicitudDescuentoLinea solicitud, ClaimsPrincipal usuario, IServicioVentas servicio, CancellationToken cancelacion) =>
            ConSesion(usuario, async sesion => Resultado(await servicio.AplicarDescuentoLineaAsync(sesion, ventaId, numeroLinea, solicitud.Tipo, solicitud.Valor, solicitud.Motivo, solicitud.AutorizacionId, cancelacion))));

        ventas.MapDelete("/{ventaId:int}/lineas/{numeroLinea:int}/descuento", (int ventaId, int numeroLinea, ClaimsPrincipal usuario, IServicioVentas servicio, CancellationToken cancelacion) =>
            ConSesion(usuario, async sesion => Resultado(await servicio.QuitarDescuentoLineaAsync(sesion, ventaId, numeroLinea, cancelacion))));

        ventas.MapPost("/{ventaId:int}/descuento", (int ventaId, SolicitudDescuentoFactura solicitud, ClaimsPrincipal usuario, IServicioVentas servicio, CancellationToken cancelacion) =>
            ConSesion(usuario, async sesion => Resultado(await servicio.AplicarDescuentoFacturaAsync(sesion, ventaId, solicitud.Tipo, solicitud.Valor, solicitud.Lineas, solicitud.Motivo, solicitud.AutorizacionId, cancelacion))));

        // Descuento del banco por la tarjeta con la que se va a pagar (RF-98), antes de cobrar y de emitir el e-CF.
        ventas.MapPost("/{ventaId:int}/descuento-tarjeta", (int ventaId, SolicitudDescuentoTarjeta solicitud, ClaimsPrincipal usuario, IServicioVentas servicio, CancellationToken cancelacion) =>
            ConSesion(usuario, async sesion => Resultado(await servicio.AplicarDescuentoTarjetaAsync(sesion, ventaId, solicitud.Bin, cancelacion))));

        ventas.MapDelete("/{ventaId:int}/descuento", (int ventaId, ClaimsPrincipal usuario, IServicioVentas servicio, CancellationToken cancelacion) =>
            ConSesion(usuario, async sesion => Resultado(await servicio.QuitarDescuentoFacturaAsync(sesion, ventaId, cancelacion))));

        ventas.MapPost("/{ventaId:int}/lineas/{numeroLinea:int}/desactivar-oferta", (int ventaId, int numeroLinea, SolicitudConAutorizacion solicitud, ClaimsPrincipal usuario, IServicioVentas servicio, CancellationToken cancelacion) =>
            ConSesion(usuario, async sesion => Resultado(await servicio.DesactivarPromocionAsync(sesion, ventaId, numeroLinea, solicitud.AutorizacionId, cancelacion))));

        api.MapGet("/descuentos/motivos", async (IServicioVentas servicio, CancellationToken cancelacion) =>
            Results.Ok(await servicio.ListarMotivosDescuentoAsync(cancelacion)));

        api.MapGet("/articulos/{articuloId:int}/promociones", (int articuloId, ClaimsPrincipal usuario, IServicioVentas servicio, CancellationToken cancelacion) =>
            ConSesion(usuario, async sesion => Results.Ok(await servicio.ListarPromocionesVigentesAsync(sesion, articuloId, cancelacion))));

        // Cobro y periféricos (C6)
        ventas.MapPost("/{ventaId:int}/terminal", (int ventaId, SolicitudCobroTarjeta solicitud, ClaimsPrincipal usuario, IServicioCobro servicio, CancellationToken cancelacion) =>
            ConSesion(usuario, async sesion =>
                ResultadoTerminal(await servicio.CobrarConTerminalAsync(sesion, ventaId, solicitud.Monto, solicitud.PagaSaldo, cancelacion))));

        ventas.MapPost("/{ventaId:int}/terminal/anular-ultima", (int ventaId, ClaimsPrincipal usuario, IServicioCobro servicio, CancellationToken cancelacion) =>
            ConSesion(usuario, async sesion => ResultadoTerminal(await servicio.AnularUltimaOperacionAsync(sesion, ventaId, cancelacion))));

        ventas.MapPost("/{ventaId:int}/cobrar", (int ventaId, SolicitudCobro solicitud, ClaimsPrincipal usuario, IServicioCobro servicio, PublicadorPantallaCliente pantallaCliente,
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

        // Sincronizar ahora, sin esperar el ciclo: para cuando en el Central acaban de cambiar algo y lo quieren ver ya.
        api.MapPost("/sincronizacion", async (ISincronizacionAPedido sincronizacion, CancellationToken cancelacion) =>
            Results.Ok(await sincronizacion.EjecutarAsync(cancelacion)));

        return aplicacion;
    }

    private static async Task<IResult> ConSesion(ClaimsPrincipal usuario, Func<SesionUsuario, Task<IResult>> accion) =>
        EmisorTokens.LeerSesion(usuario) is { } sesion ? await accion(sesion) : Results.Unauthorized();

    private static IResult Resultado(RespuestaVenta respuesta) =>
        respuesta.Exitosa ? Results.Ok(respuesta) : Results.UnprocessableEntity(respuesta);

    private static IResult ResultadoTerminal(RespuestaOperacionTerminal respuesta) =>
        respuesta.Exitosa ? Results.Ok(respuesta) : Results.UnprocessableEntity(respuesta);
}
