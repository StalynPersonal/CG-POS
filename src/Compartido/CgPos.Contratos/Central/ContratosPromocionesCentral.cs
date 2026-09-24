using CgPos.Contratos.Catalogo;

namespace CgPos.Contratos.Central;

/// <param name="Oferta">Descripción corta como la ve el cajero en la columna Promo (ej. "-10%", "2x1").</param>
/// <param name="CajasDestino">Cajas habilitadas de las sucursales donde aplica.</param>
/// <param name="CajasConPromocion">De esas, las que ya confirmaron tener aplicada esta versión de la promoción.</param>
/// <summary>Apagar o encender una promoción publicada; es lo único que se le puede cambiar.</summary>
public sealed record SolicitudEstadoPromocion(bool Activa);

public sealed record DatosPromocionCentral(
    PromocionCarga Promocion,
    string Oferta,
    int CajasDestino,
    int CajasConPromocion,
    DateTimeOffset ModificadoEn,
    string ModificadoPor);

/// <param name="Contenido">Texto del CSV (separador ; o ,).</param>
/// <param name="DesplazamientoMinutos">Zona horaria de quien importa, para leer las fechas y horas del archivo (ej. -240).</param>
/// <param name="SoloValidar">Revisa el archivo sin publicar nada.</param>
public sealed record SolicitudImportacionPromociones(string Contenido, int DesplazamientoMinutos, bool SoloValidar);

/// <param name="Linea">Línea del archivo; 0 si el error es del archivo completo.</param>
public sealed record ErrorImportacionCentral(int Linea, string Mensaje);

/// <param name="Publicada">Se publicó el archivo completo; con cualquier error no se publica ninguna línea.</param>
public sealed record ResultadoImportacionPromociones(int Leidas, int Nuevas, int Actualizadas, bool Publicada, IReadOnlyList<ErrorImportacionCentral> Errores);

/// <param name="Momento">Fecha y hora local de la venta simulada: los días y horas de las ofertas son locales.</param>
public sealed record SolicitudSimulacionPromociones(string ArticuloCodigo, decimal Cantidad, int SucursalId, DateTimeOffset Momento, bool ConFidelidad);

/// <param name="Motivo">Por qué no aplica; nulo si aplica.</param>
public sealed record DatosCandidataPromocion(int Id, string Codigo, string Nombre, string Oferta, bool Aplica, string? Motivo, decimal Descuento);

/// <param name="BrutoMayor">Importe al precio por mayor, si la cantidad lo alcanza.</param>
/// <param name="GanadoraId">La oferta que aplicaría la caja; nula si ninguna mejora el precio.</param>
public sealed record ResultadoSimulacionPromociones(
    string Articulo,
    decimal Cantidad,
    decimal PrecioDetalle,
    decimal BrutoDetalle,
    decimal? BrutoMayor,
    IReadOnlyList<DatosCandidataPromocion> Candidatas,
    int? GanadoraId,
    decimal Total,
    string Explicacion);
