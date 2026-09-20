using CgPos.Dominio.Cotizaciones;

namespace CgPos.Contratos.Central;

/// <summary>Cotización como la ve el Central Manager, con sus líneas y sus totales ya calculados.</summary>
/// <param name="Vencida">Pasó su fecha y sigue abierta: se factura solo con autorización de un supervisor.</param>
public sealed record DatosCotizacion(
    int Id,
    string Numero,
    string ClienteNombre,
    string? ClienteDocumento,
    string? ClienteTelefono,
    string? ClienteCorreo,
    int? SucursalId,
    string? SucursalNombre,
    DateOnly VenceEn,
    bool Vencida,
    EstadoCotizacion Estado,
    string? VentaNumero,
    string? Observacion,
    string? MotivoAnulacion,
    string CreadaPor,
    DateTimeOffset CreadaEn,
    decimal Subtotal,
    decimal Impuesto,
    decimal Descuento,
    decimal Total,
    IReadOnlyList<DatosLineaCotizacionCentral> Lineas);

/// <param name="PrecioUnitario">Con impuesto incluido, congelado al cotizar.</param>
public sealed record DatosLineaCotizacionCentral(
    int NumeroLinea,
    string ArticuloCodigo,
    string Descripcion,
    string? UnidadMedida,
    decimal Cantidad,
    decimal PrecioUnitario,
    decimal Descuento,
    decimal PorcentajeImpuesto,
    int IndicadorFacturacion,
    decimal Importe);

/// <param name="VenceEn">Si no viene, se calcula con los días de vigencia configurados en el Central.</param>
public sealed record SolicitudCotizacion(
    string ClienteNombre,
    string? ClienteDocumento,
    string? ClienteTelefono,
    string? ClienteCorreo,
    int? SucursalId,
    DateOnly? VenceEn,
    string? Observacion,
    IReadOnlyList<SolicitudLineaCotizacion> Lineas);

/// <param name="PrecioUnitario">Con impuesto incluido; si no viene, se toma el precio vigente del artículo.</param>
public sealed record SolicitudLineaCotizacion(string ArticuloCodigo, decimal Cantidad, decimal? PrecioUnitario, decimal Descuento = 0m);

public sealed record SolicitudAnularCotizacion(string Motivo);

/// <summary>Lo que la caja necesita para convertirla en factura: sin Ids del Central y con los precios congelados.</summary>
/// <param name="Vencida">La caja la acepta igual, pero pidiendo autorización de un supervisor.</param>
public sealed record DatosCotizacionParaCaja(
    string Numero,
    string ClienteNombre,
    string? ClienteDocumento,
    EstadoCotizacion Estado,
    DateOnly VenceEn,
    bool Vencida,
    decimal Total,
    IReadOnlyList<DatosLineaCotizacionParaCaja> Lineas);

public sealed record DatosLineaCotizacionParaCaja(
    string ArticuloCodigo,
    string Descripcion,
    decimal Cantidad,
    decimal PrecioUnitario,
    decimal Descuento);
