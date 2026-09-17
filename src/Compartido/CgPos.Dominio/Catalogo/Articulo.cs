using CgPos.Dominio.Comun;

namespace CgPos.Dominio.Catalogo;

public enum TipoArticulo
{
    Normal,

    /// <summary>Se vende por peso tomado de la balanza (RF-19).</summary>
    Pesado,

    /// <summary>Requiere capturar el serial al venderlo (RF-17).</summary>
    Serializado,

    /// <summary>Combo o kit creado en SAP B1; no aplica precio por mayor (RN-04).</summary>
    ComboKit,
}

public enum TipoCodigoArticulo
{
    Barras,
    Proveedor,
}

/// <summary>Artículo del maestro (RF-178). Los precios viven en <see cref="PrecioArticulo"/> con su vigencia.</summary>
public sealed class Articulo : Entidad
{
    public const int LargoMaximoCodigo = 30;
    public const int LargoMaximoDescripcion = 200;
    public const int LargoMaximoReferencia = 50;
    public const int LargoMaximoRutaImagen = 260;

    private readonly List<CodigoArticulo> _codigos = [];

    private Articulo()
    {
    }

    /// <summary>Código interno del artículo (el de SAP B1).</summary>
    public string Codigo { get; private set; } = string.Empty;

    public string Descripcion { get; private set; } = string.Empty;
    public string? Referencia { get; private set; }
    public int DepartamentoId { get; private set; }

    /// <summary>Categoría dentro del departamento. El Central la exige al publicar; es nula solo en artículos cargados antes de existir las categorías.</summary>
    public int? CategoriaId { get; private set; }

    /// <summary>Marca; opcional (hay artículos genéricos sin marca).</summary>
    public int? MarcaId { get; private set; }
    public int UnidadMedidaId { get; private set; }
    public int ImpuestoId { get; private set; }
    public TipoArticulo Tipo { get; private set; }

    /// <summary>Costo unitario sin impuesto.</summary>
    public decimal? Costo { get; private set; }

    /// <summary>Precio mínimo de venta con impuesto (RF-189).</summary>
    public decimal? PrecioMinimo { get; private set; }

    /// <summary>Cantidad a partir de la cual se aplica automáticamente el precio por mayor (RF-187).</summary>
    public decimal? CantidadMinimaMayor { get; private set; }

    /// <summary>Peso del empaque que se descuenta del peso leído en la balanza, en la unidad del artículo (RF-196).</summary>
    public decimal? Tara { get; private set; }

    public string? RutaImagen { get; private set; }

    /// <summary>El artículo es un servicio (instalación, transporte…) y no un bien; se informa así en el e-CF.</summary>
    public bool EsServicio { get; private set; }

    /// <summary>Se muestra en el catálogo visual de la caja (mosaicos).</summary>
    public bool MostrarEnCatalogo { get; private set; }

    /// <summary>Solo los artículos marcados para el POS se venden en caja (RF-3).</summary>
    public bool VentaEnPos { get; private set; } = true;

    public bool Activo { get; private set; } = true;

    public IReadOnlyCollection<CodigoArticulo> Codigos => _codigos;

    /// <summary>Los combos y kits no aplican lista por mayor (RN-04).</summary>
    public bool AplicaPrecioMayor => Tipo != TipoArticulo.ComboKit;

    public static Articulo Crear(string codigo, string descripcion, int departamentoId, int unidadMedidaId, int impuestoId,
        TipoArticulo tipo = TipoArticulo.Normal)
    {
        var articulo = new Articulo
        {
            Codigo = Validar.Texto(codigo, "Código de artículo", LargoMaximoCodigo),
        };
        articulo.ActualizarDatos(descripcion, null, departamentoId, unidadMedidaId, impuestoId, tipo);
        return articulo;
    }

    public void ActualizarDatos(string descripcion, string? referencia, int departamentoId, int unidadMedidaId, int impuestoId, TipoArticulo tipo)
    {
        if (!Enum.IsDefined(tipo))
            throw new ArgumentOutOfRangeException(nameof(tipo), tipo, "Tipo de artículo no válido.");

        Descripcion = Validar.Texto(descripcion, "Descripción", LargoMaximoDescripcion);
        Referencia = Validar.TextoOpcional(referencia, "Referencia", LargoMaximoReferencia);
        DepartamentoId = Validar.Id(departamentoId, "Departamento");
        UnidadMedidaId = Validar.Id(unidadMedidaId, "Unidad de medida");
        ImpuestoId = Validar.Id(impuestoId, "Impuesto");
        Tipo = tipo;
    }

    public void ConfigurarPrecios(decimal? costo, decimal? precioMinimo, decimal? cantidadMinimaMayor)
    {
        if (costo < 0)
            throw new ArgumentOutOfRangeException(nameof(costo), costo, "El costo no puede ser negativo.");
        if (precioMinimo < 0)
            throw new ArgumentOutOfRangeException(nameof(precioMinimo), precioMinimo, "El precio mínimo no puede ser negativo.");
        if (cantidadMinimaMayor <= 0)
            throw new ArgumentOutOfRangeException(nameof(cantidadMinimaMayor), cantidadMinimaMayor, "La cantidad mínima para precio por mayor debe ser mayor que cero.");

        Costo = costo;
        PrecioMinimo = precioMinimo;
        CantidadMinimaMayor = cantidadMinimaMayor;
    }

    public void ConfigurarTara(decimal? tara)
    {
        if (tara < 0)
            throw new ArgumentOutOfRangeException(nameof(tara), tara, "El peso del empaque no puede ser negativo.");

        Tara = tara is 0m ? null : tara;
    }

    /// <summary>La categoría debe ser del departamento del artículo: lo valida quien conoce las categorías (el Central al publicar).</summary>
    public void Clasificar(int? categoriaId, int? marcaId)
    {
        CategoriaId = categoriaId == 0 ? null : categoriaId;
        MarcaId = marcaId == 0 ? null : marcaId;
    }

    public void ConfigurarNaturaleza(bool esServicio) => EsServicio = esServicio;

    public void ConfigurarPresentacion(string? rutaImagen, bool mostrarEnCatalogo, bool ventaEnPos)
    {
        RutaImagen = Validar.TextoOpcional(rutaImagen, "Ruta de imagen", LargoMaximoRutaImagen);
        MostrarEnCatalogo = mostrarEnCatalogo;
        VentaEnPos = ventaEnPos;
    }

    /// <summary>Agrega un código de barras o de proveedor (RF-179). Repetir el mismo código no lo duplica.</summary>
    public void AgregarCodigo(string codigo, TipoCodigoArticulo tipo)
    {
        var limpio = Validar.Texto(codigo, "Código", LargoMaximoCodigo);
        var existente = _codigos.FirstOrDefault(c => c.Codigo == limpio);
        if (existente is not null)
        {
            existente.CambiarTipo(tipo);
            return;
        }

        _codigos.Add(new CodigoArticulo(Id, limpio, tipo));
    }

    public void QuitarCodigo(string codigo) => _codigos.RemoveAll(c => c.Codigo == codigo.Trim());

    /// <summary>Deja exactamente los códigos indicados (usado al recibir el maestro completo).</summary>
    public void ReemplazarCodigos(IEnumerable<(string Codigo, TipoCodigoArticulo Tipo)> codigos)
    {
        var deseados = codigos
            .Select(c => (Codigo: Validar.Texto(c.Codigo, "Código", LargoMaximoCodigo), c.Tipo))
            .GroupBy(c => c.Codigo)
            .ToDictionary(g => g.Key, g => g.Last().Tipo);

        _codigos.RemoveAll(c => !deseados.ContainsKey(c.Codigo));
        foreach (var (codigo, tipo) in deseados)
            AgregarCodigo(codigo, tipo);
    }

    public void Activar() => Activo = true;

    public void Desactivar() => Activo = false;
}

public sealed class CodigoArticulo
{
    private CodigoArticulo()
    {
    }

    internal CodigoArticulo(int articuloId, string codigo, TipoCodigoArticulo tipo)
    {
        ArticuloId = articuloId;
        Codigo = codigo;
        Tipo = tipo;
    }

    public int ArticuloId { get; private set; }

    /// <summary>Código de barras o de proveedor, único entre todos los artículos.</summary>
    public string Codigo { get; private set; } = string.Empty;

    public TipoCodigoArticulo Tipo { get; private set; }

    internal void CambiarTipo(TipoCodigoArticulo tipo) => Tipo = tipo;
}
