using CgPos.Dominio.Catalogo;
using CgPos.Dominio.Fiscal;
using CgPos.Dominio.Ventas;

namespace CgPos.Dominio.Devoluciones;

/// <summary>
/// Factura que se va a devolver, vista por la devolución. Es una proyección y no el agregado <see cref="Venta"/> porque una
/// nota de crédito se le puede hacer a una factura de cualquier tienda: esa llega del Central a una copia temporal y nunca
/// existe como venta en la base de la caja.
/// </summary>
/// <param name="VentaId">Venta local de origen; nula cuando la factura vino del Central.</param>
public sealed record FacturaParaDevolver(
    int? VentaId,
    string NumeroTransaccion,
    DateTimeOffset CobradaEn,
    int SucursalId,
    int CajaId,
    TipoComprobante TipoComprobante,
    string Moneda,
    string SimboloMoneda,
    IReadOnlyList<LineaFacturaParaDevolver> Lineas)
{
    /// <summary>La factura tal como está en la base de esta caja. Solo cuenta las líneas activas: las eliminadas no se vendieron.</summary>
    public static FacturaParaDevolver De(Venta venta)
    {
        ArgumentNullException.ThrowIfNull(venta);
        if (venta.Estado != EstadoVenta.Cobrada || venta.CobradaEn is not { } cobradaEn)
            throw new ReglaDevolucionExcepcion(CodigoErrorDevolucion.FacturaNoCobrada, $"La transacción {venta.NumeroTransaccion} no es una factura cobrada.");

        return new FacturaParaDevolver(venta.Id, venta.NumeroTransaccion, cobradaEn, venta.SucursalId, venta.CajaId, venta.TipoComprobante,
            venta.Moneda, venta.SimboloMoneda,
            venta.Lineas.Where(l => l.EstaActiva).Select(LineaFacturaParaDevolver.De).ToList());
    }
}

/// <param name="ImporteConImpuesto">Importe neto de la línea con ITBIS, ya descontadas las promociones y los descuentos.</param>
public sealed record LineaFacturaParaDevolver(
    int NumeroLinea,
    int ArticuloId,
    string CodigoInterno,
    string CodigoLeido,
    string Descripcion,
    TipoArticulo TipoArticulo,
    string UnidadMedidaCodigo,
    bool PermiteDecimales,
    int DecimalesCantidad,
    decimal Cantidad,
    decimal ImporteConImpuesto,
    decimal PorcentajeImpuesto,
    int IndicadorFacturacion,
    bool EsServicio,
    string? Serial)
{
    public static LineaFacturaParaDevolver De(LineaVenta linea)
    {
        ArgumentNullException.ThrowIfNull(linea);
        return new LineaFacturaParaDevolver(linea.NumeroLinea, linea.ArticuloId, linea.CodigoInterno, linea.CodigoLeido, linea.Descripcion,
            linea.TipoArticulo, linea.UnidadMedidaCodigo, linea.PermiteDecimales, linea.DecimalesCantidad, linea.Cantidad,
            linea.ImporteConImpuesto, linea.PorcentajeImpuesto, linea.IndicadorFacturacion, linea.EsServicio, linea.Serial);
    }
}
