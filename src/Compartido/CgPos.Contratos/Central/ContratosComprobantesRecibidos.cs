using CgPos.Dominio.Fiscal;
using CgPos.Dominio.Pagos;
using CgPos.Dominio.Reportes;
using CgPos.Dominio.Sincronizacion;

namespace CgPos.Contratos.Central;

/// <summary>Fila del listado de facturas y notas de crédito que subieron las cajas.</summary>
public sealed record DatosComprobanteRecibido(
    int Id,
    TipoComprobanteVenta Tipo,
    string Numero,
    string? Encf,
    TipoComprobante TipoComprobanteFiscal,
    string SucursalNombre,
    string CajaCodigo,
    long? TurnoNumero,
    string UsuarioNombre,
    DateTimeOffset Fecha,
    DateOnly FechaOperacion,
    string? ClienteDocumento,
    string? ClienteNombre,
    string Moneda,
    decimal Subtotal,
    decimal Descuento,
    decimal Impuesto,
    decimal Total,
    int CantidadLineas,
    EstadoEnvioDgii? EstadoDgii,
    DateTimeOffset RecibidoEn);

public sealed record PaginaComprobantesRecibidos(IReadOnlyList<DatosComprobanteRecibido> Elementos, int Total, decimal SumaTotal);

public sealed record DatosLineaComprobanteRecibido(
    int NumeroLinea,
    string Codigo,
    string Descripcion,
    string? UnidadMedida,
    decimal Cantidad,
    decimal PrecioUnitario,
    decimal Descuento,
    decimal Impuesto,
    decimal Importe,
    string? Serial,
    string? PromocionCodigo);

public sealed record DatosPagoComprobanteRecibido(TipoFormaPago Tipo, string FormaPagoNombre, string Moneda, decimal Monto);

public sealed record DatosImpuestoComprobanteRecibido(decimal Porcentaje, decimal Base, decimal Impuesto);

/// <param name="TieneXml">El e-CF firmado está en el Central y se puede descargar desde el monitor de comprobantes.</param>
public sealed record DatosComprobanteRecibidoDetalle(
    DatosComprobanteRecibido Comprobante,
    string? EncfModificado,
    decimal ImpuestoRetenido,
    IReadOnlyList<DatosLineaComprobanteRecibido> Lineas,
    IReadOnlyList<DatosImpuestoComprobanteRecibido> Impuestos,
    IReadOnlyList<DatosPagoComprobanteRecibido> Pagos,
    bool TieneXml,
    string? MensajeDgii);
