using CgPos.Contratos.Central;

namespace CgPos.Central.Aplicacion.Reportes;

/// <summary>
/// Reportes del Central sobre lo que informan las cajas (M16): ventas, ITBIS, formato 607 de la DGII, cuadres, e-CF y sincronización.
/// Cada uno devuelve sus datos y también una tabla lista para exportar, para que pantalla, Excel y PDF muestren exactamente lo mismo.
/// </summary>
public interface IServicioReportesCentral
{
    Task<IReadOnlyList<DatosVentasReporte>> VentasAsync(FiltroReporte filtro, CancellationToken cancelacion = default);

    Task<IReadOnlyList<DatosItbisReporte>> ItbisAsync(FiltroReporte filtro, CancellationToken cancelacion = default);

    Task<IReadOnlyList<DatosFormato607>> Formato607Async(FiltroReporte filtro, CancellationToken cancelacion = default);

    Task<IReadOnlyList<DatosCuadreReporte>> CuadresAsync(FiltroReporte filtro, CancellationToken cancelacion = default);

    Task<IReadOnlyList<DatosEcfReporte>> EcfAsync(FiltroReporte filtro, CancellationToken cancelacion = default);

    /// <summary>Tabla del reporte, tal como se ve en pantalla y como se exporta.</summary>
    Task<TablaReporte> TablaAsync(TipoReporteCentral tipo, FiltroReporte filtro, CancellationToken cancelacion = default);
}

/// <summary>Archivo generado para descargar.</summary>
public sealed record ArchivoReporte(string Nombre, string TipoContenido, byte[] Contenido);

/// <summary>Exporta una tabla de reporte a los formatos que pide el negocio (RF-286).</summary>
public interface IExportadorReportes
{
    ArchivoReporte AExcel(TablaReporte tabla);

    ArchivoReporte APdf(TablaReporte tabla);

    /// <summary>Archivo de texto del formato 607 con el diseño que pide la DGII (una línea por comprobante, campos con "|").</summary>
    ArchivoReporte A607(string rncEmisor, DateOnly periodo, IReadOnlyList<DatosFormato607> filas);
}
