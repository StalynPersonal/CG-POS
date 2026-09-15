using CgPos.Contratos.Catalogo;
using CgPos.Contratos.Ventas;
using CgPos.Dominio.Fiscal;
using CgPos.Dominio.Ventas;
using CgPos.Pos.Aplicacion.Seguridad;

namespace CgPos.Pos.Aplicacion.Ventas;

public interface IServicioTurnos
{
    Task<DatosEstadoTurno> ObtenerEstadoAsync(SesionUsuario sesion, CancellationToken cancelacion = default);

    /// <summary>Abre el turno de la caja con el fondo indicado o el sugerido (RF-4, RF-259). Solo uno abierto por caja.</summary>
    Task<RespuestaTurno> AbrirAsync(SesionUsuario sesion, decimal? fondoInicial, CancellationToken cancelacion = default);
}

/// <summary>
/// Operaciones sobre la venta en curso. Cada operación se guarda al instante, así la venta se recupera
/// tras un cierre inesperado (RF-195). Las que requieren permiso aceptan una autorización de supervisor.
/// </summary>
public interface IServicioVentas
{
    /// <summary>Devuelve la venta en curso del usuario en su turno; si no hay, inicia una nueva.</summary>
    Task<RespuestaVenta> ObtenerActualAsync(SesionUsuario sesion, CancellationToken cancelacion = default);

    /// <param name="serialEnDespacho">Serializado sin serial que se marcará para entrega o envío (RN-16).</param>
    Task<RespuestaVenta> AgregarArticuloAsync(SesionUsuario sesion, Guid ventaId, string codigo, decimal? cantidad, string? serial = null, bool serialEnDespacho = false,
        CancellationToken cancelacion = default);

    /// <summary>
    /// Marca líneas, completas o en parte, para retiro en almacén o envío a dirección (RF-246 a RF-250), con permiso o clave de supervisor (RF-53).
    /// Al cobrar cada destino genera su pendiente de entrega.
    /// </summary>
    Task<RespuestaVenta> MarcarEntregaAsync(SesionUsuario sesion, Guid ventaId, SolicitudMarcarEntrega solicitud, CancellationToken cancelacion = default);

    Task<RespuestaVenta> QuitarEntregaAsync(SesionUsuario sesion, Guid ventaId, int numeroDestino, CancellationToken cancelacion = default);

    /// <summary>Almacenes y sucursales para retiro; primero los de la sucursal de la caja (RF-138, RF-140).</summary>
    Task<IReadOnlyList<DatosAlmacen>> ListarAlmacenesAsync(SesionUsuario sesion, CancellationToken cancelacion = default);

    /// <summary>Agrega un artículo pesado con el peso estable de la balanza menos su tara (RF-19, RF-196).</summary>
    Task<RespuestaVenta> AgregarDesdeBalanzaAsync(SesionUsuario sesion, Guid ventaId, string codigo, CancellationToken cancelacion = default);

    Task<RespuestaVenta> CambiarCantidadAsync(SesionUsuario sesion, Guid ventaId, int numeroLinea, decimal cantidad, CancellationToken cancelacion = default);

    Task<RespuestaVenta> EliminarLineaAsync(SesionUsuario sesion, Guid ventaId, int numeroLinea, Guid? autorizacionId, CancellationToken cancelacion = default);

    Task<RespuestaVenta> EliminarPorCodigoAsync(SesionUsuario sesion, Guid ventaId, string codigo, Guid? autorizacionId, CancellationToken cancelacion = default);

    /// <summary>Limpia la pantalla: anula la venta en curso y empieza una nueva (RF-146).</summary>
    Task<RespuestaVenta> LimpiarAsync(SesionUsuario sesion, Guid ventaId, Guid? autorizacionId, CancellationToken cancelacion = default);

    /// <summary>
    /// Asigna el cliente por RNC/cédula: registrado, del padrón DGII o, si no está en ninguno, con el nombre indicado (RF-13, RF-181).
    /// El comprobante pasa al habitual del cliente sin pedir autorización.
    /// </summary>
    Task<RespuestaVenta> AsignarClienteAsync(SesionUsuario sesion, Guid ventaId, string documento, string? nombre, CancellationToken cancelacion = default);

    Task<RespuestaVenta> QuitarClienteAsync(SesionUsuario sesion, Guid ventaId, CancellationToken cancelacion = default);

    /// <summary>Asigna el miembro del programa de fidelidad por su cédula (RF-126, RF-236); si no está inscrito responde <see cref="CodigoResultadoVenta.NoInscritoFidelidad"/>.</summary>
    Task<RespuestaVenta> AsignarFidelidadAsync(SesionUsuario sesion, Guid ventaId, string cedula, CancellationToken cancelacion = default);

    Task<RespuestaVenta> QuitarFidelidadAsync(SesionUsuario sesion, Guid ventaId, CancellationToken cancelacion = default);

    /// <summary>Cambiar el comprobante a mano requiere permiso (RF-108, RF-127).</summary>
    Task<RespuestaVenta> CambiarComprobanteAsync(SesionUsuario sesion, Guid ventaId, TipoComprobante tipo, Guid? autorizacionId, CancellationToken cancelacion = default);

    Task<RespuestaVenta> EstablecerLimiteCompraAsync(SesionUsuario sesion, Guid ventaId, decimal? limite, CancellationToken cancelacion = default);

    /// <summary>Pone la venta en espera y empieza otra (RF-22).</summary>
    Task<RespuestaVenta> PonerEnEsperaAsync(SesionUsuario sesion, Guid ventaId, CancellationToken cancelacion = default);

    Task<IReadOnlyList<DatosVentaEnEspera>> ListarEnEsperaAsync(SesionUsuario sesion, CancellationToken cancelacion = default);

    /// <summary>Retoma una venta en espera del cajero en su turno; la venta en curso pasa a espera (o se descarta si está vacía).</summary>
    Task<RespuestaVenta> RetomarAsync(SesionUsuario sesion, Guid ventaId, CancellationToken cancelacion = default);

    /// <summary>Anula la transacción con motivo y autorización; no consume NCF (RF-194).</summary>
    Task<RespuestaVenta> AnularAsync(SesionUsuario sesion, Guid ventaId, string? motivo, Guid? autorizacionId, CancellationToken cancelacion = default);

    /// <summary>Suspende las operaciones de la caja (bloqueo de pantalla) con permiso o clave de supervisor (RF-23).</summary>
    Task<RespuestaVenta> SuspenderAsync(SesionUsuario sesion, Guid? autorizacionId, CancellationToken cancelacion = default);

    /// <summary>
    /// Descuento manual a una línea (RF-199) con permiso o clave de supervisor (RF-78), motivo (RF-203) y dentro del tope
    /// del nivel de quien autoriza (RF-202). Si lo supera, responde <see cref="CodigoResultadoVenta.TopeDescuentoExcedido"/>.
    /// </summary>
    Task<RespuestaVenta> AplicarDescuentoLineaAsync(SesionUsuario sesion, Guid ventaId, int numeroLinea, TipoDescuento tipo, decimal valor, string? motivo,
        Guid? autorizacionId, CancellationToken cancelacion = default);

    Task<RespuestaVenta> QuitarDescuentoLineaAsync(SesionUsuario sesion, Guid ventaId, int numeroLinea, CancellationToken cancelacion = default);

    /// <summary>Descuento a la factura completa o a las líneas elegidas, prorrateado (RF-200, RF-201).</summary>
    Task<RespuestaVenta> AplicarDescuentoFacturaAsync(SesionUsuario sesion, Guid ventaId, TipoDescuento tipo, decimal valor, IReadOnlyList<int>? lineas,
        string? motivo, Guid? autorizacionId, CancellationToken cancelacion = default);

    Task<RespuestaVenta> QuitarDescuentoFacturaAsync(SesionUsuario sesion, Guid ventaId, CancellationToken cancelacion = default);

    /// <summary>Desactiva la oferta de un artículo en la venta, con permiso (RF-124).</summary>
    Task<RespuestaVenta> DesactivarPromocionAsync(SesionUsuario sesion, Guid ventaId, int numeroLinea, Guid? autorizacionId, CancellationToken cancelacion = default);

    Task<IReadOnlyList<DatosMotivoDescuento>> ListarMotivosDescuentoAsync(CancellationToken cancelacion = default);

    /// <summary>Ofertas vigentes ahora en esta sucursal para el artículo (RF-24, RF-141).</summary>
    Task<IReadOnlyList<DatosPromocionVigente>> ListarPromocionesVigentesAsync(SesionUsuario sesion, Guid articuloId, CancellationToken cancelacion = default);
}

/// <summary>Cobro de la venta y periféricos de caja (M08, M15 local).</summary>
public interface IServicioCobro
{
    /// <summary>Envía el monto al terminal de pago y registra la operación, aprobada o no (RF-100).</summary>
    Task<RespuestaOperacionTerminal> CobrarConTerminalAsync(SesionUsuario sesion, Guid ventaId, decimal monto, CancellationToken cancelacion = default);

    /// <summary>Anula en el terminal la última tarjeta aprobada de la venta que aún no se aplicó (RF-214).</summary>
    Task<RespuestaOperacionTerminal> AnularUltimaOperacionAsync(SesionUsuario sesion, Guid ventaId, CancellationToken cancelacion = default);

    /// <summary>
    /// Cobra la venta con uno o varios pagos (RF-211). La venta cobrada, su mensaje para el Central y la auditoría se guardan
    /// en la misma transacción; después se imprime el ticket, se abre la gaveta si corresponde (RF-112) y empieza otra venta.
    /// </summary>
    Task<RespuestaCobro> CobrarAsync(SesionUsuario sesion, Guid ventaId, IReadOnlyList<SolicitudPago> pagos, Guid? autorizacionId, CancellationToken cancelacion = default);

    /// <summary>Reimprime como copia el ticket de la última venta cobrada en la caja.</summary>
    Task<RespuestaImpresion> ReimprimirUltimoAsync(SesionUsuario sesion, CancellationToken cancelacion = default);

    /// <summary>Abre la gaveta sin venta, con permiso (RF-112).</summary>
    Task<RespuestaVenta> AbrirGavetaAsync(SesionUsuario sesion, Guid? autorizacionId, CancellationToken cancelacion = default);
}

public sealed record ResultadoPermiso(bool Permitido, bool PorAutorizacion, bool AutorizacionRechazada, Guid? SupervisorId, string? SupervisorNombre, string? Motivo)
{
    public static ResultadoPermiso PermisoPropio { get; } = new(true, false, false, null, null, null);
}

public interface IValidadorAutorizaciones
{
    /// <summary>
    /// Permite la operación si el usuario tiene el permiso, o si trae una autorización de supervisor vigente,
    /// sin usar, del mismo permiso, solicitante y caja. La autorización se marca como usada sin guardar:
    /// se persiste con el mismo SaveChanges de la operación.
    /// </summary>
    Task<ResultadoPermiso> VerificarAsync(SesionUsuario sesion, string permiso, Guid? autorizacionId, string tipoEntidad, string? entidadId,
        CancellationToken cancelacion = default);
}

public interface IEstadoSincronizacion
{
    Task<DatosEstadoSincronizacion> ObtenerAsync(CancellationToken cancelacion = default);
}
