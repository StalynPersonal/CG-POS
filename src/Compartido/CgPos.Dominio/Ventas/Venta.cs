using CgPos.Dominio.Catalogo;
using CgPos.Dominio.Comun;

namespace CgPos.Dominio.Ventas;

public enum EstadoVenta
{
    EnCurso,
    EnEspera,
    Anulada,
    Cobrada,
}

public enum CodigoErrorVenta
{
    VentaNoEditable,
    SinPrecio,
    RequiereBalanza,
    CantidadInvalida,
    LineaNoEncontrada,
    MotivoRequerido,
}

/// <summary>Una regla de la venta impidió la operación; el código permite a la pantalla reaccionar.</summary>
public sealed class ReglaVentaExcepcion(CodigoErrorVenta codigo, string mensaje) : InvalidOperationException(mensaje)
{
    public CodigoErrorVenta Codigo { get; } = codigo;
}

/// <summary>Datos del artículo tal como se leyó en caja, con precios vigentes y lectura de balanza.</summary>
public sealed record ArticuloParaVenta(
    Guid ArticuloId,
    string CodigoInterno,
    string CodigoLeido,
    string Descripcion,
    TipoArticulo Tipo,
    Guid FamiliaId,
    bool PermiteDescuentoManual,
    string UnidadMedidaCodigo,
    bool PermiteDecimales,
    int DecimalesCantidad,
    Guid ImpuestoId,
    decimal PorcentajeImpuesto,
    int IndicadorFacturacion,
    decimal? PrecioDetalle,
    decimal? PrecioMayor,
    decimal? CantidadMinimaMayor,
    decimal? PrecioMinimo,
    decimal? PesoLeido,
    decimal? PrecioLeido);

public sealed record DesgloseImpuesto(decimal Porcentaje, int IndicadorFacturacion, decimal Base, decimal Impuesto, decimal Total);

public sealed record TotalesVenta(
    decimal Subtotal,
    decimal Impuesto,
    decimal Total,
    int CantidadLineas,
    decimal CantidadArticulos,
    IReadOnlyList<DesgloseImpuesto> Desglose);

/// <summary>
/// Transacción de venta en la caja. Se guarda en cada cambio para poder recuperarla tras un corte (RF-195).
/// Su número (sucursal-caja-secuencia) es único e independiente del NCF (RF-193).
/// </summary>
public sealed class Venta : Entidad
{
    public const int LargoMaximoNumero = 40;
    public const int LargoMaximoMotivo = 500;
    public const int LargoMaximoUsuario = 150;
    public const decimal CantidadMaxima = 99_999m;

    private readonly List<LineaVenta> _lineas = [];

    private Venta()
    {
    }

    public string NumeroTransaccion { get; private set; } = string.Empty;
    public long Secuencia { get; private set; }
    public Guid SucursalId { get; private set; }
    public Guid CajaId { get; private set; }
    public Guid TurnoId { get; private set; }
    public Guid UsuarioId { get; private set; }
    public string UsuarioNombre { get; private set; } = string.Empty;
    public EstadoVenta Estado { get; private set; }
    public DateTimeOffset IniciadaEn { get; private set; }
    public DateTimeOffset ActualizadaEn { get; private set; }
    public DateTimeOffset? AnuladaEn { get; private set; }
    public string? MotivoAnulacion { get; private set; }
    public Guid? AnuladaPorId { get; private set; }
    public string? AnuladaPorNombre { get; private set; }

    public IReadOnlyCollection<LineaVenta> Lineas => _lineas;

    public static string FormatearNumero(string codigoSucursal, string codigoCaja, long secuencia) =>
        $"{codigoSucursal.Trim()}-{codigoCaja.Trim()}-{secuencia:D8}";

    public static Venta Iniciar(Guid sucursalId, string codigoSucursal, Guid cajaId, string codigoCaja, Guid turnoId, long secuencia,
        Guid usuarioId, string usuarioNombre, DateTimeOffset ahora)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(secuencia, 1);

        return new Venta
        {
            Id = Guid.CreateVersion7(),
            NumeroTransaccion = Validar.Texto(FormatearNumero(codigoSucursal, codigoCaja, secuencia), "Número de transacción", LargoMaximoNumero),
            Secuencia = secuencia,
            SucursalId = Validar.Id(sucursalId, "Sucursal"),
            CajaId = Validar.Id(cajaId, "Caja"),
            TurnoId = Validar.Id(turnoId, "Turno"),
            UsuarioId = Validar.Id(usuarioId, "Usuario"),
            UsuarioNombre = Validar.Texto(usuarioNombre, "Usuario", LargoMaximoUsuario),
            Estado = EstadoVenta.EnCurso,
            IniciadaEn = ahora,
            ActualizadaEn = ahora,
        };
    }

    /// <summary>
    /// Agrega un artículo. Los pesados toman la cantidad de la etiqueta o balanza (RF-19, RF-20);
    /// los demás usan la cantidad indicada o 1, redondeada a los decimales de su unidad (RF-198).
    /// </summary>
    public LineaVenta AgregarArticulo(ArticuloParaVenta articulo, decimal? cantidadIndicada, DateTimeOffset ahora)
    {
        ArgumentNullException.ThrowIfNull(articulo);
        AsegurarEditable();

        if (articulo.PrecioDetalle is not { } precioDetalle)
            throw new ReglaVentaExcepcion(CodigoErrorVenta.SinPrecio, $"El artículo {articulo.CodigoInterno} no tiene precio vigente.");

        decimal cantidad;
        decimal? importeEtiqueta = null;
        var leidaDeBalanza = false;

        if (articulo.Tipo == TipoArticulo.Pesado)
        {
            if (cantidadIndicada is not null and not 1m)
                throw new ReglaVentaExcepcion(CodigoErrorVenta.CantidadInvalida, "La cantidad de un artículo pesado la da la balanza; no se multiplica.");

            if (articulo.PesoLeido is { } peso)
                cantidad = peso;
            else if (articulo.PrecioLeido is { } precioEtiqueta)
            {
                cantidad = decimal.Round(precioEtiqueta / precioDetalle, 3, MidpointRounding.AwayFromZero);
                importeEtiqueta = precioEtiqueta;
            }
            else
                throw new ReglaVentaExcepcion(CodigoErrorVenta.RequiereBalanza, "Los artículos pesados se registran con la balanza o su etiqueta; el peso no se digita.");

            if (cantidad <= 0)
                throw new ReglaVentaExcepcion(CodigoErrorVenta.CantidadInvalida, "La balanza no reportó un peso válido.");

            leidaDeBalanza = true;
        }
        else
        {
            cantidad = NormalizarCantidad(cantidadIndicada ?? 1m, articulo.PermiteDecimales, articulo.DecimalesCantidad);
        }

        var precio = ReglasPrecio.Determinar(articulo.CodigoInterno, articulo.Tipo, articulo.CantidadMinimaMayor,
            new PreciosVigentes(precioDetalle, articulo.PrecioMayor), cantidad, SeleccionListaPrecio.Automatica);

        var linea = LineaVenta.Crear(Id, SiguienteNumeroLinea(), articulo, cantidad, precio, importeEtiqueta, leidaDeBalanza);
        _lineas.Add(linea);
        ActualizadaEn = ahora;
        return linea;
    }

    /// <summary>Cambia la cantidad de una línea (RF-110) y recalcula el precio por mayor automático.</summary>
    public void CambiarCantidad(int numeroLinea, decimal cantidad, DateTimeOffset ahora)
    {
        AsegurarEditable();
        var linea = LineaActiva(numeroLinea);

        if (linea.LeidaDeBalanza)
            throw new ReglaVentaExcepcion(CodigoErrorVenta.RequiereBalanza, "La cantidad de un artículo pesado viene de la balanza; no se cambia a mano.");

        var normalizada = NormalizarCantidad(cantidad, linea.PermiteDecimales, linea.DecimalesCantidad);
        var precio = ReglasPrecio.Determinar(linea.CodigoInterno, linea.TipoArticulo, linea.CantidadMinimaMayor,
            new PreciosVigentes(linea.PrecioDetalle, linea.PrecioMayor), normalizada, SeleccionListaPrecio.Automatica);

        linea.CambiarCantidad(normalizada, precio);
        ActualizadaEn = ahora;
    }

    /// <summary>
    /// Elimina una línea: queda marcada como anulada y se agrega debajo su reverso en negativo con precio cero (RF-114).
    /// </summary>
    public LineaVenta EliminarLinea(int numeroLinea, DateTimeOffset ahora)
    {
        AsegurarEditable();
        var linea = LineaActiva(numeroLinea);

        linea.MarcarAnulada();
        var reverso = LineaVenta.CrearReverso(Id, SiguienteNumeroLinea(), linea);
        _lineas.Add(reverso);
        ActualizadaEn = ahora;
        return reverso;
    }

    /// <summary>Elimina la última línea activa del código escaneado (RF-21).</summary>
    public LineaVenta EliminarPorCodigo(string codigo, DateTimeOffset ahora)
    {
        var buscado = codigo?.Trim() ?? string.Empty;
        var linea = _lineas
            .Where(l => l.EstaActiva && (l.CodigoLeido == buscado || l.CodigoInterno == buscado))
            .OrderByDescending(l => l.NumeroLinea)
            .FirstOrDefault()
            ?? throw new ReglaVentaExcepcion(CodigoErrorVenta.LineaNoEncontrada, $"No hay una línea activa con el código {buscado}.");

        return EliminarLinea(linea.NumeroLinea, ahora);
    }

    /// <summary>Anula la transacción completa antes de cobrarla (RF-194, RF-146); no consume NCF.</summary>
    public void Anular(string motivo, Guid usuarioId, string usuarioNombre, DateTimeOffset ahora)
    {
        if (Estado is not (EstadoVenta.EnCurso or EstadoVenta.EnEspera))
            throw new ReglaVentaExcepcion(CodigoErrorVenta.VentaNoEditable, $"No se puede anular una venta {Estado}.");
        if (string.IsNullOrWhiteSpace(motivo))
            throw new ReglaVentaExcepcion(CodigoErrorVenta.MotivoRequerido, "Debe indicar el motivo de la anulación.");

        Estado = EstadoVenta.Anulada;
        MotivoAnulacion = Validar.Texto(motivo, "Motivo", LargoMaximoMotivo);
        AnuladaPorId = Validar.Id(usuarioId, "Usuario");
        AnuladaPorNombre = Validar.Texto(usuarioNombre, "Usuario", LargoMaximoUsuario);
        AnuladaEn = ahora;
        ActualizadaEn = ahora;
    }

    /// <summary>
    /// Totales con ITBIS incluido en los precios: por línea se redondea el importe a 2 decimales y se separa la base,
    /// así el ITBIS total coincide con la suma del desglose por línea (RF-183).
    /// </summary>
    public TotalesVenta CalcularTotales()
    {
        var activas = _lineas.Where(l => l.EstaActiva).ToList();

        var desglose = activas
            .Select(l =>
            {
                var importe = l.ImporteConImpuesto;
                var baseImponible = decimal.Round(importe / (1 + l.PorcentajeImpuesto / 100m), 2, MidpointRounding.AwayFromZero);
                return (l.PorcentajeImpuesto, l.IndicadorFacturacion, Base: baseImponible, Impuesto: importe - baseImponible, Total: importe);
            })
            .GroupBy(x => (x.PorcentajeImpuesto, x.IndicadorFacturacion))
            .OrderByDescending(g => g.Key.PorcentajeImpuesto)
            .ThenBy(g => g.Key.IndicadorFacturacion)
            .Select(g => new DesgloseImpuesto(g.Key.PorcentajeImpuesto, g.Key.IndicadorFacturacion, g.Sum(x => x.Base), g.Sum(x => x.Impuesto), g.Sum(x => x.Total)))
            .ToList();

        var cantidadArticulos = activas.Sum(l => l.PermiteDecimales ? 1m : l.Cantidad);

        return new TotalesVenta(
            desglose.Sum(d => d.Base),
            desglose.Sum(d => d.Impuesto),
            desglose.Sum(d => d.Total),
            activas.Count,
            cantidadArticulos,
            desglose);
    }

    private void AsegurarEditable()
    {
        if (Estado != EstadoVenta.EnCurso)
            throw new ReglaVentaExcepcion(CodigoErrorVenta.VentaNoEditable, $"La venta {NumeroTransaccion} no se puede modificar (estado {Estado}).");
    }

    private LineaVenta LineaActiva(int numeroLinea) =>
        _lineas.FirstOrDefault(l => l.NumeroLinea == numeroLinea && l.EstaActiva)
        ?? throw new ReglaVentaExcepcion(CodigoErrorVenta.LineaNoEncontrada, $"La línea {numeroLinea} no existe o ya fue eliminada.");

    private int SiguienteNumeroLinea() => _lineas.Count == 0 ? 1 : _lineas.Max(l => l.NumeroLinea) + 1;

    private static decimal NormalizarCantidad(decimal cantidad, bool permiteDecimales, int decimales)
    {
        var redondeada = decimal.Round(cantidad, permiteDecimales ? decimales : 0, MidpointRounding.AwayFromZero);

        if (!permiteDecimales && cantidad != decimal.Truncate(cantidad))
            throw new ReglaVentaExcepcion(CodigoErrorVenta.CantidadInvalida, "Este artículo se vende por unidades enteras.");
        if (redondeada <= 0)
            throw new ReglaVentaExcepcion(CodigoErrorVenta.CantidadInvalida, "La cantidad debe ser mayor que cero.");
        if (redondeada > CantidadMaxima)
            throw new ReglaVentaExcepcion(CodigoErrorVenta.CantidadInvalida, $"La cantidad no puede superar {CantidadMaxima:N0}.");

        return redondeada;
    }
}

public sealed class LineaVenta : Entidad
{
    private LineaVenta()
    {
    }

    public Guid VentaId { get; private set; }
    public int NumeroLinea { get; private set; }
    public Guid ArticuloId { get; private set; }
    public string CodigoInterno { get; private set; } = string.Empty;

    /// <summary>Código tal como se leyó (barras, proveedor, interno o etiqueta de balanza); al tocarlo se muestra el interno (RF-109).</summary>
    public string CodigoLeido { get; private set; } = string.Empty;

    public string Descripcion { get; private set; } = string.Empty;
    public TipoArticulo TipoArticulo { get; private set; }
    public Guid FamiliaId { get; private set; }
    public bool PermiteDescuentoManual { get; private set; }
    public string UnidadMedidaCodigo { get; private set; } = string.Empty;
    public bool PermiteDecimales { get; private set; }
    public int DecimalesCantidad { get; private set; }
    public Guid ImpuestoId { get; private set; }
    public decimal PorcentajeImpuesto { get; private set; }
    public int IndicadorFacturacion { get; private set; }
    public decimal PrecioDetalle { get; private set; }
    public decimal? PrecioMayor { get; private set; }
    public decimal? CantidadMinimaMayor { get; private set; }
    public decimal? PrecioMinimo { get; private set; }

    public decimal Cantidad { get; private set; }

    /// <summary>Precio unitario con impuesto incluido.</summary>
    public decimal PrecioUnitario { get; private set; }

    public ListaPrecio Lista { get; private set; }
    public MotivoPrecio MotivoPrecio { get; private set; }

    /// <summary>Importe tomado de una etiqueta de balanza con precio embebido; se respeta tal cual.</summary>
    public decimal? ImporteEtiqueta { get; private set; }

    public bool LeidaDeBalanza { get; private set; }

    /// <summary>Línea en negativo que representa la eliminación de otra (RF-114).</summary>
    public bool EsReverso { get; private set; }

    public int? LineaAnuladaNumero { get; private set; }
    public bool Anulada { get; private set; }

    public bool EstaActiva => !Anulada && !EsReverso;

    public decimal ImporteConImpuesto => EsReverso
        ? 0m
        : ImporteEtiqueta ?? decimal.Round(Cantidad * PrecioUnitario, 2, MidpointRounding.AwayFromZero);

    internal static LineaVenta Crear(Guid ventaId, int numeroLinea, ArticuloParaVenta articulo, decimal cantidad, PrecioDeterminado precio,
        decimal? importeEtiqueta, bool leidaDeBalanza) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            VentaId = ventaId,
            NumeroLinea = numeroLinea,
            ArticuloId = articulo.ArticuloId,
            CodigoInterno = articulo.CodigoInterno,
            CodigoLeido = articulo.CodigoLeido,
            Descripcion = articulo.Descripcion,
            TipoArticulo = articulo.Tipo,
            FamiliaId = articulo.FamiliaId,
            PermiteDescuentoManual = articulo.PermiteDescuentoManual,
            UnidadMedidaCodigo = articulo.UnidadMedidaCodigo,
            PermiteDecimales = articulo.PermiteDecimales,
            DecimalesCantidad = articulo.DecimalesCantidad,
            ImpuestoId = articulo.ImpuestoId,
            PorcentajeImpuesto = articulo.PorcentajeImpuesto,
            IndicadorFacturacion = articulo.IndicadorFacturacion,
            PrecioDetalle = articulo.PrecioDetalle!.Value,
            PrecioMayor = articulo.PrecioMayor,
            CantidadMinimaMayor = articulo.CantidadMinimaMayor,
            PrecioMinimo = articulo.PrecioMinimo,
            Cantidad = cantidad,
            PrecioUnitario = precio.PrecioUnitario,
            Lista = precio.Lista,
            MotivoPrecio = precio.Motivo,
            ImporteEtiqueta = importeEtiqueta,
            LeidaDeBalanza = leidaDeBalanza,
        };

    internal static LineaVenta CrearReverso(Guid ventaId, int numeroLinea, LineaVenta original) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            VentaId = ventaId,
            NumeroLinea = numeroLinea,
            ArticuloId = original.ArticuloId,
            CodigoInterno = original.CodigoInterno,
            CodigoLeido = original.CodigoLeido,
            Descripcion = original.Descripcion,
            TipoArticulo = original.TipoArticulo,
            FamiliaId = original.FamiliaId,
            PermiteDescuentoManual = original.PermiteDescuentoManual,
            UnidadMedidaCodigo = original.UnidadMedidaCodigo,
            PermiteDecimales = original.PermiteDecimales,
            DecimalesCantidad = original.DecimalesCantidad,
            ImpuestoId = original.ImpuestoId,
            PorcentajeImpuesto = original.PorcentajeImpuesto,
            IndicadorFacturacion = original.IndicadorFacturacion,
            PrecioDetalle = original.PrecioDetalle,
            PrecioMayor = original.PrecioMayor,
            CantidadMinimaMayor = original.CantidadMinimaMayor,
            PrecioMinimo = original.PrecioMinimo,
            Cantidad = -original.Cantidad,
            PrecioUnitario = 0m,
            Lista = original.Lista,
            MotivoPrecio = original.MotivoPrecio,
            LeidaDeBalanza = original.LeidaDeBalanza,
            EsReverso = true,
            LineaAnuladaNumero = original.NumeroLinea,
        };

    internal void CambiarCantidad(decimal cantidad, PrecioDeterminado precio)
    {
        Cantidad = cantidad;
        PrecioUnitario = precio.PrecioUnitario;
        Lista = precio.Lista;
        MotivoPrecio = precio.Motivo;
    }

    internal void MarcarAnulada() => Anulada = true;
}
