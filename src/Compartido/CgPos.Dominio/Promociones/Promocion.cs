using CgPos.Dominio.Comun;

namespace CgPos.Dominio.Promociones;

public enum TipoPromocion
{
    /// <summary>Porcentaje de descuento sobre el precio de detalle.</summary>
    Porcentaje,

    /// <summary>Monto fijo de descuento por unidad.</summary>
    MontoPorUnidad,

    /// <summary>Precio especial por unidad.</summary>
    PrecioEspecial,

    /// <summary>Lleva X y paga Y (2x1, 3x1, 3x2…).</summary>
    LlevaPaga,

    /// <summary>Desde cierta cantidad, cada unidad sale a un precio (RF-66).</summary>
    PrecioPorCantidad,
}

[Flags]
public enum DiasSemana
{
    Ninguno = 0,
    Domingo = 1,
    Lunes = 2,
    Martes = 4,
    Miercoles = 8,
    Jueves = 16,
    Viernes = 32,
    Sabado = 64,
    Todos = 127,
}

/// <summary>
/// Oferta creada en el Central (RF-59) para artículos o departamentos (RF-58), con vigencia por fechas, días, horas y sucursales
/// (RF-62, RF-65, RF-206). Baja a la caja antes de su inicio y se activa sola aunque no haya red (RF-208).
/// </summary>
public sealed class Promocion : Entidad
{
    public const int LargoMaximoCodigo = 30;
    public const int LargoMaximoNombre = 150;

    // No son readonly: se reemplazan completas para que EF detecte el cambio al guardarlas como texto.
    private List<Guid> _articulos = [];
    private List<Guid> _departamentos = [];
    private List<Guid> _categorias = [];
    private List<Guid> _marcas = [];
    private List<Guid> _sucursales = [];

    private Promocion()
    {
    }

    public string Codigo { get; private set; } = string.Empty;
    public string Nombre { get; private set; } = string.Empty;
    public TipoPromocion Tipo { get; private set; }

    /// <summary>Porcentaje, monto por unidad o precio especial (con ITBIS), según el tipo. No se usa en "lleva X paga Y".</summary>
    public decimal Valor { get; private set; }

    public int? CantidadLleva { get; private set; }
    public int? CantidadPaga { get; private set; }
    public decimal? CantidadMinima { get; private set; }

    /// <summary>Máximo de unidades por compra que reciben la oferta (RF-71).</summary>
    public decimal? LimitePorCliente { get; private set; }

    public DateTimeOffset VigenteDesde { get; private set; }
    public DateTimeOffset VigenteHasta { get; private set; }
    public DiasSemana Dias { get; private set; } = DiasSemana.Todos;
    public TimeOnly? HoraDesde { get; private set; }
    public TimeOnly? HoraHasta { get; private set; }

    /// <summary>Solo para clientes identificados en el programa de fidelidad (RF-207).</summary>
    public bool SoloFidelidad { get; private set; }

    public bool Activa { get; private set; } = true;

    /// <summary>Artículos incluidos. Si artículos, departamentos, categorías y marcas están vacíos, no aplica a nada.</summary>
    public IReadOnlyCollection<Guid> Articulos => _articulos;

    public IReadOnlyCollection<Guid> Departamentos => _departamentos;

    public IReadOnlyCollection<Guid> Categorias => _categorias;

    public IReadOnlyCollection<Guid> Marcas => _marcas;

    /// <summary>Sucursales donde aplica; vacío = todas (RF-60).</summary>
    public IReadOnlyCollection<Guid> Sucursales => _sucursales;

    public static Promocion Crear(string codigo, string nombre, TipoPromocion tipo, decimal valor, DateTimeOffset vigenteDesde, DateTimeOffset vigenteHasta, Guid? id = null)
    {
        var promocion = new Promocion
        {
            Id = id ?? Guid.CreateVersion7(),
            Codigo = Validar.Texto(codigo, "Código de promoción", LargoMaximoCodigo).ToUpperInvariant(),
        };
        promocion.Actualizar(nombre, tipo, valor, vigenteDesde, vigenteHasta);
        return promocion;
    }

    public void Actualizar(string nombre, TipoPromocion tipo, decimal valor, DateTimeOffset vigenteDesde, DateTimeOffset vigenteHasta)
    {
        if (!Enum.IsDefined(tipo))
            throw new ArgumentOutOfRangeException(nameof(tipo), tipo, "Tipo de promoción no válido.");
        if (vigenteHasta < vigenteDesde)
            throw new ArgumentException("La fecha final de la promoción no puede ser anterior a la inicial.", nameof(vigenteHasta));

        var valorValido = tipo switch
        {
            TipoPromocion.Porcentaje => valor is > 0 and <= 100,
            TipoPromocion.LlevaPaga => valor >= 0,
            _ => valor > 0,
        };
        if (!valorValido)
            throw new ArgumentOutOfRangeException(nameof(valor), valor, "El valor de la promoción no es válido para su tipo.");

        Nombre = Validar.Texto(nombre, "Nombre de promoción", LargoMaximoNombre);
        Tipo = tipo;
        Valor = valor;
        VigenteDesde = vigenteDesde;
        VigenteHasta = vigenteHasta;
    }

    public void ConfigurarCantidades(int? lleva, int? paga, decimal? minima, decimal? limitePorCliente)
    {
        if (Tipo == TipoPromocion.LlevaPaga && (lleva is not > 1 || paga is not >= 1 || paga >= lleva))
            throw new ArgumentException("\"Lleva X paga Y\" requiere X mayor que Y, y Y de al menos 1.");
        if (Tipo == TipoPromocion.PrecioPorCantidad && minima is not > 0)
            throw new ArgumentException("La promoción por cantidad requiere la cantidad mínima.");
        if (limitePorCliente <= 0)
            throw new ArgumentOutOfRangeException(nameof(limitePorCliente), limitePorCliente, "El límite por cliente debe ser mayor que cero.");

        CantidadLleva = Tipo == TipoPromocion.LlevaPaga ? lleva : null;
        CantidadPaga = Tipo == TipoPromocion.LlevaPaga ? paga : null;
        CantidadMinima = Tipo == TipoPromocion.PrecioPorCantidad ? minima : null;
        LimitePorCliente = limitePorCliente;
    }

    /// <summary>Días de la semana y rango de horas (el rango puede cruzar la medianoche).</summary>
    public void Programar(DiasSemana dias, TimeOnly? horaDesde, TimeOnly? horaHasta)
    {
        if ((dias & DiasSemana.Todos) == DiasSemana.Ninguno)
            throw new ArgumentException("La promoción debe aplicar al menos un día de la semana.", nameof(dias));
        if (horaDesde is null != horaHasta is null)
            throw new ArgumentException("Indique hora inicial y final, o ninguna.");
        if (horaDesde is not null && horaDesde == horaHasta)
            throw new ArgumentException("La hora inicial y la final no pueden ser iguales.");

        Dias = dias & DiasSemana.Todos;
        HoraDesde = horaDesde;
        HoraHasta = horaHasta;
    }

    public void AsignarAlcance(IEnumerable<Guid>? articulos, IEnumerable<Guid>? departamentos, IEnumerable<Guid>? sucursales,
        IEnumerable<Guid>? categorias = null, IEnumerable<Guid>? marcas = null)
    {
        _articulos = Limpiar(articulos);
        _departamentos = Limpiar(departamentos);
        _categorias = Limpiar(categorias);
        _marcas = Limpiar(marcas);
        _sucursales = Limpiar(sucursales);
    }

    public void ConfigurarFidelidad(bool soloFidelidad) => SoloFidelidad = soloFidelidad;

    public void Activar() => Activa = true;

    public void Desactivar() => Activa = false;

    /// <param name="ahoraLocal">Fecha y hora local de la caja: los días y horas de la oferta son locales.</param>
    public bool EstaVigente(Guid sucursalId, DateTimeOffset ahoraLocal)
    {
        if (!Activa || ahoraLocal < VigenteDesde || ahoraLocal > VigenteHasta)
            return false;
        if ((Dias & DiaDe(ahoraLocal.DayOfWeek)) == DiasSemana.Ninguno)
            return false;

        if (HoraDesde is { } desde && HoraHasta is { } hasta)
        {
            var hora = TimeOnly.FromTimeSpan(ahoraLocal.TimeOfDay);
            var enRango = desde < hasta ? hora >= desde && hora < hasta : hora >= desde || hora < hasta;
            if (!enRango)
                return false;
        }

        return _sucursales.Count == 0 || _sucursales.Contains(sucursalId);
    }

    /// <summary>La oferta alcanza al artículo si lo incluye directamente o por su departamento, su categoría o su marca.</summary>
    public bool AplicaA(Guid articuloId, Guid departamentoId, Guid? categoriaId = null, Guid? marcaId = null) =>
        _articulos.Contains(articuloId) || _departamentos.Contains(departamentoId)
        || (categoriaId is { } categoria && _categorias.Contains(categoria))
        || (marcaId is { } marca && _marcas.Contains(marca));

    /// <summary>Texto corto para la columna Promo (RF-143).</summary>
    public string DescripcionCorta => Tipo switch
    {
        TipoPromocion.Porcentaje => $"-{Valor:0.##}%",
        TipoPromocion.MontoPorUnidad => $"-{Valor:N2} c/u",
        TipoPromocion.PrecioEspecial => $"Oferta {Valor:N2}",
        TipoPromocion.LlevaPaga => $"{CantidadLleva}x{CantidadPaga}",
        TipoPromocion.PrecioPorCantidad => $"{CantidadMinima:0.###}+ a {Valor:N2}",
        _ => Nombre,
    };

    private static DiasSemana DiaDe(DayOfWeek dia) => (DiasSemana)(1 << (int)dia);

    private static List<Guid> Limpiar(IEnumerable<Guid>? origen) =>
        origen is null ? [] : origen.Where(id => id != Guid.Empty).Distinct().ToList();
}

public static class MotorPromociones
{
    /// <summary>
    /// Descuento total (con ITBIS, a 2 decimales) que da la promoción a un grupo de unidades del mismo artículo.
    /// </summary>
    /// <param name="cantidad">Unidades del artículo en toda la venta (suma de sus líneas activas).</param>
    /// <param name="precioUnitario">Precio de detalle por unidad, con ITBIS.</param>
    /// <param name="importeBruto">Importe del grupo antes de descuentos; el descuento nunca lo supera.</param>
    public static decimal CalcularDescuento(Promocion promocion, decimal cantidad, decimal precioUnitario, decimal importeBruto)
    {
        ArgumentNullException.ThrowIfNull(promocion);
        if (cantidad <= 0 || importeBruto <= 0)
            return 0m;

        var conOferta = promocion.LimitePorCliente is { } limite ? Math.Min(cantidad, limite) : cantidad;

        var descuento = promocion.Tipo switch
        {
            TipoPromocion.Porcentaje => importeBruto * (conOferta / cantidad) * promocion.Valor / 100m,
            TipoPromocion.MontoPorUnidad => Math.Min(promocion.Valor, precioUnitario) * conOferta,
            TipoPromocion.PrecioEspecial => Math.Max(0m, precioUnitario - promocion.Valor) * conOferta,
            TipoPromocion.LlevaPaga when promocion is { CantidadLleva: { } lleva, CantidadPaga: { } paga } =>
                decimal.Floor(conOferta / lleva) * (lleva - paga) * precioUnitario,
            TipoPromocion.PrecioPorCantidad when promocion.CantidadMinima is { } minima && cantidad >= minima =>
                Math.Max(0m, precioUnitario - promocion.Valor) * conOferta,
            _ => 0m,
        };

        return decimal.Round(Math.Clamp(descuento, 0m, importeBruto), 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>Reparte un monto entre partes proporcionales a sus bases, al centavo y sin perder ni sobrar (mayor residuo).</summary>
    public static decimal[] Prorratear(decimal monto, IReadOnlyList<decimal> bases)
    {
        ArgumentNullException.ThrowIfNull(bases);
        var resultado = new decimal[bases.Count];
        var totalBases = bases.Sum();
        if (bases.Count == 0 || totalBases <= 0 || monto <= 0)
            return resultado;

        var centavos = (long)decimal.Round(monto * 100m, 0, MidpointRounding.AwayFromZero);
        var exactos = bases.Select(b => b / totalBases * centavos).ToArray();
        var asignados = exactos.Select(decimal.Floor).ToArray();
        var sobrante = centavos - (long)asignados.Sum();

        foreach (var indice in exactos
                     .Select((valor, indice) => (Residuo: valor - decimal.Floor(valor), Indice: indice))
                     .OrderByDescending(x => x.Residuo)
                     .ThenBy(x => x.Indice)
                     .Take((int)sobrante)
                     .Select(x => x.Indice))
            asignados[indice]++;

        for (var i = 0; i < resultado.Length; i++)
            resultado[i] = asignados[i] / 100m;

        return resultado;
    }
}
