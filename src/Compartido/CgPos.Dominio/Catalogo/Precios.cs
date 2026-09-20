using CgPos.Dominio.Comun;

namespace CgPos.Dominio.Catalogo;

public enum ListaPrecio
{
    Detalle,
    Mayor,
}

/// <summary>
/// Precio de un artículo en una lista, con fecha de vigencia. Cada cambio es un registro nuevo:
/// el conjunto forma la bitácora de precios (RF-190) y el vigente es el más reciente ya iniciado.
/// </summary>
public sealed class PrecioArticulo : Entidad
{
    public const int LargoMaximoOrigen = 50;
    public const int LargoMaximoUsuario = 150;

    private PrecioArticulo()
    {
    }

    public int ArticuloId { get; private set; }
    public ListaPrecio Lista { get; private set; }

    /// <summary>Precio unitario con impuesto incluido.</summary>
    public decimal Precio { get; private set; }

    public DateTimeOffset VigenteDesde { get; private set; }
    public DateTimeOffset RegistradoEn { get; private set; }

    /// <summary>De dónde vino el cambio: "Central", "Carga inicial", "Importación CSV"…</summary>
    public string Origen { get; private set; } = string.Empty;

    public int? UsuarioId { get; private set; }
    public string? UsuarioNombre { get; private set; }

    public static PrecioArticulo Registrar(int articuloId, ListaPrecio lista, decimal precio, DateTimeOffset vigenteDesde,
        DateTimeOffset registradoEn, string origen, int? usuarioId = null, string? usuarioNombre = null)
    {
        if (precio <= 0)
            throw new ArgumentOutOfRangeException(nameof(precio), precio, "El precio debe ser mayor que cero.");
        if (!Enum.IsDefined(lista))
            throw new ArgumentOutOfRangeException(nameof(lista), lista, "Lista de precio no válida.");

        return new PrecioArticulo
        {
            ArticuloId = Validar.Id(articuloId, "Artículo"),
            Lista = lista,
            Precio = precio,
            VigenteDesde = vigenteDesde,
            RegistradoEn = registradoEn,
            Origen = Validar.Texto(origen, "Origen", LargoMaximoOrigen),
            UsuarioId = usuarioId,
            UsuarioNombre = Validar.TextoOpcional(usuarioNombre, "Usuario", LargoMaximoUsuario),
        };
    }
}

/// <summary>Precios vigentes de un artículo en un momento dado.</summary>
public sealed record PreciosVigentes(decimal? Detalle, decimal? Mayor)
{
    public static PreciosVigentes Resolver(IEnumerable<PrecioArticulo> historial, DateTimeOffset ahora)
    {
        decimal? Vigente(ListaPrecio lista) => historial
            .Where(p => p.Lista == lista && p.VigenteDesde <= ahora)
            .OrderByDescending(p => p.VigenteDesde)
            .ThenByDescending(p => p.RegistradoEn)
            .Select(p => (decimal?)p.Precio)
            .FirstOrDefault();

        return new PreciosVigentes(Vigente(ListaPrecio.Detalle), Vigente(ListaPrecio.Mayor));
    }
}

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
    /// Verdadero si el precio final (con impuesto) queda por debajo del precio mínimo del artículo,
    /// o si su base sin impuesto queda por debajo del costo. Venderlo así requiere autorización (RF-189).
    /// </summary>
    public static bool EstaBajoMinimo(Articulo articulo, Impuesto impuesto, decimal precioUnitarioConImpuesto)
    {
        ArgumentNullException.ThrowIfNull(articulo);
        ArgumentNullException.ThrowIfNull(impuesto);

        if (articulo.PrecioMinimo is { } minimo && precioUnitarioConImpuesto < minimo)
            return true;

        return articulo.Costo is { } costo && impuesto.BaseDesdePrecioConImpuesto(precioUnitarioConImpuesto) < costo;
    }
}
