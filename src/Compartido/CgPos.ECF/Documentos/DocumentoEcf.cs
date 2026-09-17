namespace CgPos.ECF.Documentos;

/// <summary>
/// Datos de un comprobante fiscal electrónico para generar su XML. Independiente del POS: la caja lo arma desde la venta.
/// Estructura según el formato e-CF 1.0 de la DGII; pendiente de validar contra los XSD oficiales y TesteCF.
/// </summary>
/// <param name="TipoEcf">31 crédito fiscal, 32 consumo, 33 nota de débito, 34 nota de crédito, 44 régimen especial, 45 gubernamental…</param>
/// <param name="Encf">E + tipo (2 dígitos) + secuencia (10 dígitos), ej. E320000000001.</param>
/// <param name="TipoIngresos">Tabla de tipos de ingresos de la DGII (1 a 6); lo define el negocio.</param>
/// <param name="TipoPago">1 contado, 2 crédito, 3 gratuito.</param>
public sealed record DocumentoEcf(
    int TipoEcf,
    string Encf,
    DateOnly? FechaVencimientoSecuencia,
    EmisorEcf Emisor,
    CompradorEcf? Comprador,
    IReadOnlyList<ItemEcf> Items,
    TotalesEcf Totales,
    DateTimeOffset FechaEmision,
    DateTimeOffset FechaHoraFirma,
    int TipoIngresos,
    int TipoPago,
    IReadOnlyList<FormaPagoEcf>? FormasPago = null,
    ReferenciaEcf? Referencia = null);

public sealed record EmisorEcf(string Rnc, string RazonSocial, string? NombreComercial, string? Sucursal, string Direccion);

/// <param name="Rnc">RNC (9 dígitos) o cédula (11 dígitos) del comprador.</param>
public sealed record CompradorEcf(string? Rnc, string? RazonSocial);

/// <param name="IndicadorFacturacion">1 = ITBIS 18 %, 2 = ITBIS 16 %, 3 = ITBIS 0 %, 4 = exento.</param>
/// <param name="PrecioUnitario">Sin ITBIS.</param>
/// <param name="Descuento">Descuento de la línea sin ITBIS (ofertas, manuales y prorrateo de la factura).</param>
/// <param name="Monto">Monto de la línea sin ITBIS, después del descuento.</param>
/// <param name="IndicadorBienServicio">1 = bien, 2 = servicio.</param>
public sealed record ItemEcf(
    int NumeroLinea,
    int IndicadorFacturacion,
    string Nombre,
    decimal Cantidad,
    string? UnidadMedida,
    decimal PrecioUnitario,
    decimal Descuento,
    decimal Monto,
    int IndicadorBienServicio = 1);

/// <summary>Totales sin ITBIS por tasa, ITBIS por tasa y monto total.</summary>
/// <param name="TasaItbis1">Porcentaje de ITBIS de las líneas con indicador 1, tomado del maestro de impuestos; obligatorio si hay monto gravado I1.</param>
public sealed record TotalesEcf(
    decimal MontoGravadoI1,
    decimal MontoGravadoI2,
    decimal MontoGravadoI3,
    decimal MontoExento,
    decimal TotalItbis1,
    decimal TotalItbis2,
    decimal TotalItbis3,
    decimal MontoTotal,
    decimal? TasaItbis1 = null,
    decimal? TasaItbis2 = null,
    decimal? TasaItbis3 = null)
{
    public decimal MontoGravadoTotal => MontoGravadoI1 + MontoGravadoI2 + MontoGravadoI3;

    public decimal TotalItbis => TotalItbis1 + TotalItbis2 + TotalItbis3;
}

/// <param name="FormaPago">1 efectivo, 2 cheque/transferencia/depósito, 3 tarjeta de débito o crédito, 4 venta a crédito,
/// 5 bonos o certificados de regalo, 6 permuta, 7 nota de crédito, 8 otras formas de pago.</param>
public sealed record FormaPagoEcf(int FormaPago, decimal Monto);

/// <param name="CodigoModificacion">1 anula el comprobante, 2 corrige texto, 3 corrige montos, 4 reemplaza por contingencia, 5 referencia a factura de consumo.</param>
public sealed record ReferenciaEcf(string NcfModificado, DateOnly FechaNcfModificado, int CodigoModificacion);
