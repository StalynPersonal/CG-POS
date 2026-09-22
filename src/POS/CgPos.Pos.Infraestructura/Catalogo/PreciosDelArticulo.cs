using CgPos.Dominio.Catalogo;
using CgPos.Pos.Infraestructura.Persistencia;
using CgPos.Pos.Infraestructura.Persistencia.Configuraciones;

namespace CgPos.Pos.Infraestructura.Catalogo;

/// <summary>
/// Los precios viven en el propio artículo, igual que en el Central, y son columnas del mapeo (no propiedades del dominio,
/// que no conoce listas de precio). Aquí se leen y se escriben en un solo lugar para no repetir el nombre de la columna.
/// </summary>
internal static class PreciosDelArticulo
{
    /// <summary>Guarda los precios que llegan del Central o de una importación. Devuelve si alguno cambió.</summary>
    public static bool Establecer(ContextoDatosPos contexto, Articulo articulo, decimal precioDetalle, decimal? precioMayor, DateTimeOffset vigenteDesde)
    {
        var entrada = contexto.Entry(articulo);
        var detalle = entrada.Property<decimal>(ArticuloConfiguracion.PrecioDetalle);
        var mayor = entrada.Property<decimal?>(ArticuloConfiguracion.PrecioMayor);
        var desde = entrada.Property<DateTimeOffset?>(ArticuloConfiguracion.PreciosVigentesDesde);

        var cambio = detalle.CurrentValue != precioDetalle || mayor.CurrentValue != precioMayor;
        if (!cambio)
            return false;

        detalle.CurrentValue = precioDetalle;
        mayor.CurrentValue = precioMayor;
        desde.CurrentValue = vigenteDesde;
        return true;
    }

    /// <summary>Los precios del artículo tal como están guardados; nulo el que venga en cero (artículo sin precio).</summary>
    public static PreciosVigentes Leer(ContextoDatosPos contexto, Articulo articulo)
    {
        var entrada = contexto.Entry(articulo);
        var detalle = entrada.Property<decimal>(ArticuloConfiguracion.PrecioDetalle).CurrentValue;
        var mayor = entrada.Property<decimal?>(ArticuloConfiguracion.PrecioMayor).CurrentValue;
        return new PreciosVigentes(detalle > 0 ? detalle : null, mayor > 0 ? mayor : null);
    }
}
