using CgPos.Dominio.Catalogo;
using CgPos.Dominio.Devoluciones;
using CgPos.Dominio.Fiscal;

namespace CgPos.Contratos.Ventas;

public sealed record DatosMotivoDevolucion(int Codigo, string Nombre);

/// <param name="ImporteDisponible">Lo que se acreditaría si se devuelve todo lo disponible, antes de retener el ITBIS.</param>
public sealed record DatosLineaFacturaDevolucion(
    int NumeroLinea,
    Guid ArticuloId,
    string CodigoInterno,
    string CodigoLeido,
    string Descripcion,
    TipoArticulo TipoArticulo,
    string UnidadMedidaCodigo,
    int DecimalesCantidad,
    bool PermiteDecimales,
    decimal CantidadVendida,
    decimal CantidadDevuelta,
    decimal CantidadDisponible,
    decimal PrecioUnitario,
    decimal ImporteDisponible,
    decimal PorcentajeImpuesto,
    bool RequiereSerial);

public sealed record DatosNotaCreditoResumen(Guid Id, string Numero, string? Encf, decimal Total, DateTimeOffset CreadaEn);

/// <summary>Factura llamada para devolver (RF-159): lo vendido, lo ya devuelto y lo disponible por línea.</summary>
/// <param name="RetieneImpuesto">Pasó el plazo de devolución: la nota acredita sin ITBIS (RF-44).</param>
public sealed record DatosFacturaDevolucion(
    Guid VentaId,
    string NumeroTransaccion,
    string? Encf,
    TipoComprobante TipoComprobante,
    DateTimeOffset CobradaEn,
    int DiasTranscurridos,
    bool RetieneImpuesto,
    int DiasRetencionImpuesto,
    DatosClienteVenta? Cliente,
    IReadOnlyList<DatosLineaFacturaDevolucion> Lineas,
    IReadOnlyList<DatosMotivoDevolucion> Motivos,
    IReadOnlyList<DatosNotaCreditoResumen> NotasPrevias);

public sealed record SolicitudLineaDevolucion(int NumeroLinea, decimal Cantidad, string? Serial = null);

/// <param name="ClienteDocumento">Obligatorio si la factura no tiene cliente (RF-160).</param>
/// <param name="Reembolso">Cómo se le devuelve el dinero (RF-123); por omisión queda como saldo en la nota de crédito.</param>
/// <param name="ReembolsoReferencia">Operación del terminal para la tarjeta o número del cheque.</param>
/// <param name="ReembolsoDetalle">Banco del cheque, tarjeta o quien recibe el efectivo.</param>
public sealed record SolicitudDevolucion(
    Guid VentaId,
    IReadOnlyList<SolicitudLineaDevolucion> Lineas,
    string? ClienteDocumento,
    string? ClienteNombre,
    int? MotivoCodigo,
    string? Observacion,
    Guid? AutorizacionId,
    TipoReembolso Reembolso = TipoReembolso.SaldoNotaCredito,
    string? ReembolsoReferencia = null,
    string? ReembolsoDetalle = null);

public sealed record DatosLineaNotaCredito(
    int NumeroLineaOrigen,
    string CodigoInterno,
    string CodigoLeido,
    string Descripcion,
    string UnidadMedidaCodigo,
    int DecimalesCantidad,
    decimal Cantidad,
    decimal PrecioUnitario,
    decimal PorcentajeImpuesto,
    decimal Base,
    decimal Impuesto,
    decimal ImpuestoRetenido,
    decimal Importe,
    string? Serial);

public sealed record DatosNotaCredito(
    Guid Id,
    string Numero,
    Guid VentaOrigenId,
    string VentaOrigenNumero,
    string? EncfOrigen,
    DateTimeOffset VentaOrigenCobradaEn,
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
    decimal Saldo,
    string Moneda,
    DateOnly VenceEn,
    EstadoNotaCredito Estado,
    DateTimeOffset CreadaEn,
    IReadOnlyList<DatosLineaNotaCredito> Lineas,
    DatosComprobanteElectronico? Comprobante,
    int PuntosReversados = 0,
    TipoReembolso Reembolso = TipoReembolso.SaldoNotaCredito,
    string? ReembolsoReferencia = null,
    string? ReembolsoDetalle = null);


public sealed record DatosSaldoNotaCredito(Guid Id, string Numero, string? Encf, string ClienteNombre, decimal Total, decimal Saldo, DateOnly VenceEn, EstadoNotaCredito Estado);

public enum CodigoResultadoDevolucion
{
    Correcto,
    FacturaNoEncontrada,
    FacturaNoCobrada,
    TodoDevuelto,
    RequiereAutorizacion,
    AutorizacionInvalida,
    DevolucionInvalida,
    CertificadoNoCargado,
    ComprobanteNoDisponible,
    EcfInvalido,
    NotaCreditoNoEncontrada,
    NotaCreditoConsumida,
    NotaCreditoVencida,
}

public sealed record RespuestaFacturaDevolucion(CodigoResultadoDevolucion Resultado, string? Mensaje, DatosFacturaDevolucion? Factura)
{
    public bool Exitosa => Resultado == CodigoResultadoDevolucion.Correcto;
}

public sealed record RespuestaDevolucion(CodigoResultadoDevolucion Resultado, string? Mensaje, string? PermisoRequerido = null, DatosNotaCredito? NotaCredito = null)
{
    public bool Exitosa => Resultado == CodigoResultadoDevolucion.Correcto;
}

public sealed record RespuestaSaldoNotaCredito(CodigoResultadoDevolucion Resultado, string? Mensaje, DatosSaldoNotaCredito? NotaCredito)
{
    public bool Exitosa => Resultado == CodigoResultadoDevolucion.Correcto;
}
