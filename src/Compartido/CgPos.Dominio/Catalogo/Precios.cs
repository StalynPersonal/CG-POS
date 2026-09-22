namespace CgPos.Dominio.Catalogo;

public enum ListaPrecio
{
    Detalle,
    Mayor,
}

/// <summary>
/// Precios del artículo: los que rigen ahora. Viven en el propio artículo, igual en la caja y en el Central; el histórico de
/// cambios queda en la auditoría del Central, que es donde se cambian.
/// </summary>
public sealed record PreciosVigentes(decimal? Detalle, decimal? Mayor);

public enum SeleccionListaPrecio
{
    /// <summary>Detalle, o mayor si la cantidad alcanza el mínimo del artículo.</summary>
    Automatica,

    /// <summary>Forzar precio detalle.</summary>
    Detalle,

    /// <summary>Aplicar precio por mayor por decisión del cajero o del cliente; requiere autorización (RF-188, RF-77).</summary>
    Mayor,
}

public enum MotivoPrecio
{
    PrecioDetalle,
    MayorPorCantidad,
    MayorManual,
    MayorNoAplicaComboKit,
    SinPrecioMayor,

    /// <summary>El precio viene pactado en una cotización del Central: no se recalcula ni recibe ofertas.</summary>
    PrecioCotizado,
}

public sealed record PrecioDeterminado(ListaPrecio Lista, decimal PrecioUnitario, MotivoPrecio Motivo, bool RequiereAutorizacion);

/// <summary>Reglas de precio detalle/mayor (RN-01 a RN-04) y de precio mínimo (RF-189).</summary>
public static class ReglasPrecio
{
    public static PrecioDeterminado Determinar(Articulo articulo, PreciosVigentes precios, decimal cantidad, SeleccionListaPrecio seleccion)
    {
        ArgumentNullException.ThrowIfNull(articulo);
        return Determinar(articulo.Codigo, articulo.Tipo, articulo.CantidadMinimaMayor, precios, cantidad, seleccion);
    }

    /// <summary>Misma regla, con los datos del artículo ya copiados (por ejemplo, en una línea de venta).</summary>
    public static PrecioDeterminado Determinar(string codigoArticulo, TipoArticulo tipo, decimal? cantidadMinimaMayor, PreciosVigentes precios, decimal cantidad, SeleccionListaPrecio seleccion)
    {
        ArgumentNullException.ThrowIfNull(precios);

        if (precios.Detalle is not { } detalle)
            throw new InvalidOperationException($"El artículo {codigoArticulo} no tiene precio detalle vigente.");

        if (seleccion == SeleccionListaPrecio.Detalle)
            return new(ListaPrecio.Detalle, detalle, MotivoPrecio.PrecioDetalle, false);

        if (tipo == TipoArticulo.ComboKit)
            return new(ListaPrecio.Detalle, detalle, seleccion == SeleccionListaPrecio.Mayor ? MotivoPrecio.MayorNoAplicaComboKit : MotivoPrecio.PrecioDetalle, false);

        if (precios.Mayor is not { } mayor)
            return new(ListaPrecio.Detalle, detalle, seleccion == SeleccionListaPrecio.Mayor ? MotivoPrecio.SinPrecioMayor : MotivoPrecio.PrecioDetalle, false);

        if (seleccion == SeleccionListaPrecio.Mayor)
            return new(ListaPrecio.Mayor, mayor, MotivoPrecio.MayorManual, true);

        // Suposición documentada: el cambio automático por cantidad (RF-187) no pide autorización; la aplicación manual sí (RF-188).
        return cantidadMinimaMayor is { } minima && cantidad >= minima
            ? new(ListaPrecio.Mayor, mayor, MotivoPrecio.MayorPorCantidad, false)
            : new(ListaPrecio.Detalle, detalle, MotivoPrecio.PrecioDetalle, false);
    }

    /// <summary>
    /// Verdadero si el precio final queda por debajo del precio mínimo del artículo o de su costo. Todo va sin impuesto,
    /// como los precios. Venderlo así requiere autorización (RF-189).
    /// </summary>
    public static bool EstaBajoMinimo(Articulo articulo, decimal precioUnitario)
    {
        ArgumentNullException.ThrowIfNull(articulo);

        if (articulo.PrecioMinimo is { } minimo && precioUnitario < minimo)
            return true;

        return articulo.Costo is { } costo && precioUnitario < costo;
    }
}
