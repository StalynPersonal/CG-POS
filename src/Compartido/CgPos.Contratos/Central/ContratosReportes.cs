using CgPos.Dominio.Fiscal;
using CgPos.Dominio.Sincronizacion;
using CgPos.Dominio.Reportes;

namespace CgPos.Contratos.Central;

/// <summary>Reportes del Central. Los rangos de fecha son días de operación de la caja, inclusive.</summary>
public enum TipoReporteCentral
{
    /// <summary>Ventas por día, sucursal y caja.</summary>
    Ventas,

    /// <summary>ITBIS por tasa del período.</summary>
    Itbis,

    /// <summary>Formato 607 de la DGII: ventas de bienes y servicios del período.</summary>
    Formato607,

    /// <summary>Cuadres de caja con lo esperado, lo declarado y la diferencia.</summary>
    Cuadres,

    /// <summary>e-CF emitidos y su estado en la DGII.</summary>
    Ecf,

    /// <summary>Estado de la sincronización de cada caja.</summary>
    Sincronizacion,
}

/// <param name="Desde">Primer día de operación incluido.</param>
/// <param name="Hasta">Último día de operación incluido.</param>
public sealed record FiltroReporte(DateOnly Desde, DateOnly Hasta, int? SucursalId = null, int? CajaId = null);

/// <summary>Resultado de un reporte en forma de tabla: sirve para la pantalla y para exportarlo a Excel o PDF sin duplicar la consulta.</summary>
/// <param name="Alineaciones">Por columna, <c>true</c> si es numérica (se alinea a la derecha y se totaliza).</param>
public sealed record TablaReporte(
    string Titulo,
    string Subtitulo,
    IReadOnlyList<string> Columnas,
    IReadOnlyList<bool> Alineaciones,
    IReadOnlyList<IReadOnlyList<string>> Filas,
    IReadOnlyList<string>? Totales = null);

public sealed record DatosVentasReporte(
    DateOnly Fecha,
    string SucursalCodigo,
    string CajaCodigo,
    int Facturas,
    int NotasCredito,
    decimal Subtotal,
    decimal Descuento,
    decimal Impuesto,
    decimal Total);

public sealed record DatosItbisReporte(decimal Porcentaje, decimal Base, decimal Impuesto, int Comprobantes);

/// <param name="TipoIdentificacion">1 = RNC, 2 = cédula, 3 = pasaporte o sin identificar.</param>
public sealed record DatosFormato607(
    string? Rnc,
    int TipoIdentificacion,
    TipoComprobante TipoComprobante,
    string Encf,
    string? EncfModificado,
    DateOnly Fecha,
    decimal MontoFacturado,
    decimal ItbisFacturado,
    decimal ItbisRetenido,
    string TipoVenta);

public sealed record DatosCuadreReporte(
    DateOnly Fecha,
    string SucursalCodigo,
    string CajaCodigo,
    long TurnoNumero,
    string UsuarioNombre,
    int CantidadVentas,
    decimal TotalVentas,
    decimal Esperado,
    decimal Declarado,
    decimal Diferencia,
    bool Ciego);

public sealed record DatosEcfReporte(
    string Encf,
    TipoComprobante TipoComprobante,
    string SucursalCodigo,
    string CajaCodigo,
    DateTimeOffset RecibidoEn,
    EstadoEnvioDgii Estado,
    string? TrackId,
    string? Mensaje);

public sealed record ResumenReporteCentral(TipoReporteCentral Tipo, TablaReporte Tabla);
