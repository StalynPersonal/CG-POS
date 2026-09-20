namespace CgPos.Contratos.Central;

/// <summary>
/// Factura tal como la necesita una caja para devolverla, venga de la tienda que venga. El Central es el único que ve las
/// devoluciones de toda la empresa, así que es él quien dice cuánto queda disponible de cada línea.
/// </summary>
/// <param name="Propia">La emitió esta misma caja: puede devolverla con lo que tiene en su base, sin depender de la red.</param>
public sealed record DatosFacturaParaCaja(
    string Numero,
    string? Encf,
    string SucursalCodigo,
    string CajaCodigo,
    bool Propia,
    DateOnly FechaOperacion,
    DateTimeOffset CobradaEn,
    string? ClienteDocumento,
    string? ClienteNombre,
    string Moneda,
    decimal Total,
    IReadOnlyList<DatosLineaFacturaParaCaja> Lineas);

/// <param name="Devuelta">Cantidad ya devuelta de esa línea en cualquier caja de la empresa.</param>
public sealed record DatosLineaFacturaParaCaja(
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
    decimal Devuelta)
{
    /// <summary>Lo que todavía se puede devolver.</summary>
    public decimal Disponible => Math.Max(0m, Cantidad - Devuelta);
}

/// <summary>Resultado corto de buscar facturas en el Central cuando el cliente no trae el ticket.</summary>
public sealed record ResumenFacturaParaCaja(
    string Numero,
    string? Encf,
    string SucursalCodigo,
    string CajaCodigo,
    DateOnly FechaOperacion,
    string? ClienteDocumento,
    string? ClienteNombre,
    decimal Total);
