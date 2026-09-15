using CgPos.Contratos.Catalogo;

namespace CgPos.Contratos.Central;

/// <summary>Maestro publicado tal como baja a las cajas, con cuándo y quién lo cambió por última vez.</summary>
public sealed record DatosMaestroCentral<T>(T Dato, DateTimeOffset ModificadoEn, string ModificadoPor);

/// <param name="Total">Registros que coinciden con la búsqueda, sumando todas las páginas.</param>
public sealed record PaginaMaestros<T>(IReadOnlyList<DatosMaestroCentral<T>> Elementos, int Total);

/// <summary>Precios de un artículo (RF-190): detalle, mayor y mínimo con impuesto incluido; el costo sin impuesto.</summary>
/// <param name="VigenteDesde">Desde cuándo rigen el detalle y el mayor; nulo = desde ahora.</param>
public sealed record SolicitudPreciosArticulo(
    decimal PrecioDetalle,
    decimal? PrecioMayor = null,
    decimal? CantidadMinimaMayor = null,
    decimal? PrecioMinimo = null,
    decimal? Costo = null,
    DateTimeOffset? VigenteDesde = null);

/// <param name="Alcance">"General", o la familia o el artículo con su código y nombre.</param>
public sealed record DatosTopeDescuentoCentral(TopeDescuentoCarga Tope, string Alcance, DateTimeOffset ModificadoEn, string ModificadoPor);
