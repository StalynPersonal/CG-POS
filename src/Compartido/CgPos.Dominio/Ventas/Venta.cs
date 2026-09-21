using System.Globalization;
using CgPos.Dominio.Catalogo;
using CgPos.Dominio.Comun;
using CgPos.Dominio.Entregas;
using CgPos.Dominio.Fiscal;
using CgPos.Dominio.Pagos;
using CgPos.Dominio.Promociones;

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
    ComprobanteNoPermitido,
    DocumentoRequerido,
    SinLineas,
    RequiereSerial,
    SerialDuplicado,
    ArticuloEnOferta,
    DescuentoNoPermitido,
    DescuentoInvalido,
    PagoInvalido,
    PagoInsuficiente,
    DevueltaNoPermitida,
    EntregaInvalida,
}

/// <summary>Una regla de la venta impidió la operación; el código permite a la pantalla reaccionar.</summary>
public sealed class ReglaVentaExcepcion(CodigoErrorVenta codigo, string mensaje) : InvalidOperationException(mensaje)
{
    public CodigoErrorVenta Codigo { get; } = codigo;
}

/// <summary>Datos del artículo tal como se leyó en caja, con precios vigentes y lectura de balanza.</summary>
public sealed record ArticuloParaVenta(
    int ArticuloId,
    string CodigoInterno,
    string CodigoLeido,
    string Descripcion,
    TipoArticulo Tipo,
    int DepartamentoId,
    bool PermiteDescuentoManual,
    string UnidadMedidaCodigo,
    bool PermiteDecimales,
    int DecimalesCantidad,
    int ImpuestoId,
    decimal PorcentajeImpuesto,
    int IndicadorFacturacion,
    decimal? PrecioDetalle,
    decimal? PrecioMayor,
    decimal? CantidadMinimaMayor,
    decimal? PrecioMinimo,
    decimal? PesoLeido,
    decimal? PrecioLeido,
    bool EsServicio = false,
    int? CategoriaId = null,
    int? MarcaId = null);

public sealed record DesgloseImpuesto(decimal Porcentaje, int IndicadorFacturacion, decimal Base, decimal Impuesto, decimal Total);

/// <param name="Retencion">Retención de la Ley 32-23 que el cliente de régimen especial (E44) no paga en caja.</param>
public sealed record TotalesVenta(
    decimal Subtotal,
    decimal Impuesto,
    decimal Total,
    int CantidadLineas,
    decimal CantidadArticulos,
    IReadOnlyList<DesgloseImpuesto> Desglose,
    decimal Descuento = 0m,
    decimal Retencion = 0m)
{
    /// <summary>Lo que el cliente paga en caja: el total de la factura menos la retención.</summary>
    public decimal TotalAPagar => Total - Retencion;
}

public enum TipoDescuento
{
    Porcentaje,
    Monto,
}

/// <param name="Base">Importe sobre el que se calcula el descuento.</param>
public sealed record VistaPreviaDescuento(decimal Monto, decimal Porcentaje, decimal Base);

/// <param name="LineasExcluidas">Líneas elegidas que no toman el descuento (en oferta o de departamentos sin descuento manual, RF-204).</param>
public sealed record ResultadoDescuentoFactura(decimal Monto, decimal Porcentaje, IReadOnlyList<int> LineasExcluidas);

/// <summary>Cliente asignado a la venta: registrado en el maestro o solo con su documento y nombre.</summary>
public sealed record ClienteVenta(
    int? ClienteId,
    TipoDocumentoIdentidad? TipoDocumento,
    string? Documento,
    string Nombre,
    TipoComprobante ComprobantePredeterminado);

/// <summary>Miembro del programa de fidelidad asignado a la venta.</summary>
public sealed record MiembroVenta(int MiembroId, string Cedula, string Nombre, string? Nivel);

/// <summary>Qué comprobantes se emiten en una venta de caja y qué documento del comprador exige cada uno (RF-27).</summary>
public static class ReglasComprobante
{
    public static IReadOnlyList<TipoComprobante> DeVenta { get; } =
    [
        TipoComprobante.FacturaConsumo,
        TipoComprobante.FacturaCreditoFiscal,
        TipoComprobante.RegimenesEspeciales,
        TipoComprobante.Gubernamental,
    ];

    public static bool EsDeVenta(TipoComprobante tipo) => DeVenta.Contains(tipo);

    /// <summary>Documento que exige el tipo: nulo si no exige ninguno.</summary>
    public static IReadOnlyList<TipoDocumentoIdentidad>? DocumentosAceptados(TipoComprobante tipo) => tipo switch
    {
        TipoComprobante.FacturaCreditoFiscal or TipoComprobante.RegimenesEspeciales => [TipoDocumentoIdentidad.Rnc, TipoDocumentoIdentidad.Cedula],
        TipoComprobante.Gubernamental => [TipoDocumentoIdentidad.Rnc],
        _ => null,
    };

    public static bool ClienteCumple(TipoComprobante tipo, TipoDocumentoIdentidad? tipoDocumento, string? documento) =>
        DocumentosAceptados(tipo) is not { } aceptados
        || (!string.IsNullOrEmpty(documento) && tipoDocumento is { } tipoDoc && aceptados.Contains(tipoDoc));

    public static string Nombre(TipoComprobante tipo) => tipo switch
    {
        TipoComprobante.FacturaConsumo => "Consumidor final",
        TipoComprobante.FacturaCreditoFiscal => "Crédito fiscal",
        TipoComprobante.RegimenesEspeciales => "Régimen especial",
        TipoComprobante.Gubernamental => "Gubernamental",
        TipoComprobante.NotaCredito => "Nota de crédito",
        TipoComprobante.NotaDebito => "Nota de débito",
        _ => tipo.ToString(),
    };
}

/// <summary>
/// Transacción de venta en la caja. Se guarda en cada cambio para poder recuperarla tras un corte (RF-195).
/// Su número (sucursal-caja-secuencia) es único e independiente del NCF (RF-193).
/// </summary>
public sealed class Venta : Entidad
{
    public const int LargoMaximoNumero = 40;
    public const int LargoMaximoCertificacion = 50;
    public const int LargoMaximoNumeroCotizacion = 20;
    public const int LargoMaximoMotivo = 500;
    public const int LargoMaximoUsuario = 150;
    public const int LargoMaximoNombreCliente = 150;
    public const int LargoMaximoDocumento = 20;
    public const decimal CantidadMaxima = 99_999m;

    private readonly List<LineaVenta> _lineas = [];
    private readonly List<PagoVenta> _pagos = [];
    private readonly List<DestinoEntrega> _destinosEntrega = [];

    private Venta()
    {
    }

    public string NumeroTransaccion { get; private set; } = string.Empty;
    public long Secuencia { get; private set; }
    public int SucursalId { get; private set; }
    public int CajaId { get; private set; }
    public int TurnoId { get; private set; }
    public int UsuarioId { get; private set; }
    public string UsuarioNombre { get; private set; } = string.Empty;

    /// <summary>Moneda local de la caja al iniciar la venta (ISO 4217): los montos de la factura están en ella.</summary>
    public string Moneda { get; private set; } = string.Empty;

    public string SimboloMoneda { get; private set; } = string.Empty;

    public EstadoVenta Estado { get; private set; }
    public DateTimeOffset IniciadaEn { get; private set; }
    public DateTimeOffset ActualizadaEn { get; private set; }
    public DateTimeOffset? AnuladaEn { get; private set; }
    public string? MotivoAnulacion { get; private set; }
    public int? AnuladaPorId { get; private set; }
    public string? AnuladaPorNombre { get; private set; }

    // Cliente y comprobante (RF-13, RF-27)
    public int? ClienteId { get; private set; }
    public TipoDocumentoIdentidad? ClienteTipoDocumento { get; private set; }
    public string? ClienteDocumento { get; private set; }
    public string? ClienteNombre { get; private set; }
    public TipoComprobante TipoComprobante { get; private set; } = TipoComprobante.FacturaConsumo;

    /// <summary>
    /// Porcentaje de retención de la Ley 32-23 que aplica a esta factura: solo en comprobantes de régimen especial (E44) y con el
    /// porcentaje configurado en el Central. Se calcula sobre el subtotal con descuentos y se descuenta de lo que el cliente paga.
    /// </summary>
    public decimal PorcentajeRetencion { get; private set; }

    /// <summary>
    /// Número de la certificación de exención de ITBIS que presentó una entidad del Estado (E45). Con ella la factura va
    /// exenta; sin ella, el gubernamental lleva ITBIS como cualquier otra.
    /// </summary>
    public string? CertificacionExencion { get; private set; }

    /// <summary>Monto tope que pidió el cliente; se avisa al superarlo (RF-18).</summary>
    public decimal? LimiteCompra { get; private set; }

    /// <summary>Número de la lista de boda contra la que se compra (RF-73); la lista vive en el Central.</summary>
    public string? ListaBodaNumero { get; private set; }

    /// <summary>Evento de la lista, para mostrarlo en pantalla y en el ticket sin volver a consultar al Central.</summary>
    public string? ListaBodaEvento { get; private set; }

    // Programa de fidelidad (M11): la cédula identifica al miembro (RF-236).
    public int? FidelidadMiembroId { get; private set; }
    public string? FidelidadCedula { get; private set; }
    public string? FidelidadNombre { get; private set; }
    public string? FidelidadNivel { get; private set; }
    public int PuntosAcumulados { get; private set; }
    public int PuntosCanjeados { get; private set; }

    public bool TieneFidelidad => FidelidadMiembroId is not null;

    /// <summary>Cuándo se puso en espera (RF-22, RF-197).</summary>
    public DateTimeOffset? PuestaEnEsperaEn { get; private set; }

    // Descuento a la factura (RF-200, RF-201): se guarda la definición y se prorratea en cada cambio.
    public TipoDescuento? DescuentoFacturaTipo { get; private set; }
    public decimal? DescuentoFacturaValor { get; private set; }

    /// <summary>Números de línea a los que se limitó el descuento, separados por coma; nulo = todas.</summary>
    public string? DescuentoFacturaLineas { get; private set; }

    public string? MotivoDescuentoFactura { get; private set; }
    public int? DescuentoFacturaAutorizadoPorId { get; private set; }
    public string? DescuentoFacturaAutorizadoPorNombre { get; private set; }

    // Cobro (M08)
    public DateTimeOffset? CobradaEn { get; private set; }
    public int? CobradaPorId { get; private set; }
    public string? CobradaPorNombre { get; private set; }

    /// <summary>Total cobrado después del redondeo del efectivo.</summary>
    public decimal? TotalCobrado { get; private set; }

    public decimal Devuelta { get; private set; }

    /// <summary>Diferencia por redondeo del efectivo (RF-216). No altera el total fiscal de la factura.</summary>
    public decimal RedondeoEfectivo { get; private set; }

    public IReadOnlyCollection<PagoVenta> Pagos => _pagos;

    /// <summary>Destinos de entrega o envío de parte de la mercancía (M12); lo demás se despacha en caja.</summary>
    public IReadOnlyCollection<DestinoEntrega> DestinosEntrega => _destinosEntrega;

    public IReadOnlyCollection<LineaVenta> Lineas => _lineas;

    /// <summary>Menor cantidad de dígitos que admite la secuencia de un número de documento.</summary>
    public static Venta Iniciar(int sucursalId, string codigoSucursal, int cajaId, string codigoCaja, int turnoId, long secuencia, int digitosSecuencia,
        int usuarioId, string usuarioNombre, string moneda, string simboloMoneda, DateTimeOffset ahora)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(secuencia, 1);

        return new Venta
        {
            NumeroTransaccion = Validar.Texto(NumeroDocumento.Formatear(codigoSucursal, codigoCaja, TipoDocumentoNumerado.Factura, secuencia, digitosSecuencia), "Número de transacción", LargoMaximoNumero),
            Secuencia = secuencia,
            SucursalId = Validar.Id(sucursalId, "Sucursal"),
            CajaId = Validar.Id(cajaId, "Caja"),
            TurnoId = Validar.Id(turnoId, "Turno"),
            UsuarioId = Validar.Id(usuarioId, "Usuario"),
            UsuarioNombre = Validar.Texto(usuarioNombre, "Usuario", LargoMaximoUsuario),
            Moneda = FormaPago.ValidarMoneda(moneda),
            SimboloMoneda = Validar.Texto(simboloMoneda, "Símbolo de la moneda", CgPos.Dominio.Pagos.Moneda.LargoMaximoSimbolo),
            Estado = EstadoVenta.EnCurso,
            IniciadaEn = ahora,
            ActualizadaEn = ahora,
        };
    }

    /// <summary>
    /// Agrega un artículo. Los pesados toman la cantidad de la etiqueta o balanza (RF-19, RF-20);
    /// los serializados exigen su serial y van de uno en uno (RF-17), salvo que se entreguen después: el serial se captura en el despacho (RN-16);
    /// los demás usan la cantidad indicada o 1, redondeada a los decimales de su unidad (RF-198).
    /// </summary>
    /// <param name="serialEnDespacho">Serializado sin serial en caja porque se marcará para entrega o envío.</param>
    public LineaVenta AgregarArticulo(ArticuloParaVenta articulo, decimal? cantidadIndicada, DateTimeOffset ahora, string? serial = null, bool serialEnDespacho = false)
    {
        ArgumentNullException.ThrowIfNull(articulo);
        AsegurarEditable();

        if (articulo.PrecioDetalle is not { } precioDetalle)
            throw new ReglaVentaExcepcion(CodigoErrorVenta.SinPrecio, $"El artículo {articulo.CodigoInterno} no tiene precio vigente.");

        var serialLimpio = string.IsNullOrWhiteSpace(serial) ? null : serial.Trim().ToUpperInvariant();
        var serialPendiente = false;
        if (articulo.Tipo == TipoArticulo.Serializado && serialLimpio is null && serialEnDespacho)
        {
            if (cantidadIndicada is not null and not 1m)
                throw new ReglaVentaExcepcion(CodigoErrorVenta.CantidadInvalida, "Los artículos con serial se registran de uno en uno.");
            serialPendiente = true;
        }
        else if (articulo.Tipo == TipoArticulo.Serializado)
        {
            if (serialLimpio is null)
                throw new ReglaVentaExcepcion(CodigoErrorVenta.RequiereSerial, $"Escanee el serial de {articulo.Descripcion}.");
            if (cantidadIndicada is not null and not 1m)
                throw new ReglaVentaExcepcion(CodigoErrorVenta.CantidadInvalida, "Los artículos con serial se registran de uno en uno.");
            if (serialLimpio.Length > LineaVenta.LargoMaximoSerial)
                throw new ReglaVentaExcepcion(CodigoErrorVenta.RequiereSerial, $"El serial no puede superar {LineaVenta.LargoMaximoSerial} caracteres.");
            if (_lineas.Any(l => l.EstaActiva && l.ArticuloId == articulo.ArticuloId && l.Serial == serialLimpio))
                throw new ReglaVentaExcepcion(CodigoErrorVenta.SerialDuplicado, $"El serial {serialLimpio} ya está en esta venta.");
        }
        else
        {
            serialLimpio = null;
        }

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

        var linea = LineaVenta.Crear(Id, SiguienteNumeroLinea(), articulo, cantidad, precio, importeEtiqueta, leidaDeBalanza, serialLimpio, serialPendiente);

        // En una factura de régimen especial lo que se agrega ya entra sin ITBIS, como el resto.
        if (ExentaDeImpuesto)
            linea.Exentar();

        _lineas.Add(linea);
        ProrratearDescuentoFactura();
        ActualizadaEn = ahora;
        return linea;
    }

    /// <summary>
    /// Agrega una línea con el precio que se le cotizó al cliente. No pasa por las reglas de precio ni por las ofertas: lo
    /// cotizado es un compromiso por escrito y se respeta tal cual mientras la cotización esté vigente.
    /// </summary>
    /// <param name="precioUnitario">Con impuesto incluido, el de la cotización.</param>
    /// <param name="descuento">Descuento de esa línea en la cotización; 0 si no lleva.</param>
    public LineaVenta AgregarArticuloCotizado(ArticuloParaVenta articulo, decimal cantidad, decimal precioUnitario, decimal descuento, DateTimeOffset ahora)
    {
        ArgumentNullException.ThrowIfNull(articulo);
        AsegurarEditable();

        if (precioUnitario < 0)
            throw new ReglaVentaExcepcion(CodigoErrorVenta.SinPrecio, $"El precio cotizado de {articulo.Descripcion} no puede ser negativo.");

        var normalizada = NormalizarCantidad(cantidad, articulo.PermiteDecimales, articulo.DecimalesCantidad);
        var precio = new PrecioDeterminado(ListaPrecio.Detalle, precioUnitario, MotivoPrecio.PrecioCotizado, RequiereAutorizacion: false);
        var linea = LineaVenta.Crear(Id, SiguienteNumeroLinea(),
            articulo with { PrecioDetalle = precioUnitario, PrecioMayor = null, CantidadMinimaMayor = null },
            normalizada, precio, importeEtiqueta: null, leidaDeBalanza: false, serial: null, serialPendiente: false);

        if (ExentaDeImpuesto)
            linea.Exentar();

        _lineas.Add(linea);

        if (descuento > 0)
            linea.AplicarDescuentoCotizado(decimal.Round(descuento, 2, MidpointRounding.AwayFromZero));

        ProrratearDescuentoFactura();
        ActualizadaEn = ahora;
        return linea;
    }

    /// <summary>Deja constancia de qué cotización se facturó, para el ticket y para avisarle al Central.</summary>
    public void AsignarCotizacion(string? numero, DateTimeOffset ahora)
    {
        AsegurarEditable();
        CotizacionNumero = string.IsNullOrWhiteSpace(numero) ? null : Validar.Texto(numero, "Cotización", LargoMaximoNumeroCotizacion).ToUpperInvariant();
        ActualizadaEn = ahora;
    }

    /// <summary>Cambia la cantidad de una línea (RF-110) y recalcula el precio por mayor automático.</summary>
    public void CambiarCantidad(int numeroLinea, decimal cantidad, DateTimeOffset ahora)
    {
        AsegurarEditable();
        var linea = LineaActiva(numeroLinea);
        AsegurarSinEntrega(linea);

        if (linea.LeidaDeBalanza)
            throw new ReglaVentaExcepcion(CodigoErrorVenta.RequiereBalanza, "La cantidad de un artículo pesado viene de la balanza; no se cambia a mano.");
        if (linea.Serial is not null)
            throw new ReglaVentaExcepcion(CodigoErrorVenta.CantidadInvalida, "Un artículo con serial se vende de uno en uno; escanee otro serial para agregar más.");

        var normalizada = NormalizarCantidad(cantidad, linea.PermiteDecimales, linea.DecimalesCantidad);
        var precio = ReglasPrecio.Determinar(linea.CodigoInterno, linea.TipoArticulo, linea.CantidadMinimaMayor,
            new PreciosVigentes(linea.PrecioDetalle, linea.PrecioMayor), normalizada, SeleccionListaPrecio.Automatica);

        linea.CambiarCantidad(normalizada, precio);
        ProrratearDescuentoFactura();
        ActualizadaEn = ahora;
    }

    /// <summary>
    /// Elimina una línea: queda marcada como anulada y se agrega debajo su reverso en negativo con precio cero (RF-114).
    /// </summary>
    public LineaVenta EliminarLinea(int numeroLinea, DateTimeOffset ahora)
    {
        AsegurarEditable();
        var linea = LineaActiva(numeroLinea);
        AsegurarSinEntrega(linea);

        linea.MarcarAnulada();
        var reverso = LineaVenta.CrearReverso(Id, SiguienteNumeroLinea(), linea);
        _lineas.Add(reverso);
        ProrratearDescuentoFactura();
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
    public void Anular(string motivo, int usuarioId, string usuarioNombre, DateTimeOffset ahora)
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
    /// Asigna el cliente y toma su comprobante habitual; si ese comprobante no aplica en caja o el cliente no tiene
    /// el documento que exige, queda como consumidor final (RF-13). Se puede cambiar hasta totalizar.
    /// </summary>
    public void AsignarCliente(ClienteVenta cliente, DateTimeOffset ahora)
    {
        ArgumentNullException.ThrowIfNull(cliente);
        AsegurarEditable();

        var documento = string.IsNullOrWhiteSpace(cliente.Documento) ? null : DocumentoIdentidad.Normalizar(cliente.Documento);
        if (documento is not null && cliente.TipoDocumento is null)
            throw new ArgumentException("Un documento debe indicar su tipo.", nameof(cliente));

        ClienteId = cliente.ClienteId;
        ClienteTipoDocumento = documento is null ? null : cliente.TipoDocumento;
        ClienteDocumento = Validar.TextoOpcional(documento, "Documento del cliente", LargoMaximoDocumento);
        ClienteNombre = Validar.Texto(cliente.Nombre, "Nombre del cliente", LargoMaximoNombreCliente);

        var comprobante = cliente.ComprobantePredeterminado;
        TipoComprobante = ReglasComprobante.EsDeVenta(comprobante) && ReglasComprobante.ClienteCumple(comprobante, ClienteTipoDocumento, ClienteDocumento)
            ? comprobante
            : TipoComprobante.FacturaConsumo;
        ActualizadaEn = ahora;
    }

    public void QuitarCliente(DateTimeOffset ahora)
    {
        AsegurarEditable();
        ClienteId = null;
        ClienteTipoDocumento = null;
        ClienteDocumento = null;
        ClienteNombre = null;
        TipoComprobante = TipoComprobante.FacturaConsumo;
        ActualizadaEn = ahora;
    }

    /// <summary>
    /// Marca líneas, completas o en parte, para retiro en un almacén o envío a dirección (RF-246 a RF-248, RN-12 a RN-14). Quien llama
    /// valida la autorización del supervisor (RF-53, RN-15). Una factura admite varios destinos.
    /// </summary>
    public DestinoEntrega MarcarEntrega(MetodoEntrega metodo, int? sucursalRetiroId, string? sucursalRetiroNombre, DatosEnvio? envio, DateOnly? fechaComprometida,
        string? comentario, IReadOnlyCollection<CantidadEntrega> cantidades, int? autorizadoPorId, string? autorizadoPorNombre, DateOnly hoy, DateTimeOffset ahora)
    {
        ArgumentNullException.ThrowIfNull(cantidades);
        AsegurarEditable();

        if (!Enum.IsDefined(metodo))
            throw new ReglaVentaExcepcion(CodigoErrorVenta.EntregaInvalida, "Método de entrega no válido.");
        if (metodo == MetodoEntrega.RetiroSucursal && (sucursalRetiroId is null || string.IsNullOrWhiteSpace(sucursalRetiroNombre)))
            throw new ReglaVentaExcepcion(CodigoErrorVenta.EntregaInvalida, "Seleccione el almacén o la sucursal donde el cliente retira.");
        if (metodo == MetodoEntrega.Envio && (envio is null || string.IsNullOrWhiteSpace(envio.Direccion) || string.IsNullOrWhiteSpace(envio.Telefono)))
            throw new ReglaVentaExcepcion(CodigoErrorVenta.EntregaInvalida, "El envío requiere la dirección y un teléfono de contacto.");
        if (envio?.CostoEnvio < 0)
            throw new ReglaVentaExcepcion(CodigoErrorVenta.EntregaInvalida, "El costo del envío no puede ser negativo.");
        if (fechaComprometida < hoy)
            throw new ReglaVentaExcepcion(CodigoErrorVenta.EntregaInvalida, "La fecha comprometida no puede ser anterior a hoy.");

        var pedidas = cantidades.Where(c => c.Cantidad != 0m).ToList();
        if (pedidas.Count == 0)
            throw new ReglaVentaExcepcion(CodigoErrorVenta.EntregaInvalida, "Indique los artículos y las cantidades que se entregan después.");
        if (pedidas.GroupBy(p => p.NumeroLinea).Any(grupo => grupo.Count() > 1))
            throw new ReglaVentaExcepcion(CodigoErrorVenta.EntregaInvalida, "Cada línea se indica una sola vez por destino.");

        foreach (var pedida in pedidas)
        {
            var linea = LineaActiva(pedida.NumeroLinea);
            if (linea.EsServicio)
                throw new ReglaVentaExcepcion(CodigoErrorVenta.EntregaInvalida, $"{linea.Descripcion} es un servicio: no tiene entrega.");
            if (pedida.Cantidad < 0 || (!linea.PermiteDecimales && pedida.Cantidad != decimal.Truncate(pedida.Cantidad)))
                throw new ReglaVentaExcepcion(CodigoErrorVenta.EntregaInvalida, $"{linea.Descripcion}: la cantidad a entregar no es válida.");

            var disponible = linea.Cantidad - CantidadEnEntregas(linea.NumeroLinea);
            if (pedida.Cantidad > disponible)
                throw new ReglaVentaExcepcion(CodigoErrorVenta.EntregaInvalida, disponible <= 0
                    ? $"{linea.Descripcion} ya está marcado completo para entrega."
                    : $"{linea.Descripcion}: solo quedan {disponible:0.###} por marcar.");
        }

        var numero = _destinosEntrega.Count == 0 ? 1 : _destinosEntrega.Max(d => d.Numero) + 1;
        var destino = DestinoEntrega.Crear(Id, numero, metodo, sucursalRetiroId, sucursalRetiroNombre, envio, fechaComprometida, comentario, autorizadoPorId, autorizadoPorNombre, pedidas);
        _destinosEntrega.Add(destino);
        ActualizadaEn = ahora;
        return destino;
    }

    /// <summary>Quita un destino: sus artículos vuelven a despacharse en caja.</summary>
    public void QuitarEntrega(int numeroDestino, DateTimeOffset ahora)
    {
        AsegurarEditable();
        var destino = _destinosEntrega.FirstOrDefault(d => d.Numero == numeroDestino)
            ?? throw new ReglaVentaExcepcion(CodigoErrorVenta.EntregaInvalida, $"El destino de entrega {numeroDestino} no existe.");

        _destinosEntrega.Remove(destino);
        ActualizadaEn = ahora;
    }

    /// <summary>Cantidad de la línea marcada para entrega o envío en todos los destinos.</summary>
    public decimal CantidadEnEntregas(int numeroLinea) => _destinosEntrega.SelectMany(d => d.Lineas).Where(l => l.NumeroLinea == numeroLinea).Sum(l => l.Cantidad);

    private void AsegurarSinEntrega(LineaVenta linea)
    {
        if (CantidadEnEntregas(linea.NumeroLinea) > 0)
            throw new ReglaVentaExcepcion(CodigoErrorVenta.EntregaInvalida,
                $"La línea {linea.NumeroLinea} está marcada para entrega: quite el destino de entrega antes de modificarla.");
    }

    /// <summary>Identifica al miembro del programa de fidelidad (RF-126, RF-236): habilita sus ofertas exclusivas y acumula al cobrar.</summary>
    public void AsignarFidelidad(MiembroVenta miembro, DateTimeOffset ahora)
    {
        ArgumentNullException.ThrowIfNull(miembro);
        AsegurarEditable();

        FidelidadMiembroId = Validar.Id(miembro.MiembroId, "Miembro");
        FidelidadCedula = Validar.Texto(miembro.Cedula, "Cédula", LargoMaximoDocumento);
        FidelidadNombre = Validar.Texto(miembro.Nombre, "Nombre del miembro", LargoMaximoNombreCliente);
        FidelidadNivel = Validar.TextoOpcional(miembro.Nivel, "Nivel", LargoMaximoNombreCliente);
        ActualizadaEn = ahora;
    }

    public void QuitarFidelidad(DateTimeOffset ahora)
    {
        AsegurarEditable();
        FidelidadMiembroId = null;
        FidelidadCedula = null;
        FidelidadNombre = null;
        FidelidadNivel = null;
        ActualizadaEn = ahora;
    }

    /// <summary>Puntos que acumuló y canjeó la venta cobrada (RF-238, RF-239).</summary>
    public void RegistrarPuntos(int acumulados, int canjeados)
    {
        if (Estado != EstadoVenta.Cobrada)
            throw new ReglaVentaExcepcion(CodigoErrorVenta.VentaNoEditable, "Los puntos se registran al cobrar la venta.");
        ArgumentOutOfRangeException.ThrowIfNegative(acumulados);
        ArgumentOutOfRangeException.ThrowIfNegative(canjeados);
        if (!TieneFidelidad && acumulados + canjeados > 0)
            throw new ReglaVentaExcepcion(CodigoErrorVenta.PagoInvalido, "La venta no tiene un miembro del programa de fidelidad.");

        PuntosAcumulados = acumulados;
        PuntosCanjeados = canjeados;
    }

    /// <summary>Cambia el comprobante (RF-127); valida que el cliente tenga el documento que el tipo exige.</summary>
    public void CambiarComprobante(TipoComprobante tipo, DateTimeOffset ahora)
    {
        AsegurarEditable();

        if (!ReglasComprobante.EsDeVenta(tipo))
            throw new ReglaVentaExcepcion(CodigoErrorVenta.ComprobanteNoPermitido, $"El comprobante {ReglasComprobante.Nombre(tipo)} no se emite en una venta de caja.");

        if (!ReglasComprobante.ClienteCumple(tipo, ClienteTipoDocumento, ClienteDocumento))
        {
            var aceptados = string.Join(" o ", ReglasComprobante.DocumentosAceptados(tipo)!.Select(d => d == TipoDocumentoIdentidad.Rnc ? "RNC" : "cédula"));
            throw new ReglaVentaExcepcion(CodigoErrorVenta.DocumentoRequerido, $"El comprobante {ReglasComprobante.Nombre(tipo)} requiere un cliente con {aceptados}.");
        }

        TipoComprobante = tipo;
        if (tipo != TipoComprobante.Gubernamental)
        {
            PorcentajeRetencion = 0m;

            // La certificación es de la entidad del Estado: cambiando de comprobante deja de tener sentido.
            CertificacionExencion = null;
        }

        AplicarExencionDelComprobante();
        ActualizadaEn = ahora;
    }

    /// <summary>
    /// El régimen especial (E44) se factura exento de ITBIS: el cliente paga la base, no el precio con impuesto. Lo dice la
    /// DGII para zonas francas y demás regímenes, y vale para toda la factura, tenga o no impuesto cada artículo. Si el
    /// comprobante cambia a otro tipo, las líneas vuelven a su precio con impuesto.
    /// </summary>
    private void AplicarExencionDelComprobante()
    {
        foreach (var linea in _lineas)
        {
            if (ExentaDeImpuesto)
                linea.Exentar();
            else
                linea.Gravar();
        }

        // El descuento de factura se reparte sobre los importes nuevos.
        ProrratearDescuentoFactura();
    }

    /// <summary>
    /// Registra la certificación de exención de ITBIS de una entidad del Estado, o la quita con un número vacío. Con ella,
    /// la factura gubernamental se emite exenta, igual que la de régimen especial.
    /// </summary>
    /// <exception cref="ReglaVentaExcepcion">La venta no es gubernamental.</exception>
    public void RegistrarCertificacionExencion(string? certificacion, DateTimeOffset ahora)
    {
        AsegurarEditable();

        var limpia = string.IsNullOrWhiteSpace(certificacion) ? null : certificacion.Trim();
        if (limpia is not null && TipoComprobante != TipoComprobante.Gubernamental)
            throw new ReglaVentaExcepcion(CodigoErrorVenta.ComprobanteNoPermitido,
                "La certificación de exención solo va en una factura gubernamental (E45).");

        if (limpia == CertificacionExencion)
            return;

        CertificacionExencion = limpia is null ? null : Validar.Texto(limpia, "Certificación de exención", LargoMaximoCertificacion);
        AplicarExencionDelComprobante();
        ActualizadaEn = ahora;
    }

    /// <summary>
    /// Aplica la retención de la Ley 32-23 con el porcentaje configurado; solo queda puesta en facturas gubernamentales
    /// (E45), que es donde el Estado retiene el ISR al pagarle a su proveedor. Un emisor electrónico autorizado está
    /// exento de esa retención (artículo 34 de la Ley 32-23), así que lo normal es tenerla en cero.
    /// </summary>
    public void AplicarRetencionLey(decimal porcentaje, DateTimeOffset ahora)
    {
        AsegurarEditable();
        if (porcentaje is < 0m or > 100m)
            throw new ReglaVentaExcepcion(CodigoErrorVenta.DescuentoInvalido, "El porcentaje de retención debe estar entre 0 y 100.");

        var aplicable = TipoComprobante == TipoComprobante.Gubernamental ? porcentaje : 0m;
        if (aplicable == PorcentajeRetencion)
            return;

        PorcentajeRetencion = aplicable;
        ActualizadaEn = ahora;
    }

    /// <summary>Asocia la venta a una lista de boda del Central (RF-73), o la quita con número nulo.</summary>
    public void AsignarListaBoda(string? numero, string? evento, DateTimeOffset ahora)
    {
        AsegurarEditable();
        ListaBodaNumero = Validar.TextoOpcional(numero, "Número de la lista de boda", ListasBoda.ListaBoda.LargoMaximoNumero)?.ToUpperInvariant();
        ListaBodaEvento = ListaBodaNumero is null ? null : Validar.TextoOpcional(evento, "Evento", ListasBoda.ListaBoda.LargoMaximoNombre);
        ActualizadaEn = ahora;
    }

    /// <summary>Fija o quita (nulo) el monto tope de la compra (RF-18).</summary>
    public void EstablecerLimiteCompra(decimal? limite, DateTimeOffset ahora)
    {
        AsegurarEditable();
        if (limite <= 0)
            throw new ReglaVentaExcepcion(CodigoErrorVenta.CantidadInvalida, "El límite de compra debe ser mayor que cero.");

        LimiteCompra = limite is { } monto ? decimal.Round(monto, 2, MidpointRounding.AwayFromZero) : null;
        ActualizadaEn = ahora;
    }

    /// <summary>Guarda la venta en espera para atender a otro cliente (RF-22). Queda ligada al cajero y al turno (RF-197).</summary>
    public void PonerEnEspera(DateTimeOffset ahora)
    {
        AsegurarEditable();
        if (!_lineas.Any(l => l.EstaActiva))
            throw new ReglaVentaExcepcion(CodigoErrorVenta.SinLineas, "Una venta sin artículos no se pone en espera.");

        Estado = EstadoVenta.EnEspera;
        PuestaEnEsperaEn = ahora;
        ActualizadaEn = ahora;
    }

    public void Retomar(DateTimeOffset ahora)
    {
        if (Estado != EstadoVenta.EnEspera)
            throw new ReglaVentaExcepcion(CodigoErrorVenta.VentaNoEditable, $"La venta {NumeroTransaccion} no está en espera.");

        Estado = EstadoVenta.EnCurso;
        PuestaEnEsperaEn = null;
        ActualizadaEn = ahora;
    }

    /// <summary>
    /// Recalcula las ofertas vigentes (RF-61, RF-69). Agrupa las líneas activas por artículo (así un 2x1 cuenta aunque se
    /// escanee de a uno), parte del precio automático (detalle o mayor por cantidad) y aplica la oferta más favorable solo si
    /// deja el grupo más barato que ese precio (RN-11, RF-124). Los grupos con descuento manual o con la oferta desactivada
    /// no reciben ofertas (RN-08).
    /// </summary>
    /// <param name="ahoraLocal">Fecha y hora local de la caja, para los días y horas de las ofertas.</param>
    /// <remarks>Las ofertas exclusivas del programa de fidelidad solo aplican con un miembro asignado (RF-207).</remarks>
    public void RecalcularPromociones(IReadOnlyCollection<Promocion> promociones, int sucursalId, DateTimeOffset ahoraLocal)
    {
        ArgumentNullException.ThrowIfNull(promociones);
        if (Estado != EstadoVenta.EnCurso)
            return;

        var vigentes = promociones.Where(p => p.EstaVigente(sucursalId, ahoraLocal) && (TieneFidelidad || !p.SoloFidelidad)).ToList();

        foreach (var grupo in _lineas.Where(l => l.EstaActiva).GroupBy(l => l.ArticuloId))
        {
            var lineas = grupo.OrderBy(l => l.NumeroLinea).ToList();
            var desactivada = lineas.Any(l => l.PromocionDesactivada);

            foreach (var linea in lineas)
            {
                linea.QuitarPromocion(desactivada);
                if (linea.ImporteEtiqueta is null && !linea.EsCotizada)
                    linea.EstablecerPrecio(PrecioAutomatico(linea));
            }

            var primera = lineas[0];

            // Lo cotizado se respeta tal cual: el cliente ya tiene ese precio por escrito y no se le mejora ni se le empeora.
            if (desactivada || lineas.Any(l => l.DescuentoManual > 0 || l.EsCotizada))
                continue;

            var candidatas = vigentes.Where(p => p.AplicaA(primera.ArticuloId, primera.DepartamentoId, primera.CategoriaId, primera.MarcaId)).ToList();
            if (candidatas.Count == 0)
                continue;

            var cantidad = lineas.Sum(l => l.Cantidad);
            var brutoDetalle = lineas.Sum(l => l.ImporteEtiqueta ?? Redondear(l.Cantidad * l.PrecioDetalle));
            var brutoActual = lineas.Sum(l => l.ImporteBruto);
            var precioUnitario = lineas.All(l => l.ImporteEtiqueta is null) ? primera.PrecioDetalle : brutoDetalle / cantidad;

            var mejor = candidatas
                .Select(p => (Promocion: p, Descuento: MotorPromociones.CalcularDescuento(p, cantidad, precioUnitario, brutoDetalle)))
                .Where(x => x.Descuento > 0)
                .OrderByDescending(x => x.Descuento)
                .ThenBy(x => x.Promocion.Codigo, StringComparer.Ordinal)
                .FirstOrDefault();

            if (mejor.Promocion is null || brutoDetalle - mejor.Descuento >= brutoActual)
                continue;

            // Las ofertas se calculan sobre el precio de detalle: el grupo deja el precio por mayor.
            var detalle = new PrecioDeterminado(ListaPrecio.Detalle, primera.PrecioDetalle, MotivoPrecio.PrecioDetalle, false);
            foreach (var linea in lineas.Where(l => l.ImporteEtiqueta is null))
                linea.EstablecerPrecio(detalle);

            var partes = MotorPromociones.Prorratear(mejor.Descuento, lineas.Select(l => l.ImporteBruto).ToList());
            for (var i = 0; i < lineas.Count; i++)
                lineas[i].AsignarPromocion(mejor.Promocion, partes[i]);
        }

        ProrratearDescuentoFactura();
    }

    /// <summary>Desactiva la oferta del artículo en esta venta (con permiso, RF-124) para que admita descuento manual.</summary>
    public void DesactivarPromocion(int numeroLinea, DateTimeOffset ahora)
    {
        AsegurarEditable();
        var linea = LineaActiva(numeroLinea);
        if (!linea.TienePromocionActiva)
            throw new ReglaVentaExcepcion(CodigoErrorVenta.DescuentoInvalido, $"La línea {numeroLinea} no tiene una oferta aplicada.");

        foreach (var delArticulo in _lineas.Where(l => l.EstaActiva && l.ArticuloId == linea.ArticuloId))
        {
            delArticulo.QuitarPromocion(desactivada: true);
            if (delArticulo.ImporteEtiqueta is null)
                delArticulo.EstablecerPrecio(PrecioAutomatico(delArticulo));
        }

        ProrratearDescuentoFactura();
        ActualizadaEn = ahora;
    }

    /// <summary>Valida y calcula un descuento de línea sin aplicarlo, para revisar el tope del autorizador (RF-202).</summary>
    public VistaPreviaDescuento PrevisualizarDescuentoLinea(int numeroLinea, TipoDescuento tipo, decimal valor)
    {
        AsegurarEditable();
        var linea = LineaActiva(numeroLinea);

        if (!linea.PermiteDescuentoManual)
            throw new ReglaVentaExcepcion(CodigoErrorVenta.DescuentoNoPermitido, $"{linea.Descripcion} es de un departamento que no admite descuento manual; solo ofertas.");
        if (linea.TienePromocionActiva)
            throw new ReglaVentaExcepcion(CodigoErrorVenta.ArticuloEnOferta,
                $"{linea.Descripcion} tiene la oferta {linea.PromocionCodigo}. Desactívela para aplicar un descuento manual.");

        var baseDescuento = linea.ImporteBruto;
        var monto = CalcularMontoDescuento(tipo, valor, baseDescuento);
        return new VistaPreviaDescuento(monto, PorcentajeDe(monto, baseDescuento), baseDescuento);
    }

    /// <summary>Descuento por monto o porcentaje a una línea (RF-199), con motivo y quién lo autorizó (RF-203).</summary>
    public LineaVenta AplicarDescuentoLinea(int numeroLinea, TipoDescuento tipo, decimal valor, string motivo, int? autorizadoPorId, string? autorizadoPorNombre,
        DateTimeOffset ahora)
    {
        if (string.IsNullOrWhiteSpace(motivo))
            throw new ReglaVentaExcepcion(CodigoErrorVenta.MotivoRequerido, "Indique el motivo del descuento.");

        var vista = PrevisualizarDescuentoLinea(numeroLinea, tipo, valor);
        var linea = LineaActiva(numeroLinea);
        linea.AplicarDescuentoManual(vista.Monto, tipo, valor, Validar.Texto(motivo, "Motivo", LargoMaximoMotivo),
            autorizadoPorId, Validar.TextoOpcional(autorizadoPorNombre, "Autorizado por", LargoMaximoUsuario));

        ProrratearDescuentoFactura();
        ActualizadaEn = ahora;
        return linea;
    }

    public void QuitarDescuentoLinea(int numeroLinea, DateTimeOffset ahora)
    {
        AsegurarEditable();
        LineaActiva(numeroLinea).QuitarDescuentoManual();
        ProrratearDescuentoFactura();
        ActualizadaEn = ahora;
    }

    /// <summary>
    /// Descuento a la factura por monto o porcentaje (RF-200), a todas las líneas o a las elegidas (RF-201). Se prorratea por
    /// línea al centavo; las líneas en oferta o de departamentos sin descuento manual quedan fuera y se informan (RF-204).
    /// </summary>
    public ResultadoDescuentoFactura AplicarDescuentoFactura(TipoDescuento tipo, decimal valor, IReadOnlyCollection<int>? lineas, string motivo,
        int? autorizadoPorId, string? autorizadoPorNombre, DateTimeOffset ahora)
    {
        AsegurarEditable();
        if (string.IsNullOrWhiteSpace(motivo))
            throw new ReglaVentaExcepcion(CodigoErrorVenta.MotivoRequerido, "Indique el motivo del descuento.");

        var seleccion = lineas is { Count: > 0 } ? lineas.Distinct().Order().ToList() : null;
        var (elegibles, excluidas) = ClasificarParaDescuentoFactura(seleccion);
        if (elegibles.Count == 0)
            throw new ReglaVentaExcepcion(CodigoErrorVenta.DescuentoInvalido,
                "Ninguna línea admite el descuento: están en oferta o su departamento no admite descuento manual.");

        var baseTotal = elegibles.Sum(BaseDescuentoFactura);
        var monto = CalcularMontoDescuento(tipo, valor, baseTotal);

        DescuentoFacturaTipo = tipo;
        DescuentoFacturaValor = valor;
        DescuentoFacturaLineas = seleccion is null ? null : string.Join(',', seleccion);
        MotivoDescuentoFactura = Validar.Texto(motivo, "Motivo", LargoMaximoMotivo);
        DescuentoFacturaAutorizadoPorId = autorizadoPorId;
        DescuentoFacturaAutorizadoPorNombre = Validar.TextoOpcional(autorizadoPorNombre, "Autorizado por", LargoMaximoUsuario);

        ProrratearDescuentoFactura();
        ActualizadaEn = ahora;
        return new ResultadoDescuentoFactura(_lineas.Sum(l => l.DescuentoFactura), PorcentajeDe(monto, baseTotal), excluidas);
    }

    /// <summary>Calcula el descuento a la factura sin aplicarlo, para revisar el tope del autorizador (RF-202).</summary>
    public VistaPreviaDescuento PrevisualizarDescuentoFactura(TipoDescuento tipo, decimal valor, IReadOnlyCollection<int>? lineas)
    {
        AsegurarEditable();
        var seleccion = lineas is { Count: > 0 } ? lineas.Distinct().Order().ToList() : null;
        var (elegibles, _) = ClasificarParaDescuentoFactura(seleccion);
        if (elegibles.Count == 0)
            throw new ReglaVentaExcepcion(CodigoErrorVenta.DescuentoInvalido,
                "Ninguna línea admite el descuento: están en oferta o su departamento no admite descuento manual.");

        var baseTotal = elegibles.Sum(BaseDescuentoFactura);
        var monto = CalcularMontoDescuento(tipo, valor, baseTotal);
        return new VistaPreviaDescuento(monto, PorcentajeDe(monto, baseTotal), baseTotal);
    }

    public void QuitarDescuentoFactura(DateTimeOffset ahora)
    {
        AsegurarEditable();
        DescuentoFacturaTipo = null;
        DescuentoFacturaValor = null;
        DescuentoFacturaLineas = null;
        MotivoDescuentoFactura = null;
        DescuentoFacturaAutorizadoPorId = null;
        DescuentoFacturaAutorizadoPorNombre = null;
        ProrratearDescuentoFactura();
        ActualizadaEn = ahora;
    }

    private void ProrratearDescuentoFactura()
    {
        foreach (var linea in _lineas)
            linea.AsignarDescuentoFactura(0m);

        if (DescuentoFacturaTipo is not { } tipo || DescuentoFacturaValor is not { } valor)
            return;

        var seleccion = DescuentoFacturaLineas?
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(numero => int.Parse(numero, System.Globalization.CultureInfo.InvariantCulture))
            .ToList();
        var (elegibles, _) = ClasificarParaDescuentoFactura(seleccion);
        if (elegibles.Count == 0)
            return;

        var bases = elegibles.Select(BaseDescuentoFactura).ToList();
        if (tipo == TipoDescuento.Porcentaje)
        {
            for (var i = 0; i < elegibles.Count; i++)
                elegibles[i].AsignarDescuentoFactura(Redondear(bases[i] * valor / 100m));
            return;
        }

        var partes = MotorPromociones.Prorratear(Math.Min(valor, bases.Sum()), bases);
        for (var i = 0; i < elegibles.Count; i++)
            elegibles[i].AsignarDescuentoFactura(partes[i]);
    }

    private (List<LineaVenta> Elegibles, List<int> Excluidas) ClasificarParaDescuentoFactura(IReadOnlyCollection<int>? seleccion)
    {
        var candidatas = _lineas
            .Where(l => l.EstaActiva && (seleccion is null || seleccion.Contains(l.NumeroLinea)))
            .OrderBy(l => l.NumeroLinea)
            .ToList();
        var elegibles = candidatas.Where(l => l.PermiteDescuentoManual && !l.TienePromocionActiva).ToList();
        var excluidas = candidatas.Except(elegibles).Select(l => l.NumeroLinea).ToList();
        return (elegibles, excluidas);
    }

    private static decimal BaseDescuentoFactura(LineaVenta linea) => linea.ImporteBruto - linea.DescuentoPromocion - linea.DescuentoManual;

    private static decimal CalcularMontoDescuento(TipoDescuento tipo, decimal valor, decimal baseDescuento)
    {
        if (!Enum.IsDefined(tipo))
            throw new ArgumentOutOfRangeException(nameof(tipo), tipo, "Tipo de descuento no válido.");

        if (tipo == TipoDescuento.Porcentaje)
        {
            if (valor is not (> 0 and <= 100))
                throw new ReglaVentaExcepcion(CodigoErrorVenta.DescuentoInvalido, "El porcentaje de descuento debe ser mayor que 0 y hasta 100.");
            return Redondear(baseDescuento * valor / 100m);
        }

        var monto = Redondear(valor);
        if (monto <= 0 || monto > baseDescuento)
            throw new ReglaVentaExcepcion(CodigoErrorVenta.DescuentoInvalido, $"El descuento debe ser mayor que cero y no superar {baseDescuento:N2}.");
        return monto;
    }

    private static decimal PorcentajeDe(decimal monto, decimal baseDescuento) =>
        baseDescuento <= 0 ? 0m : decimal.Round(monto / baseDescuento * 100m, 2, MidpointRounding.AwayFromZero);

    private static PrecioDeterminado PrecioAutomatico(LineaVenta linea) =>
        ReglasPrecio.Determinar(linea.CodigoInterno, linea.TipoArticulo, linea.CantidadMinimaMayor,
            new PreciosVigentes(linea.PrecioDetalle, linea.PrecioMayor), linea.Cantidad, SeleccionListaPrecio.Automatica);

    private static decimal Redondear(decimal valor) => decimal.Round(valor, 2, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Cobra la venta con uno o varios pagos (RF-211). Reglas: la devuelta solo sale de medios que la permiten, como el
    /// efectivo (RF-30); la moneda extranjera se convierte a la tasa indicada y la devuelta es en pesos (RF-212); bonos y
    /// tarjetas de regalo no se aceptan con comprobante de crédito fiscal, gubernamental o especial (RF-117, RF-119); una
    /// factura de consumo grande exige identificación (RF-26); si se paga con efectivo se aplica el redondeo configurado (RF-216).
    /// </summary>
    /// <param name="pasoRedondeoEfectivo">Múltiplo al que se redondea el total cuando hay efectivo (ej. 1 = al peso); 0 = sin redondeo.</param>
    public ResultadoCobro Cobrar(IReadOnlyList<PagoSolicitado> pagos, decimal pasoRedondeoEfectivo, decimal montoIdentificacion, int usuarioId, string usuarioNombre,
        DateTimeOffset ahora)
    {
        ArgumentNullException.ThrowIfNull(pagos);
        AsegurarEditable();

        if (!TieneLineasActivas)
            throw new ReglaVentaExcepcion(CodigoErrorVenta.SinLineas, "No hay artículos que cobrar.");

        // Un serializado sin serial solo sale de caja si se entrega después: el serial se captura en el despacho (RN-16).
        if (_lineas.FirstOrDefault(l => l.EstaActiva && l.SerialPendiente && CantidadEnEntregas(l.NumeroLinea) < l.Cantidad) is { } sinSerial)
            throw new ReglaVentaExcepcion(CodigoErrorVenta.RequiereSerial,
                $"{sinSerial.Descripcion} no tiene serial: márquelo para entrega o envío, o elimínelo y escanéelo con su serial.");

        if (RequiereIdentificacion(montoIdentificacion))
            throw new ReglaVentaExcepcion(CodigoErrorVenta.DocumentoRequerido,
                $"Una factura de consumo desde {SimboloMoneda}{montoIdentificacion:N2} exige la cédula o el RNC del cliente.");
        if (pagos.Count == 0)
            throw new ReglaVentaExcepcion(CodigoErrorVenta.PagoInvalido, "Agregue al menos una forma de pago.");

        var aplicados = pagos.Select(ValidarPago).ToList();

        // El cliente de régimen especial paga el total menos la retención de la Ley 32-23; la factura conserva su total.
        var total = CalcularTotales().TotalAPagar;

        var hayEfectivo = pagos.Any(p => p.Forma.PermiteDevuelta);
        var totalCobrado = hayEfectivo && pasoRedondeoEfectivo > 0
            ? decimal.Round(total / pasoRedondeoEfectivo, 0, MidpointRounding.AwayFromZero) * pasoRedondeoEfectivo
            : total;

        var pagado = aplicados.Sum();
        if (pagado < totalCobrado)
            throw new ReglaVentaExcepcion(CodigoErrorVenta.PagoInsuficiente, $"Falta cobrar {SimboloMoneda}{totalCobrado - pagado:N2}.");

        var devuelta = pagado - totalCobrado;
        var conDevuelta = pagos.Select((pago, indice) => pago.Forma.PermiteDevuelta ? aplicados[indice] : 0m).Sum();
        if (devuelta > conDevuelta)
            throw new ReglaVentaExcepcion(CodigoErrorVenta.DevueltaNoPermitida,
                "La devuelta solo aplica al efectivo: los demás medios deben cubrir su monto exacto.");

        for (var i = 0; i < pagos.Count; i++)
            _pagos.Add(PagoVenta.Crear(Id, Moneda, i + 1, pagos[i], aplicados[i]));

        Estado = EstadoVenta.Cobrada;
        CobradaEn = ahora;
        CobradaPorId = Validar.Id(usuarioId, "Usuario");
        CobradaPorNombre = Validar.Texto(usuarioNombre, "Usuario", LargoMaximoUsuario);
        TotalCobrado = totalCobrado;
        Devuelta = devuelta;
        RedondeoEfectivo = totalCobrado - total;
        ActualizadaEn = ahora;

        return new ResultadoCobro(total, totalCobrado, pagado, devuelta, RedondeoEfectivo, pagos.Any(p => p.Forma.AbreGaveta));
    }

    /// <returns>Monto en la moneda de la venta que abona el pago.</returns>
    private decimal ValidarPago(PagoSolicitado pago)
    {
        ArgumentNullException.ThrowIfNull(pago);
        var forma = pago.Forma;

        if (pago.MontoRecibido <= 0)
            throw new ReglaVentaExcepcion(CodigoErrorVenta.PagoInvalido, $"El monto de {forma.Nombre} debe ser mayor que cero.");
        if (forma.RequiereReferencia && string.IsNullOrWhiteSpace(pago.Referencia))
            throw new ReglaVentaExcepcion(CodigoErrorVenta.PagoInvalido, forma.Tipo switch
            {
                TipoFormaPago.Tarjeta => "Falta el número de aprobación de la tarjeta.",
                TipoFormaPago.Transferencia => "Falta el número de la transferencia.",
                TipoFormaPago.Cheque => "Falta el número del cheque.",
                TipoFormaPago.BonoRegalo or TipoFormaPago.TarjetaRegalo => "Escanee el serial del bono o tarjeta de regalo.",
                _ => $"Falta la referencia de {forma.Nombre}.",
            });
        if (forma.Tipo == TipoFormaPago.Puntos && !TieneFidelidad)
            throw new ReglaVentaExcepcion(CodigoErrorVenta.PagoInvalido, "Para canjear puntos asigne primero la cédula del cliente en el programa de fidelidad.");
        if (forma.RequiereBanco && pago.BancoId is null)
            throw new ReglaVentaExcepcion(CodigoErrorVenta.PagoInvalido, $"Seleccione el banco de {forma.Nombre}.");
        if (!forma.PermiteComprobanteFiscal && TipoComprobante != TipoComprobante.FacturaConsumo)
            throw new ReglaVentaExcepcion(CodigoErrorVenta.ComprobanteNoPermitido,
                $"{forma.Nombre} no se acepta con comprobante {ReglasComprobante.Nombre(TipoComprobante)}.");

        if (forma.Moneda == Moneda)
            return decimal.Round(pago.MontoRecibido, 2, MidpointRounding.AwayFromZero);

        if (pago.TasaCambio is not > 0)
            throw new ReglaVentaExcepcion(CodigoErrorVenta.PagoInvalido, $"No hay tasa de cambio del día para {forma.Moneda}.");

        return decimal.Round(pago.MontoRecibido * pago.TasaCambio.Value, 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>Una factura de consumo desde el monto indicado exige cédula o RNC del cliente (RF-26).</summary>
    public bool RequiereIdentificacion(decimal montoMinimo) =>
        TipoComprobante == TipoComprobante.FacturaConsumo
        && string.IsNullOrEmpty(ClienteDocumento)
        && CalcularTotales().Total >= montoMinimo;

    public bool LimiteCompraExcedido() => LimiteCompra is { } limite && CalcularTotales().Total > limite;

    public bool TieneLineasActivas => _lineas.Any(l => l.EstaActiva);

    /// <summary>Número de la cotización del Central que se facturó en esta venta; nulo si no vino de ninguna.</summary>
    public string? CotizacionNumero { get; private set; }

    /// <summary>
    /// La factura va sin ITBIS: siempre en régimen especial (E44), y en gubernamental (E45) solo cuando la entidad presentó
    /// su certificación de exención, que es lo que pide la Norma General 05-19.
    /// </summary>
    public bool ExentaDeImpuesto =>
        TipoComprobante == TipoComprobante.RegimenesEspeciales
        || (TipoComprobante == TipoComprobante.Gubernamental && CertificacionExencion is not null);

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
                var baseImponible = Fiscal.CalculoImpuestos.BaseDe(importe, l.PorcentajeImpuesto);
                return (l.PorcentajeImpuesto, l.IndicadorFacturacion, Base: baseImponible, Impuesto: importe - baseImponible, Total: importe);
            })
            .GroupBy(x => (x.PorcentajeImpuesto, x.IndicadorFacturacion))
            .OrderByDescending(g => g.Key.PorcentajeImpuesto)
            .ThenBy(g => g.Key.IndicadorFacturacion)
            .Select(g => new DesgloseImpuesto(g.Key.PorcentajeImpuesto, g.Key.IndicadorFacturacion, g.Sum(x => x.Base), g.Sum(x => x.Impuesto), g.Sum(x => x.Total)))
            .ToList();

        var cantidadArticulos = activas.Sum(l => l.PermiteDecimales ? 1m : l.Cantidad);
        var subtotal = desglose.Sum(d => d.Base);

        return new TotalesVenta(
            subtotal,
            desglose.Sum(d => d.Impuesto),
            desglose.Sum(d => d.Total),
            activas.Count,
            cantidadArticulos,
            desglose,
            activas.Sum(l => l.DescuentoTotal),
            // La retención se calcula sobre el subtotal, que ya viene con los descuentos aplicados.
            PorcentajeRetencion > 0 ? decimal.Round(subtotal * PorcentajeRetencion / 100m, 2, MidpointRounding.AwayFromZero) : 0m);
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
    public const int LargoMaximoSerial = 50;

    private LineaVenta()
    {
    }

    /// <summary>Serial del artículo vendido, para la garantía (RF-17).</summary>
    public string? Serial { get; private set; }

    /// <summary>Serializado que se entrega después: el serial se captura en el despacho (RF-54, RN-16).</summary>
    public bool SerialPendiente { get; private set; }

    // Oferta aplicada: queda registrada por línea para el reporte de efecto promocional (RF-210).
    public int? PromocionId { get; private set; }
    public string? PromocionCodigo { get; private set; }
    public string? PromocionNombre { get; private set; }

    /// <summary>Texto corto de la oferta para la columna Promo, ej. "2x1" o "-15%" (RF-143).</summary>
    public string? PromocionDescripcion { get; private set; }

    public decimal DescuentoPromocion { get; private set; }

    /// <summary>La oferta se desactivó con permiso para permitir un descuento manual (RF-124).</summary>
    public bool PromocionDesactivada { get; private set; }

    // Descuento manual de la línea (RF-199, RF-203)
    public decimal DescuentoManual { get; private set; }
    public TipoDescuento? DescuentoManualTipo { get; private set; }
    public decimal? DescuentoManualValor { get; private set; }
    public string? MotivoDescuento { get; private set; }
    public int? DescuentoAutorizadoPorId { get; private set; }
    public string? DescuentoAutorizadoPorNombre { get; private set; }

    /// <summary>Parte de esta línea del descuento a la factura, prorrateado para impuestos y e-CF (RF-200, RN-07).</summary>
    public decimal DescuentoFactura { get; private set; }

    public bool TienePromocionActiva => PromocionId is not null;

    public decimal DescuentoTotal => DescuentoPromocion + DescuentoManual + DescuentoFactura;

    public int VentaId { get; private set; }
    public int NumeroLinea { get; private set; }
    public int ArticuloId { get; private set; }
    public string CodigoInterno { get; private set; } = string.Empty;

    /// <summary>Código tal como se leyó (barras, proveedor, interno o etiqueta de balanza); al tocarlo se muestra el interno (RF-109).</summary>
    public string CodigoLeido { get; private set; } = string.Empty;

    public string Descripcion { get; private set; } = string.Empty;
    public TipoArticulo TipoArticulo { get; private set; }
    public int DepartamentoId { get; private set; }
    public int? CategoriaId { get; private set; }
    public int? MarcaId { get; private set; }
    public bool PermiteDescuentoManual { get; private set; }
    public string UnidadMedidaCodigo { get; private set; } = string.Empty;
    public bool PermiteDecimales { get; private set; }
    public int DecimalesCantidad { get; private set; }
    public int ImpuestoId { get; private set; }
    public decimal PorcentajeImpuesto { get; private set; }
    public int IndicadorFacturacion { get; private set; }

    /// <summary>Indicador de facturación de un e-CF exento, según la tabla de la DGII.</summary>
    public const int IndicadorExento = 4;

    // Lo que valía la línea con impuesto, guardado al exentarla para poder deshacerlo si cambia el comprobante.
    public decimal? PorcentajeImpuestoGravado { get; private set; }
    public int? IndicadorFacturacionGravado { get; private set; }
    public decimal? PrecioUnitarioGravado { get; private set; }
    public decimal? PrecioDetalleGravado { get; private set; }
    public decimal? PrecioMayorGravado { get; private set; }
    public decimal? PrecioMinimoGravado { get; private set; }
    public decimal? ImporteEtiquetaGravado { get; private set; }
    public decimal? DescuentoPromocionGravado { get; private set; }

    /// <summary>La línea se está facturando sin ITBIS, por ser de una factura de régimen especial.</summary>
    public bool Exenta => PorcentajeImpuestoGravado is not null;

    /// <summary>Su precio viene de una cotización: no se recalcula ni recibe ofertas.</summary>
    public bool EsCotizada => MotivoPrecio == MotivoPrecio.PrecioCotizado;

    /// <summary>Servicio y no bien, para el e-CF.</summary>
    public bool EsServicio { get; private set; }
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

    /// <summary>Importe antes de descuentos, con impuesto.</summary>
    public decimal ImporteBruto => EsReverso
        ? 0m
        : ImporteEtiqueta ?? decimal.Round(Cantidad * PrecioUnitario, 2, MidpointRounding.AwayFromZero);

    /// <summary>Importe a cobrar, con impuesto y después de ofertas y descuentos.</summary>
    public decimal ImporteConImpuesto => EsReverso ? 0m : Math.Max(0m, ImporteBruto - DescuentoTotal);

    internal static LineaVenta Crear(int ventaId, int numeroLinea, ArticuloParaVenta articulo, decimal cantidad, PrecioDeterminado precio,
        decimal? importeEtiqueta, bool leidaDeBalanza, string? serial, bool serialPendiente) =>
        new()
        {
            Serial = serial,
            SerialPendiente = serialPendiente,
            VentaId = ventaId,
            NumeroLinea = numeroLinea,
            ArticuloId = articulo.ArticuloId,
            CodigoInterno = articulo.CodigoInterno,
            CodigoLeido = articulo.CodigoLeido,
            Descripcion = articulo.Descripcion,
            TipoArticulo = articulo.Tipo,
            DepartamentoId = articulo.DepartamentoId,
            CategoriaId = articulo.CategoriaId,
            MarcaId = articulo.MarcaId,
            PermiteDescuentoManual = articulo.PermiteDescuentoManual,
            UnidadMedidaCodigo = articulo.UnidadMedidaCodigo,
            PermiteDecimales = articulo.PermiteDecimales,
            DecimalesCantidad = articulo.DecimalesCantidad,
            ImpuestoId = articulo.ImpuestoId,
            PorcentajeImpuesto = articulo.PorcentajeImpuesto,
            IndicadorFacturacion = articulo.IndicadorFacturacion,
            EsServicio = articulo.EsServicio,
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

    internal static LineaVenta CrearReverso(int ventaId, int numeroLinea, LineaVenta original) =>
        new()
        {
            Serial = original.Serial,
            VentaId = ventaId,
            NumeroLinea = numeroLinea,
            ArticuloId = original.ArticuloId,
            CodigoInterno = original.CodigoInterno,
            CodigoLeido = original.CodigoLeido,
            Descripcion = original.Descripcion,
            TipoArticulo = original.TipoArticulo,
            DepartamentoId = original.DepartamentoId,
            CategoriaId = original.CategoriaId,
            MarcaId = original.MarcaId,
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
        EstablecerPrecio(precio);
    }

    /// <summary>
    /// La línea se vende exenta (factura de régimen especial): el precio baja a su base y el impuesto sale. Se guardan los
    /// valores con impuesto para poder volver atrás si el comprobante cambia. Una línea sin impuesto también se marca, así
    /// toda la factura habla el mismo idioma.
    /// </summary>
    internal void Exentar()
    {
        if (Exenta)
            return;

        PorcentajeImpuestoGravado = PorcentajeImpuesto;
        IndicadorFacturacionGravado = IndicadorFacturacion;
        PrecioUnitarioGravado = PrecioUnitario;
        PrecioDetalleGravado = PrecioDetalle;
        PrecioMayorGravado = PrecioMayor;
        PrecioMinimoGravado = PrecioMinimo;
        ImporteEtiquetaGravado = ImporteEtiqueta;
        DescuentoPromocionGravado = DescuentoPromocion;

        var factor = 1m + PorcentajeImpuesto / 100m;
        PrecioUnitario = SinImpuesto(PrecioUnitario, factor);
        PrecioDetalle = SinImpuesto(PrecioDetalle, factor);
        PrecioMayor = PrecioMayor is { } mayor ? SinImpuesto(mayor, factor) : null;
        PrecioMinimo = PrecioMinimo is { } minimo ? SinImpuesto(minimo, factor) : null;
        ImporteEtiqueta = ImporteEtiqueta is { } etiqueta ? SinImpuesto(etiqueta, factor) : null;
        DescuentoPromocion = SinImpuesto(DescuentoPromocion, factor);
        PorcentajeImpuesto = 0m;
        IndicadorFacturacion = IndicadorExento;

        RecalcularDescuentoManual();
    }

    /// <summary>Vuelve al precio con impuesto: el comprobante dejó de ser de régimen especial.</summary>
    internal void Gravar()
    {
        if (!Exenta)
            return;

        PorcentajeImpuesto = PorcentajeImpuestoGravado!.Value;
        IndicadorFacturacion = IndicadorFacturacionGravado!.Value;
        PrecioUnitario = PrecioUnitarioGravado!.Value;
        PrecioDetalle = PrecioDetalleGravado!.Value;
        PrecioMayor = PrecioMayorGravado;
        PrecioMinimo = PrecioMinimoGravado;
        ImporteEtiqueta = ImporteEtiquetaGravado;
        DescuentoPromocion = DescuentoPromocionGravado!.Value;

        PorcentajeImpuestoGravado = null;
        IndicadorFacturacionGravado = null;
        PrecioUnitarioGravado = null;
        PrecioDetalleGravado = null;
        PrecioMayorGravado = null;
        PrecioMinimoGravado = null;
        ImporteEtiquetaGravado = null;
        DescuentoPromocionGravado = null;

        RecalcularDescuentoManual();
    }

    private static decimal SinImpuesto(decimal monto, decimal factor) =>
        factor == 1m ? monto : decimal.Round(monto / factor, 2, MidpointRounding.AwayFromZero);

    internal void EstablecerPrecio(PrecioDeterminado precio)
    {
        PrecioUnitario = precio.PrecioUnitario;
        Lista = precio.Lista;
        MotivoPrecio = precio.Motivo;
        RecalcularDescuentoManual();
    }

    internal void QuitarPromocion(bool desactivada)
    {
        PromocionId = null;
        PromocionCodigo = null;
        PromocionNombre = null;
        PromocionDescripcion = null;
        DescuentoPromocion = 0m;
        PromocionDesactivada = desactivada;
    }

    internal void AsignarPromocion(Promocion promocion, decimal descuento)
    {
        PromocionId = promocion.Id;
        PromocionCodigo = promocion.Codigo;
        PromocionNombre = promocion.Nombre;
        PromocionDescripcion = promocion.DescripcionCorta;
        DescuentoPromocion = descuento;
    }

    internal void AplicarDescuentoManual(decimal monto, TipoDescuento tipo, decimal valor, string motivo, int? autorizadoPorId, string? autorizadoPorNombre)
    {
        DescuentoManual = monto;
        DescuentoManualTipo = tipo;
        DescuentoManualValor = valor;
        MotivoDescuento = motivo;
        DescuentoAutorizadoPorId = autorizadoPorId;
        DescuentoAutorizadoPorNombre = autorizadoPorNombre;
    }

    /// <summary>Descuento que traía la línea en la cotización; se guarda como manual para que las ofertas no la pisen.</summary>
    internal void AplicarDescuentoCotizado(decimal monto)
    {
        DescuentoManual = Math.Min(monto, ImporteBruto);
        DescuentoManualTipo = TipoDescuento.Monto;
        DescuentoManualValor = DescuentoManual;
        MotivoDescuento = "Descuento cotizado";
    }

    internal void QuitarDescuentoManual()
    {
        DescuentoManual = 0m;
        DescuentoManualTipo = null;
        DescuentoManualValor = null;
        MotivoDescuento = null;
        DescuentoAutorizadoPorId = null;
        DescuentoAutorizadoPorNombre = null;
    }

    internal void AsignarDescuentoFactura(decimal monto) => DescuentoFactura = monto;

    /// <summary>Si cambia la cantidad o el precio, el porcentaje se recalcula y el monto nunca supera el nuevo importe.</summary>
    private void RecalcularDescuentoManual()
    {
        if (DescuentoManualTipo is not { } tipo || DescuentoManualValor is not { } valor)
            return;

        DescuentoManual = tipo == TipoDescuento.Porcentaje
            ? decimal.Round(ImporteBruto * valor / 100m, 2, MidpointRounding.AwayFromZero)
            : Math.Min(valor, ImporteBruto);
    }

    internal void MarcarAnulada() => Anulada = true;
}
