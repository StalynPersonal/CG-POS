using CgPos.Dominio.Catalogo;
using CgPos.Dominio.Comun;
using CgPos.Dominio.Fiscal;
using CgPos.Dominio.Ventas;

namespace CgPos.Dominio.Devoluciones;

/// <summary>Motivo seleccionable de una devolución (RF-232).</summary>
public sealed class MotivoDevolucion : Entidad
{
    public const int LargoMaximoNombre = 100;

    private MotivoDevolucion()
    {
    }

    public int Codigo { get; private set; }
    public string Nombre { get; private set; } = string.Empty;
    public bool Activo { get; private set; } = true;

    public static MotivoDevolucion Crear(int codigo, string nombre)
    {
        var motivo = new MotivoDevolucion
        {
            Codigo = Validar.Codigo(codigo, "Código de motivo"),
        };
        motivo.CambiarNombre(nombre);
        return motivo;
    }

    public void CambiarNombre(string nombre) => Nombre = Validar.Texto(nombre, "Nombre de motivo", LargoMaximoNombre);

    public void Activar() => Activo = true;

    public void Desactivar() => Activo = false;
}

public enum CodigoErrorDevolucion
{
    FacturaNoCobrada,
    SinLineas,
    LineaNoEncontrada,
    CantidadInvalida,
    CantidadExcedida,
    SerialRequerido,
    SerialNoCoincide,
    ClienteRequerido,
    MotivoRequerido,
    MontoInvalido,
    NotaCreditoConsumida,
    NotaCreditoVencida,
    SaldoInsuficiente,
    DevolucionInterna,
}

public sealed class ReglaDevolucionExcepcion(CodigoErrorDevolucion codigo, string mensaje) : Exception(mensaje)
{
    public CodigoErrorDevolucion Codigo { get; } = codigo;
}

public enum EstadoNotaCredito
{
    Vigente,
    Consumida,
    Vencida,
}

public sealed record LineaSolicitadaDevolucion(int NumeroLinea, decimal Cantidad, string? Serial = null);

/// <param name="ImporteFactura">Parte del importe de la línea de la factura (con ITBIS) ya devuelta.</param>
public sealed record DevueltoLinea(decimal Cantidad, decimal ImporteFactura);

public sealed record ClienteDevolucion(TipoDocumentoIdentidad? TipoDocumento, string Documento, string Nombre);

/// <summary>
/// Devolución de mercancía y su nota de crédito (M10). Parcial o total contra una factura cobrada, sin devolver más de lo vendido
/// (RF-41, RF-42); los serializados exigen el serial vendido (RF-45). Pasado el plazo configurado se retiene el ITBIS (RF-44).
/// El monto queda como saldo consumible en caja hasta su vencimiento (RF-36, RF-39); se emite como e-CF E34 (RF-227).
/// </summary>
/// <summary>Cómo se le devuelve el dinero al cliente (RF-123). En todos los casos se emite la nota de crédito E34 que exige la DGII.</summary>
public enum TipoReembolso
{
    /// <summary>Queda como saldo a favor en la nota de crédito, para consumirlo en otra compra.</summary>
    SaldoNotaCredito,

    Efectivo,

    /// <summary>Devolución a la tarjeta con la que se pagó, por el terminal.</summary>
    Tarjeta,

    /// <summary>Queda registrado para que contabilidad emita el cheque.</summary>
    Cheque,
}

public sealed class Devolucion : Entidad
{
    public const int LargoMaximoNumero = 30;
    public const int LargoMaximoNombre = 150;
    public const int LargoMaximoObservacion = 250;

    private readonly List<LineaDevolucion> _lineas = [];
    private readonly List<ConsumoNotaCredito> _consumos = [];

    private Devolucion()
    {
    }

    public string Numero { get; private set; } = string.Empty;
    public int SucursalId { get; private set; }
    public int CajaId { get; private set; }
    public int? TurnoId { get; private set; }
    public int UsuarioId { get; private set; }
    public string UsuarioNombre { get; private set; } = string.Empty;

    /// <summary>Venta de esta caja que se devuelve; nula cuando la factura se consultó al Central.</summary>
    public int? VentaOrigenId { get; private set; }
    public string VentaOrigenNumero { get; private set; } = string.Empty;
    public DateTimeOffset VentaOrigenCobradaEn { get; private set; }
    public TipoComprobante TipoComprobanteOrigen { get; private set; }

    /// <summary>e-NCF de la factura que se modifica; nulo si la factura no tenía e-CF.</summary>
    public string? EncfOrigen { get; private set; }

    public TipoDocumentoIdentidad? ClienteTipoDocumento { get; private set; }
    public string ClienteDocumento { get; private set; } = string.Empty;
    public string ClienteNombre { get; private set; } = string.Empty;

    public int MotivoCodigo { get; private set; }
    public string MotivoNombre { get; private set; } = string.Empty;
    public string? Observacion { get; private set; }

    /// <summary>Encargado que autorizó la devolución; sale impreso en la nota (RF-162).</summary>
    public int? AutorizadoPorId { get; private set; }

    public string? AutorizadoPorNombre { get; private set; }

    public bool RetieneImpuesto { get; private set; }

    /// <summary>
    /// Nota de crédito interna (sin comprobante fiscal): solo corrige un problema en la factura. No devuelve dinero, no sirve como
    /// forma de pago y no va a la DGII; sale en los reportes de ventas y exige la autorización de un supervisor.
    /// </summary>
    public bool EsInterna { get; private set; }

    /// <summary>La devolución completa lo que quedaba de la factura.</summary>
    public bool EsTotal { get; private set; }

    public decimal Subtotal { get; private set; }
    public decimal Impuesto { get; private set; }
    public decimal ImpuestoRetenido { get; private set; }

    /// <summary>Monto de la nota de crédito.</summary>
    public decimal Total { get; private set; }

    public decimal Saldo { get; private set; }

    /// <summary>Moneda de la factura de origen: la de los montos de la nota.</summary>
    public string Moneda { get; private set; } = string.Empty;

    public string SimboloMoneda { get; private set; } = string.Empty;

    /// <summary>Puntos de fidelidad reversados de la compra original (RF-244, RN-21).</summary>
    public int PuntosReversados { get; private set; }

    /// <summary>Día de emisión en la zona horaria de la caja: la vigencia se cuenta desde aquí.</summary>
    public DateOnly FechaEmision { get; private set; }

    public DateTimeOffset CreadaEn { get; private set; }

    /// <summary>e-NCF de la nota de crédito (E34); también es el código para consumirla en caja (RF-57).</summary>
    public string? Encf { get; private set; }

    /// <summary>Cómo se le devolvió el dinero al cliente (RF-123).</summary>
    public TipoReembolso Reembolso { get; private set; }

    /// <summary>Autorización del terminal, número del cheque o lo que identifique el reembolso.</summary>
    public string? ReembolsoReferencia { get; private set; }

    /// <summary>Banco, tarjeta o quien recibió, según el tipo de reembolso.</summary>
    public string? ReembolsoDetalle { get; private set; }

    public DateTimeOffset? ReembolsadaEn { get; private set; }

    public IReadOnlyList<LineaDevolucion> Lineas => _lineas;
    public IReadOnlyList<ConsumoNotaCredito> Consumos => _consumos;

    /// <param name="sucursalId">Sucursal que emite la nota, que no es la que vendió cuando la factura vino del Central.</param>
    /// <param name="cajaId">Caja que emite la nota: es la que tiene el rango de e-NCF y el certificado.</param>
    public static Devolucion Registrar(FacturaParaDevolver venta, int sucursalId, int cajaId, string? encfOrigen,
        IReadOnlyCollection<LineaSolicitadaDevolucion> solicitadas,
        IReadOnlyDictionary<int, DevueltoLinea> devuelto, ClienteDevolucion? cliente, int? motivoCodigo, string? motivoNombre, string? observacion,
        string numero, int? turnoId, int usuarioId, string usuarioNombre, int? autorizadoPorId, string? autorizadoPorNombre,
        int diasRetencionImpuesto, DateOnly hoy, DateTimeOffset ahora, TimeZoneInfo zonaHoraria, bool esInterna = false)
    {
        ArgumentNullException.ThrowIfNull(zonaHoraria);
        ArgumentNullException.ThrowIfNull(venta);

        var cobradaEn = venta.CobradaEn;

        var pedidas = solicitadas.Where(s => s.Cantidad != 0m).OrderBy(s => s.NumeroLinea).ToList();
        if (pedidas.Count == 0)
            throw new ReglaDevolucionExcepcion(CodigoErrorDevolucion.SinLineas, "Indique la cantidad a devolver de al menos un artículo.");
        if (pedidas.GroupBy(p => p.NumeroLinea).Any(grupo => grupo.Count() > 1))
            throw new ReglaDevolucionExcepcion(CodigoErrorDevolucion.CantidadInvalida, "Cada línea de la factura se indica una sola vez.");

        // La nota interna no es un comprobante fiscal: no exige identificar al cliente, pero se guarda el que traiga la factura.
        if (!esInterna && (cliente is null || !DocumentoIdentidad.Validar(cliente.Documento).EsValido || string.IsNullOrWhiteSpace(cliente.Nombre)))
            throw new ReglaDevolucionExcepcion(CodigoErrorDevolucion.ClienteRequerido, "La nota de crédito requiere la cédula o el RNC válido y el nombre del cliente.");
        if (motivoCodigo is null or < 1 || string.IsNullOrWhiteSpace(motivoNombre))
            throw new ReglaDevolucionExcepcion(CodigoErrorDevolucion.MotivoRequerido, "Seleccione el motivo de la devolución.");

        // El plazo se cuenta en días de la zona horaria de la caja.
        var fechaVenta = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(cobradaEn, zonaHoraria).DateTime);
        var devolucion = new Devolucion
        {
            Numero = Validar.Texto(numero, "Número de la devolución", LargoMaximoNumero),
            SucursalId = Validar.Id(sucursalId, "Sucursal"),
            CajaId = Validar.Id(cajaId, "Caja"),
            TurnoId = turnoId,
            UsuarioId = Validar.Id(usuarioId, "Usuario"),
            UsuarioNombre = Validar.Texto(usuarioNombre, "Usuario", LargoMaximoNombre),
            VentaOrigenId = venta.VentaId,
            VentaOrigenNumero = venta.NumeroTransaccion,
            VentaOrigenCobradaEn = cobradaEn,
            TipoComprobanteOrigen = venta.TipoComprobante,
            Moneda = venta.Moneda,
            SimboloMoneda = venta.SimboloMoneda,
            EncfOrigen = encfOrigen,
            ClienteTipoDocumento = cliente?.TipoDocumento,
            ClienteDocumento = DocumentoIdentidad.Normalizar(cliente?.Documento ?? string.Empty),
            ClienteNombre = Validar.TextoOpcional(cliente?.Nombre, "Cliente", LargoMaximoNombre) ?? string.Empty,
            MotivoCodigo = motivoCodigo.Value,
            MotivoNombre = Validar.Texto(motivoNombre, "Motivo", MotivoDevolucion.LargoMaximoNombre),
            Observacion = Validar.TextoOpcional(observacion, "Observación", LargoMaximoObservacion),
            AutorizadoPorId = autorizadoPorId,
            AutorizadoPorNombre = Validar.TextoOpcional(autorizadoPorNombre, "Autorizado por", LargoMaximoNombre),
            RetieneImpuesto = hoy.DayNumber - fechaVenta.DayNumber > diasRetencionImpuesto,
            EsInterna = esInterna,
            CreadaEn = ahora,
            FechaEmision = hoy,
        };

        foreach (var pedida in pedidas)
        {
            var linea = venta.Lineas.FirstOrDefault(l => l.NumeroLinea == pedida.NumeroLinea)
                ?? throw new ReglaDevolucionExcepcion(CodigoErrorDevolucion.LineaNoEncontrada, $"La línea {pedida.NumeroLinea} no existe en la factura.");

            if (pedida.Cantidad < 0 || (!linea.PermiteDecimales && pedida.Cantidad != decimal.Truncate(pedida.Cantidad)))
                throw new ReglaDevolucionExcepcion(CodigoErrorDevolucion.CantidadInvalida, $"{linea.Descripcion}: la cantidad a devolver no es válida.");

            var previo = devuelto.GetValueOrDefault(linea.NumeroLinea) ?? new DevueltoLinea(0m, 0m);
            var disponible = linea.Cantidad - previo.Cantidad;
            if (pedida.Cantidad > disponible)
                throw new ReglaDevolucionExcepcion(CodigoErrorDevolucion.CantidadExcedida, disponible <= 0
                    ? $"{linea.Descripcion} ya fue devuelto completo."
                    : $"{linea.Descripcion}: solo quedan {disponible:0.###} por devolver.");

            string? serial = null;
            if (linea.TipoArticulo == TipoArticulo.Serializado)
            {
                if (string.IsNullOrWhiteSpace(pedida.Serial))
                    throw new ReglaDevolucionExcepcion(CodigoErrorDevolucion.SerialRequerido, $"{linea.Descripcion}: escanee el serial del artículo que devuelve.");
                // El serial vendido en caja se valida; el de un artículo entregado después se captura en el despacho (RF-46, RF-56).
                if (linea.Serial is not null && !string.Equals(pedida.Serial.Trim(), linea.Serial, StringComparison.OrdinalIgnoreCase))
                    throw new ReglaDevolucionExcepcion(CodigoErrorDevolucion.SerialNoCoincide, $"{linea.Descripcion}: el serial no coincide con el vendido en la factura.");
                serial = linea.Serial ?? pedida.Serial.Trim().ToUpperInvariant();
            }

            // La última devolución de la línea toma el resto exacto, para no dejar centavos por redondeo.
            var importeFactura = pedida.Cantidad == disponible
                ? linea.ImporteConImpuesto - previo.ImporteFactura
                : Redondear(linea.ImporteConImpuesto / linea.Cantidad * pedida.Cantidad);

            devolucion._lineas.Add(LineaDevolucion.Crear(devolucion.Id, linea, pedida.Cantidad, importeFactura, devolucion.RetieneImpuesto, serial));
        }

        devolucion.EsTotal = venta.Lineas.All(l =>
            l.Cantidad - (devuelto.GetValueOrDefault(l.NumeroLinea)?.Cantidad ?? 0m) - devolucion._lineas.Where(d => d.NumeroLineaOrigen == l.NumeroLinea).Sum(d => d.Cantidad) <= 0m);

        devolucion.Subtotal = devolucion._lineas.Sum(l => l.Base);
        devolucion.Impuesto = devolucion._lineas.Sum(l => l.Impuesto);
        devolucion.ImpuestoRetenido = devolucion._lineas.Sum(l => l.ImpuestoRetenido);
        devolucion.Total = devolucion._lineas.Sum(l => l.Importe);

        // La interna nace sin saldo: es un ajuste, no dinero a favor del cliente.
        devolucion.Saldo = esInterna ? 0m : devolucion.Total;

        if (devolucion.Total <= 0)
            throw new ReglaDevolucionExcepcion(CodigoErrorDevolucion.MontoInvalido, "La devolución no tiene monto.");

        return devolucion;
    }

    public void AsignarComprobante(string encf)
    {
        if (EsInterna)
            throw new InvalidOperationException("Una nota de crédito interna no lleva comprobante fiscal.");

        Encf = Validar.Texto(encf, "e-NCF", DocumentoElectronico.LargoEncf);
    }

    /// <summary>
    /// El cliente se lleva el dinero en vez del saldo a favor (RF-123): la nota de crédito se emite igual, pero queda sin saldo
    /// porque ya se le pagó en efectivo, a su tarjeta o con un cheque.
    /// </summary>
    public void RegistrarReembolso(TipoReembolso tipo, string? referencia, string? detalle, DateTimeOffset ahora)
    {
        if (tipo == TipoReembolso.SaldoNotaCredito)
            throw new ArgumentException("El saldo a favor no es un reembolso.", nameof(tipo));
        if (EsInterna)
            throw new ReglaDevolucionExcepcion(CodigoErrorDevolucion.DevolucionInterna, "Una nota de crédito interna no devuelve dinero: solo ajusta la factura.");
        if (!Enum.IsDefined(tipo))
            throw new ArgumentOutOfRangeException(nameof(tipo), tipo, "Tipo de reembolso no válido.");

        Reembolso = tipo;
        ReembolsoReferencia = Validar.TextoOpcional(referencia, "Referencia del reembolso", LargoMaximoNombre);
        ReembolsoDetalle = Validar.TextoOpcional(detalle, "Detalle del reembolso", LargoMaximoObservacion);
        ReembolsadaEn = ahora;
        Saldo = 0m;
    }

    /// <summary>
    /// Último día en que se puede consumir con la vigencia configurada hoy (días desde la emisión). La vigencia no se guarda en la nota:
    /// si el negocio sube los días, una nota ya vencida vuelve a poder usarse.
    /// </summary>
    public DateOnly VenceEn(int diasVigencia) => VigenciaNotaCredito.VenceEn(FechaEmision, diasVigencia);

    public EstadoNotaCredito EstadoSaldo(DateOnly hoy, int diasVigencia) =>
        Saldo <= 0m ? EstadoNotaCredito.Consumida : hoy > VenceEn(diasVigencia) ? EstadoNotaCredito.Vencida : EstadoNotaCredito.Vigente;

    public void RegistrarReversoPuntos(int puntos)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(puntos);
        PuntosReversados = puntos;
    }

    /// <summary>Consume saldo de la nota como forma de pago de otra venta (RF-36, RF-38). Devuelve el saldo que queda (RF-43).</summary>
    public decimal Consumir(int ventaId, string ventaNumero, int cajaId, decimal monto, DateOnly hoy, int diasVigencia, DateTimeOffset ahora)
    {
        var redondeado = Redondear(monto);
        if (redondeado <= 0m)
            throw new ReglaDevolucionExcepcion(CodigoErrorDevolucion.MontoInvalido, "El monto a consumir de la nota de crédito debe ser mayor que cero.");
        if (EsInterna)
            throw new ReglaDevolucionExcepcion(CodigoErrorDevolucion.DevolucionInterna,
                $"La nota de crédito {Numero} es interna: solo ajusta la factura, no se usa como forma de pago.");

        switch (EstadoSaldo(hoy, diasVigencia))
        {
            case EstadoNotaCredito.Consumida:
                throw new ReglaDevolucionExcepcion(CodigoErrorDevolucion.NotaCreditoConsumida, $"La nota de crédito {Encf ?? Numero} ya fue consumida.");
            case EstadoNotaCredito.Vencida:
                throw new ReglaDevolucionExcepcion(CodigoErrorDevolucion.NotaCreditoVencida, $"La nota de crédito {Encf ?? Numero} venció el {VenceEn(diasVigencia):dd/MM/yyyy}.");
        }

        if (redondeado > Saldo)
            throw new ReglaDevolucionExcepcion(CodigoErrorDevolucion.SaldoInsuficiente, $"La nota de crédito {Encf ?? Numero} solo tiene {SimboloMoneda}{Saldo:N2} disponibles.");

        Saldo -= redondeado;
        _consumos.Add(ConsumoNotaCredito.Crear(Id, ventaId, ventaNumero, cajaId, redondeado, Saldo, ahora));
        return Saldo;
    }

    internal static decimal Redondear(decimal valor) => decimal.Round(valor, 2, MidpointRounding.AwayFromZero);
}

public sealed class LineaDevolucion : Entidad
{
    private LineaDevolucion()
    {
    }

    public int DevolucionId { get; private set; }
    public int NumeroLineaOrigen { get; private set; }
    public int ArticuloId { get; private set; }
    public string CodigoInterno { get; private set; } = string.Empty;
    public string CodigoLeido { get; private set; } = string.Empty;
    public string Descripcion { get; private set; } = string.Empty;
    public string UnidadMedidaCodigo { get; private set; } = string.Empty;
    public int DecimalesCantidad { get; private set; }
    public decimal Cantidad { get; private set; }

    /// <summary>Precio neto por unidad con ITBIS, tal como se cobró.</summary>
    public decimal PrecioUnitario { get; private set; }

    public decimal PorcentajeImpuesto { get; private set; }
    public int IndicadorFacturacion { get; private set; }
    public bool EsServicio { get; private set; }

    /// <summary>Parte del importe de la factura (con ITBIS) que corresponde a lo devuelto.</summary>
    public decimal ImporteFactura { get; private set; }

    public decimal Base { get; private set; }
    public decimal Impuesto { get; private set; }
    public decimal ImpuestoRetenido { get; private set; }

    /// <summary>Lo que acredita la nota de crédito por esta línea.</summary>
    public decimal Importe { get; private set; }

    public string? Serial { get; private set; }

    internal static LineaDevolucion Crear(int devolucionId, LineaFacturaParaDevolver linea, decimal cantidad, decimal importeFactura, bool retieneImpuesto,
        string? serial)
    {
        var baseImponible = Devolucion.Redondear(importeFactura / (1m + linea.PorcentajeImpuesto / 100m));
        var impuesto = importeFactura - baseImponible;

        return new LineaDevolucion
        {
            DevolucionId = devolucionId,
            NumeroLineaOrigen = linea.NumeroLinea,
            ArticuloId = linea.ArticuloId,
            CodigoInterno = linea.CodigoInterno,
            CodigoLeido = linea.CodigoLeido,
            Descripcion = linea.Descripcion,
            UnidadMedidaCodigo = linea.UnidadMedidaCodigo,
            DecimalesCantidad = linea.DecimalesCantidad,
            Cantidad = cantidad,
            PrecioUnitario = Devolucion.Redondear(linea.ImporteConImpuesto / linea.Cantidad),
            PorcentajeImpuesto = linea.PorcentajeImpuesto,
            IndicadorFacturacion = linea.IndicadorFacturacion,
            EsServicio = linea.EsServicio,
            ImporteFactura = importeFactura,
            Base = baseImponible,
            Impuesto = retieneImpuesto ? 0m : impuesto,
            ImpuestoRetenido = retieneImpuesto ? impuesto : 0m,
            Importe = retieneImpuesto ? baseImponible : importeFactura,
            Serial = serial,
        };
    }
}

/// <summary>Uso del saldo de una nota de crédito en una venta (RF-38): monto, caja, fecha y saldo que quedó.</summary>
public sealed class ConsumoNotaCredito : Entidad
{
    private ConsumoNotaCredito()
    {
    }

    public int DevolucionId { get; private set; }
    public int VentaId { get; private set; }
    public string VentaNumero { get; private set; } = string.Empty;
    public int CajaId { get; private set; }
    public decimal Monto { get; private set; }
    public decimal SaldoRestante { get; private set; }
    public DateTimeOffset Fecha { get; private set; }

    internal static ConsumoNotaCredito Crear(int devolucionId, int ventaId, string ventaNumero, int cajaId, decimal monto, decimal saldoRestante, DateTimeOffset fecha) =>
        new()
        {
            DevolucionId = devolucionId,
            VentaId = ventaId,
            VentaNumero = ventaNumero,
            CajaId = cajaId,
            Monto = monto,
            SaldoRestante = saldoRestante,
            Fecha = fecha,
        };
}

/// <summary>Vigencia de una nota de crédito: días desde su emisión, según lo configurado en el Central en el momento de usarla.</summary>
public static class VigenciaNotaCredito
{
    public static DateOnly VenceEn(DateOnly fechaEmision, int diasVigencia)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(diasVigencia, 1);
        return fechaEmision.AddDays(diasVigencia);
    }
}
