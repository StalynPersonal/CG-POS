using CgPos.Dominio.Catalogo;
using CgPos.Dominio.Comun;
using CgPos.Dominio.Fiscal;

namespace CgPos.Dominio.Devoluciones;

/// <summary>
/// Copia temporal de una factura que la caja le pidió al Central para devolverla (RF-42, Fase 3). Toda nota de crédito se emite
/// contra la factura registrada en el Central —también las de esta misma caja—, porque es el único que sabe cuánto se devolvió
/// ya de cada línea en toda la empresa. No es el histórico: se borra en cuanto la nota se emite, y si quedara alguna por un
/// corte de luz, la siguiente consulta de ese mismo número la reemplaza.
/// </summary>
public sealed class FacturaConsultada : Entidad
{
    public const int LargoMaximoNumero = 30;
    public const int LargoMaximoTexto = 200;
    public const int LargoMaximoCodigo = 50;

    private readonly List<LineaFacturaConsultada> _lineas = [];

    private FacturaConsultada()
    {
    }

    public string Numero { get; private set; } = string.Empty;
    public string? Encf { get; private set; }
    public string SucursalCodigo { get; private set; } = string.Empty;
    public string CajaCodigo { get; private set; } = string.Empty;
    public TipoComprobante TipoComprobante { get; private set; }
    public DateTimeOffset CobradaEn { get; private set; }
    public TipoDocumentoIdentidad? ClienteTipoDocumento { get; private set; }
    public string? ClienteDocumento { get; private set; }
    public string? ClienteNombre { get; private set; }
    public string Moneda { get; private set; } = string.Empty;
    public string SimboloMoneda { get; private set; } = string.Empty;
    public decimal Total { get; private set; }

    /// <summary>Cuándo se le pidió al Central. Sirve para purgar lo que quedó de una devolución que nunca terminó.</summary>
    public DateTimeOffset ConsultadaEn { get; private set; }

    /// <summary>
    /// La venta de esta caja, si la factura la vendió ella. Los datos y lo disponible siguen saliendo del Central; esto solo sirve
    /// para lo que únicamente la caja sabe de su propia venta: los puntos que acumuló y la mercancía pendiente de entregar.
    /// </summary>
    public int? VentaLocalId { get; private set; }

    public void EnlazarVentaLocal(int? ventaId) => VentaLocalId = ventaId;

    public IReadOnlyList<LineaFacturaConsultada> Lineas => _lineas;

    public static FacturaConsultada Crear(string numero, string? encf, string sucursalCodigo, string cajaCodigo, TipoComprobante tipoComprobante,
        DateTimeOffset cobradaEn, TipoDocumentoIdentidad? clienteTipoDocumento, string? clienteDocumento, string? clienteNombre, string moneda,
        string simboloMoneda, decimal total, DateTimeOffset ahora) =>
        new()
        {
            Numero = Validar.Texto(numero, "Número de la factura", LargoMaximoNumero).ToUpperInvariant(),
            Encf = Validar.TextoOpcional(encf, "e-NCF", DocumentoElectronico.LargoEncf)?.ToUpperInvariant(),
            SucursalCodigo = Validar.TextoOpcional(sucursalCodigo, "Sucursal", LargoMaximoCodigo) ?? string.Empty,
            CajaCodigo = Validar.TextoOpcional(cajaCodigo, "Caja", LargoMaximoCodigo) ?? string.Empty,
            TipoComprobante = tipoComprobante,
            CobradaEn = cobradaEn,
            ClienteTipoDocumento = clienteTipoDocumento,
            ClienteDocumento = Validar.TextoOpcional(clienteDocumento, "Documento del cliente", 20),
            ClienteNombre = Validar.TextoOpcional(clienteNombre, "Cliente", LargoMaximoTexto),
            Moneda = Validar.Texto(moneda, "Moneda", Pagos.Moneda.LargoCodigo).ToUpperInvariant(),
            SimboloMoneda = Validar.Texto(simboloMoneda, "Símbolo de la moneda", Pagos.Moneda.LargoMaximoSimbolo),
            Total = total,
            ConsultadaEn = ahora,
        };

    /// <param name="articuloId">Artículo del maestro local; 0 si el código ya no existe en esta caja.</param>
    /// <param name="devuelta">Lo ya devuelto de esa línea en toda la empresa, según el Central.</param>
    public void AgregarLinea(int numeroLinea, int articuloId, string codigoInterno, string codigoLeido, string descripcion, TipoArticulo tipoArticulo,
        string unidadMedidaCodigo, int decimalesCantidad, decimal cantidad, decimal importeConImpuesto, decimal porcentajeImpuesto,
        int indicadorFacturacion, bool esServicio, string? serial, decimal devuelta) =>
        _lineas.Add(new LineaFacturaConsultada
        {
            FacturaConsultadaId = Id,
            NumeroLinea = numeroLinea,
            ArticuloId = articuloId,
            CodigoInterno = Validar.TextoOpcional(codigoInterno, "Código del artículo", LargoMaximoCodigo) ?? string.Empty,
            CodigoLeido = Validar.TextoOpcional(codigoLeido, "Código leído", LargoMaximoCodigo) ?? string.Empty,
            Descripcion = Validar.TextoOpcional(descripcion, "Descripción", LargoMaximoTexto) ?? string.Empty,
            TipoArticulo = tipoArticulo,
            UnidadMedidaCodigo = Validar.TextoOpcional(unidadMedidaCodigo, "Unidad de medida", 20) ?? string.Empty,
            DecimalesCantidad = decimalesCantidad,
            Cantidad = cantidad,
            ImporteConImpuesto = importeConImpuesto,
            PorcentajeImpuesto = porcentajeImpuesto,
            IndicadorFacturacion = indicadorFacturacion,
            EsServicio = esServicio,
            Serial = Validar.TextoOpcional(serial, "Serial", 100),
            Devuelta = devuelta,
        });

    /// <summary>La factura como la ve la devolución. La caja que emite la nota es la de la sesión, no la que vendió.</summary>
    public FacturaParaDevolver ParaDevolver(int sucursalId, int cajaId) =>
        new(VentaLocalId, Numero, CobradaEn, sucursalId, cajaId, TipoComprobante, Moneda, SimboloMoneda,
            _lineas.OrderBy(l => l.NumeroLinea)
                .Select(l => new LineaFacturaParaDevolver(l.NumeroLinea, l.ArticuloId, l.CodigoInterno, l.CodigoLeido, l.Descripcion, l.TipoArticulo,
                    l.UnidadMedidaCodigo, l.DecimalesCantidad > 0, l.DecimalesCantidad, l.Cantidad, l.ImporteConImpuesto, l.PorcentajeImpuesto,
                    l.IndicadorFacturacion, l.EsServicio, l.Serial))
                .ToList());

    /// <summary>Lo ya devuelto según el Central, en el formato que espera la devolución.</summary>
    public Dictionary<int, DevueltoLinea> Devuelto() =>
        _lineas.Where(l => l.Devuelta > 0m).ToDictionary(l => l.NumeroLinea,
            l => new DevueltoLinea(l.Devuelta, Devolucion.Redondear(l.ImporteConImpuesto / l.Cantidad * l.Devuelta)));
}

public sealed class LineaFacturaConsultada : Entidad
{
    public int FacturaConsultadaId { get; internal set; }
    public int NumeroLinea { get; internal set; }
    public int ArticuloId { get; internal set; }
    public string CodigoInterno { get; internal set; } = string.Empty;
    public string CodigoLeido { get; internal set; } = string.Empty;
    public string Descripcion { get; internal set; } = string.Empty;
    public TipoArticulo TipoArticulo { get; internal set; }
    public string UnidadMedidaCodigo { get; internal set; } = string.Empty;
    public int DecimalesCantidad { get; internal set; }
    public decimal Cantidad { get; internal set; }

    /// <summary>Importe neto de la línea con ITBIS, tal como se cobró.</summary>
    public decimal ImporteConImpuesto { get; internal set; }

    public decimal PorcentajeImpuesto { get; internal set; }
    public int IndicadorFacturacion { get; internal set; }
    public bool EsServicio { get; internal set; }
    public string? Serial { get; internal set; }
    public decimal Devuelta { get; internal set; }
}
