using CgPos.Contratos.Ventas;
using CgPos.Dominio.Catalogo;
using CgPos.Dominio.Devoluciones;
using CgPos.Dominio.Entregas;
using CgPos.Dominio.Fidelidad;
using CgPos.Dominio.Fiscal;
using CgPos.Dominio.Pagos;
using CgPos.Dominio.Turnos;
using CgPos.Dominio.Ventas;

namespace CgPos.Contratos.Sincronizacion;

// Documentos que la caja envía al Central. No llevan Id: cada documento se identifica por su número (sucursal + caja + tipo + secuencia)
// o por su llave natural, y cada referencia va por código. La caja que lo envía la conoce el Central por la credencial.

/// <summary>
/// Línea de la venta cobrada; el artículo va por su código interno. El precio y el importe van sin ITBIS; el ITBIS de la
/// línea va aparte.
/// </summary>
public sealed record DocumentoLineaVenta(
    int NumeroLinea,
    string CodigoInterno,
    string CodigoLeido,
    string Descripcion,
    TipoArticulo TipoArticulo,
    string UnidadMedidaCodigo,
    int DecimalesCantidad,
    decimal Cantidad,
    decimal PrecioUnitario,
    decimal Importe,
    decimal PorcentajeImpuesto,
    ListaPrecio Lista,
    MotivoPrecio MotivoPrecio,
    bool LeidaDeBalanza,
    bool Anulada,
    string? Serial,
    string? PromocionCodigo,
    decimal DescuentoPromocion,
    decimal DescuentoManual,
    TipoDescuento? DescuentoManualTipo,
    decimal? DescuentoManualValor,
    string? MotivoDescuento,
    string? DescuentoAutorizadoPorNombre,
    decimal DescuentoFactura,
    decimal ImporteBruto,
    decimal CantidadEnEntrega,
    decimal Impuesto)
{
    /// <summary>Lo que pagó el cliente por la línea: su importe sin ITBIS más su ITBIS.</summary>
    public decimal ImporteConImpuesto => Importe + Impuesto;
}

/// <param name="ClienteCodigo">Código del cliente registrado; nulo si se identificó solo con el documento.</param>
public sealed record DocumentoClienteVenta(string? ClienteCodigo, TipoDocumentoIdentidad? TipoDocumento, string? Documento, string Nombre);

public sealed record DocumentoPagoVenta(
    int Numero,
    string FormaPagoCodigo,
    string FormaPagoNombre,
    TipoFormaPago Tipo,
    string Moneda,
    decimal MontoRecibido,
    decimal? TasaCambio,
    decimal MontoAplicado,
    string? Referencia,
    string? BancoNombre,
    string? TipoTarjetaNombre,
    string? UltimosDigitos,
    bool AprobacionManual);

public sealed record DocumentoFidelidadVenta(string Cedula, string Nombre, int PuntosAcumulados, int PuntosCanjeados);

public sealed record DocumentoDestinoEntrega(
    int Numero,
    MetodoEntrega Metodo,
    string? SucursalRetiroCodigo,
    string? SucursalRetiroNombre,
    string? Direccion,
    string? Sector,
    string? Ciudad,
    string? Referencia,
    string? Telefono,
    string? Transportista,
    decimal? CostoEnvio,
    DateOnly? FechaComprometida,
    string? Comentario,
    string? AutorizadoPorNombre,
    IReadOnlyList<DatosLineaDestinoEntrega> Lineas);

/// <summary>Mensaje <c>Venta.Cobrada</c>: la factura cobrada con su e-CF firmado.</summary>
public sealed record DocumentoVentaCobrada(
    string Numero,
    long TurnoNumero,
    string UsuarioCodigo,
    string UsuarioNombre,
    DateTimeOffset IniciadaEn,
    DateTimeOffset CobradaEn,
    TipoComprobante TipoComprobante,
    DocumentoClienteVenta? Cliente,
    string Moneda,
    IReadOnlyList<DocumentoLineaVenta> Lineas,
    DatosTotalesVenta Totales,
    DatosDescuentoFactura? DescuentoFactura,
    IReadOnlyList<DocumentoPagoVenta> Pagos,
    decimal TotalCobrado,
    decimal Devuelta,
    decimal RedondeoEfectivo,
    DatosComprobanteElectronico? Comprobante,
    DocumentoFidelidadVenta? Fidelidad,
    IReadOnlyList<DocumentoDestinoEntrega> DestinosEntrega,
    DocumentoElectronicoParaCentral? Ecf = null,
    string? ListaBodaNumero = null,
    /// <summary>Cotización del Central que se facturó con esta venta; el Central la marca facturada al recibirla.</summary>
    string? CotizacionNumero = null);

/// <summary>
/// Mensaje <c>Devolucion.NotaCreditoEmitida</c>: la nota de crédito con referencia a la factura por su número, y su XML firmado. La nota
/// interna (<c>EsInterna</c>) es solo un ajuste: viaja sin comprobante ni XML y no va a la DGII.
/// </summary>
public sealed record DocumentoNotaCreditoEmitida(
    string Numero,
    string VentaOrigenNumero,
    string? EncfOrigen,
    DateTimeOffset VentaOrigenCobradaEn,
    long? TurnoNumero,
    TipoDocumentoIdentidad? ClienteTipoDocumento,
    string ClienteDocumento,
    string ClienteNombre,
    int MotivoCodigo,
    string MotivoNombre,
    string? Observacion,
    string UsuarioNombre,
    string? AutorizadoPorNombre,
    bool RetieneImpuesto,
    bool EsTotal,
    decimal Subtotal,
    decimal Impuesto,
    decimal ImpuestoRetenido,
    decimal Total,
    string Moneda,
    DateOnly FechaEmision,
    DateTimeOffset CreadaEn,
    IReadOnlyList<DatosLineaNotaCredito> Lineas,
    DatosComprobanteElectronico? Comprobante,
    int PuntosReversados,
    TipoReembolso Reembolso,
    string? ReembolsoReferencia,
    string? ReembolsoDetalle,
    DocumentoElectronicoParaCentral? Ecf,
    bool EsInterna = false);

/// <summary>Mensaje <c>NotaCredito.Consumida</c>: una factura consumió saldo de una nota de crédito. La llave es la nota y la factura.</summary>
/// <param name="VentaReserva">
/// Con qué se reservó el saldo en el Central, si fue distinto del número de la factura: la reserva se hace mientras la
/// venta es un borrador sin número, y el Central la cierra con esto al registrar el consumo.
/// </param>
public sealed record DocumentoConsumoNotaCredito(string NotaCreditoNumero, string? Encf, string VentaNumero, decimal Monto, decimal SaldoRestante, DateTimeOffset Fecha,
    string? VentaReserva = null);

/// <summary>Retiro, relevo o reembolso de un turno; se identifica por el número del turno y el suyo.</summary>
public sealed record DocumentoMovimientoTurno(
    TipoMovimientoCaja Tipo,
    int Numero,
    decimal Monto,
    string? Moneda,
    string? Motivo,
    string UsuarioNombre,
    string? UsuarioAnteriorNombre,
    string? AutorizadoPorNombre,
    DateTimeOffset Fecha);

/// <summary>
/// Mensaje <c>Seguridad.IngresoUsuario</c>: un usuario entró a esta caja. Con esto, en el Central se ve el último acceso de
/// cada usuario de caja sin tener que ir terminal por terminal.
/// </summary>
public sealed record DocumentoIngresoUsuario(string UsuarioCodigo, DateTimeOffset IngresoEn);

/// <summary>
/// Mensaje <c>Fiscal.ConsumoSecuenciaEcf</c>: hasta qué número llegó la caja en un rango. Se manda cuando queda poco o
/// cuando se agota, no en cada factura: el Central solo necesita saberlo cuando hay que actuar.
/// </summary>
/// <param name="Agotada">Se usó el último número. En la caja el rango ya se borró y el Central deja de bajárselo.</param>
public sealed record DocumentoConsumoSecuenciaEcf(
    TipoComprobante TipoComprobante,
    string Serie,
    long Desde,
    long Hasta,
    long Ultimo,
    bool Agotada,
    DateTimeOffset InformadoEn);

/// <summary>
/// Mensaje <c>Caja.Suspension</c>: un rato de caja parada ya cerrado. Se manda al reanudar (o al cerrar el turno, si nadie volvió),
/// nunca mientras sigue abierto: al Central le sirve el tiempo completo, no el que va corriendo.
/// </summary>
/// <param name="Numero">El identificador de la suspensión en esa caja; con la caja forma la llave que evita duplicarla.</param>
/// <param name="CerradaPorCierreDeTurno">Nadie volvió a la caja: la cerró el cierre del turno.</param>
public sealed record DocumentoSuspensionCaja(
    int Numero,
    long TurnoNumero,
    DateOnly FechaOperacion,
    string UsuarioNombre,
    int? MotivoCodigo,
    string MotivoNombre,
    bool Programado,
    string? Nota,
    DateTimeOffset SuspendidaEn,
    DateTimeOffset ReanudadaEn,
    bool CerradaPorCierreDeTurno);

/// <summary>Mensajes <c>Caja.RetiroEfectivo</c> y <c>Caja.RelevoCajero</c>.</summary>
public sealed record DocumentoMovimientoCaja(long TurnoNumero, DocumentoMovimientoTurno Movimiento);

public sealed record DocumentoCierreFormaPago(
    string FormaPagoCodigo,
    string Nombre,
    TipoFormaPago Tipo,
    string Moneda,
    int Transacciones,
    decimal Esperado);

/// <summary>Mensaje <c>Caja.TurnoCerrado</c>: el cierre del turno; se identifica por el número del turno en la caja.</summary>
public sealed record DocumentoCierreTurno(
    long TurnoNumero,
    int Numero,
    DateOnly FechaOperacion,
    decimal FondoInicial,
    bool FondoEnCuadre,
    string Moneda,
    int CantidadVentas,
    decimal TotalVentas,
    decimal TotalRetiros,
    decimal TotalEsperado,
    string UsuarioNombre,
    DateTimeOffset AbiertoEn,
    DateTimeOffset CerradoEn,
    IReadOnlyList<DocumentoCierreFormaPago> FormasPago,
    IReadOnlyList<DocumentoMovimientoTurno> Movimientos,

    /// <summary>El cierre del lote de tarjetas del turno; nulo si el cajero no lo cerró en la caja.</summary>
    DocumentoLoteTarjetas? Lote = null);

/// <summary>El lote del terminal cuadrado contra lo aprobado en la caja (RF-215), tal como lo informa la terminal.</summary>
public sealed record DocumentoLoteTarjetas(
    string? NumeroLote,
    int TransaccionesCaja,
    decimal MontoCaja,
    int TransaccionesTerminal,
    decimal MontoTerminal,
    decimal Diferencia,
    bool DetalleDelTerminal,
    string UsuarioNombre,
    DateTimeOffset CerradoEn,
    IReadOnlyList<string> SoloEnCaja,
    IReadOnlyList<string> SoloEnTerminal);

/// <summary>Mensajes <c>Entregas.PendienteCreado</c> y <c>Entregas.PendienteActualizado</c>: el pendiente completo, identificado por su número.</summary>
public sealed record DocumentoPendienteEntrega(
    string Numero,
    string VentaNumero,
    MetodoEntrega Metodo,
    EstadoPendiente Estado,
    string? SucursalRetiroCodigo,
    string? SucursalRetiroNombre,
    string? Direccion,
    string? Sector,
    string? Ciudad,
    string? Referencia,
    string? Telefono,
    string? Transportista,
    decimal? CostoEnvio,
    DateOnly? FechaComprometida,
    string? Comentario,
    string? ClienteDocumento,
    string? ClienteNombre,
    string VendidoPorNombre,
    string? AutorizadoPorNombre,
    DateTimeOffset CreadoEn,
    DateTimeOffset ActualizadoEn,
    string ActualizadoPorNombre,
    string? MotivoAnulacion,
    IReadOnlyList<DatosLineaPendiente> Lineas,
    IReadOnlyList<DatosEntregaPendiente> Entregas);

/// <summary>Mensaje <c>Fidelidad.Inscripcion</c>: inscripción hecha en caja (RF-237); el miembro se identifica por su cédula.</summary>
public sealed record DocumentoInscripcionFidelidad(string Cedula, string Nombre, string? Telefono, string? Correo, string UsuarioNombre, DateTimeOffset InscritoEn);

/// <summary>
/// Mensaje <c>Fidelidad.MovimientoPuntos</c>: puntos acumulados, canjeados o reversados en la caja. <paramref name="Documento"/> es el número de la
/// factura o de la nota de crédito que los originó; con el tipo identifica el movimiento.
/// </summary>
public sealed record DocumentoMovimientoPuntos(string Cedula, TipoMovimientoPuntos Tipo, int Puntos, string Documento, DateTimeOffset Fecha, DateOnly? VenceEn);
