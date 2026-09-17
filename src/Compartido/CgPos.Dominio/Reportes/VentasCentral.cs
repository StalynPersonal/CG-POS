using CgPos.Dominio.Comun;
using CgPos.Dominio.Fiscal;
using CgPos.Dominio.Pagos;

namespace CgPos.Dominio.Reportes;

/// <summary>Qué documento es la fila: una factura cobrada o una nota de crédito, que resta.</summary>
public enum TipoComprobanteVenta
{
    Factura,
    NotaCredito,
}

/// <summary>
/// Modelo de lectura de los comprobantes de venta de todas las cajas, con lo necesario para los reportes fiscales y de gestión
/// (607, ITBIS, ventas y cuadres). Se arma con lo que informan las cajas: la caja es la autoridad sobre sus transacciones.
/// Una nota de crédito guarda sus montos en negativo, para que los totales se sumen sin casos especiales.
/// </summary>
public sealed class ComprobanteVentaCentral : Entidad
{
    public const int LargoMaximoNumero = 40;
    public const int LargoMaximoEncf = 13;
    public const int LargoMaximoTexto = 200;
    public const int LargoMaximoMoneda = 3;

    private readonly List<ImpuestoVentaCentral> _impuestos = [];
    private readonly List<PagoVentaCentral> _pagos = [];

    private ComprobanteVentaCentral()
    {
    }

    public TipoComprobanteVenta Tipo { get; private set; }
    public string Numero { get; private set; } = string.Empty;
    public Guid SucursalId { get; private set; }
    public Guid CajaId { get; private set; }
    /// <summary>Número del turno en la caja; nulo en una nota de crédito emitida fuera de un turno.</summary>
    public long? TurnoNumero { get; private set; }
    public string UsuarioNombre { get; private set; } = string.Empty;

    /// <summary>Momento del cobro o de la emisión de la nota, tal como lo registró la caja.</summary>
    public DateTimeOffset Fecha { get; private set; }

    /// <summary>Día de operación en hora local de la caja: es el que agrupan los reportes.</summary>
    public DateOnly FechaOperacion { get; private set; }

    public TipoComprobante TipoComprobanteFiscal { get; private set; }
    public string? Encf { get; private set; }

    /// <summary>e-NCF de la factura que modifica una nota de crédito (607).</summary>
    public string? EncfModificado { get; private set; }

    public TipoDocumentoIdentidad? ClienteTipoDocumento { get; private set; }
    public string? ClienteDocumento { get; private set; }
    public string? ClienteNombre { get; private set; }
    public string Moneda { get; private set; } = string.Empty;

    /// <summary>Base imponible más exento, sin impuesto ni descuento.</summary>
    public decimal Subtotal { get; private set; }

    public decimal Descuento { get; private set; }
    public decimal Impuesto { get; private set; }

    /// <summary>ITBIS retenido al cliente en una devolución fuera de plazo (RF-44).</summary>
    public decimal ImpuestoRetenido { get; private set; }

    public decimal Total { get; private set; }
    public int CantidadLineas { get; private set; }
    public DateTimeOffset RegistradoEn { get; private set; }

    public IReadOnlyList<ImpuestoVentaCentral> Impuestos => _impuestos;
    public IReadOnlyList<PagoVentaCentral> Pagos => _pagos;

    public static ComprobanteVentaCentral Registrar(TipoComprobanteVenta tipo, string numero, Guid sucursalId, Guid cajaId, long? turnoNumero,
        string? usuarioNombre, DateTimeOffset fecha, DateOnly fechaOperacion, TipoComprobante tipoFiscal, string? encf, string? encfModificado,
        TipoDocumentoIdentidad? clienteTipoDocumento, string? clienteDocumento, string? clienteNombre, string moneda, decimal subtotal, decimal descuento,
        decimal impuesto, decimal impuestoRetenido, decimal total, int cantidadLineas, DateTimeOffset ahora)
    {
        var signo = tipo == TipoComprobanteVenta.NotaCredito ? -1m : 1m;
        return new ComprobanteVentaCentral
        {
            Tipo = tipo,
            Numero = Validar.Texto(numero, "Número del comprobante", LargoMaximoNumero),
            SucursalId = Validar.Id(sucursalId, "Sucursal"),
            CajaId = Validar.Id(cajaId, "Caja"),
            TurnoNumero = turnoNumero,
            UsuarioNombre = Validar.TextoOpcional(usuarioNombre, "Usuario", LargoMaximoTexto) ?? string.Empty,
            Fecha = fecha,
            FechaOperacion = fechaOperacion,
            TipoComprobanteFiscal = tipoFiscal,
            Encf = Validar.TextoOpcional(encf, "e-NCF", LargoMaximoEncf),
            EncfModificado = Validar.TextoOpcional(encfModificado, "e-NCF modificado", LargoMaximoEncf),
            ClienteTipoDocumento = clienteTipoDocumento,
            ClienteDocumento = Validar.TextoOpcional(clienteDocumento, "Documento del cliente", LargoMaximoTexto),
            ClienteNombre = Validar.TextoOpcional(clienteNombre, "Nombre del cliente", LargoMaximoTexto),
            Moneda = Validar.Texto(moneda, "Moneda", LargoMaximoMoneda).ToUpperInvariant(),
            Subtotal = signo * subtotal,
            Descuento = signo * descuento,
            Impuesto = signo * impuesto,
            ImpuestoRetenido = signo * impuestoRetenido,
            Total = signo * total,
            CantidadLineas = cantidadLineas,
            RegistradoEn = ahora,
        };
    }

    public void AgregarImpuesto(decimal porcentaje, decimal baseImponible, decimal impuesto)
    {
        var signo = Tipo == TipoComprobanteVenta.NotaCredito ? -1m : 1m;
        _impuestos.Add(new ImpuestoVentaCentral
        {
            ComprobanteId = Id,
            Porcentaje = porcentaje,
            Base = signo * baseImponible,
            Impuesto = signo * impuesto,
        });
    }

    public void AgregarPago(TipoFormaPago tipo, string? formaPagoNombre, string? moneda, decimal monto)
    {
        var signo = Tipo == TipoComprobanteVenta.NotaCredito ? -1m : 1m;
        _pagos.Add(new PagoVentaCentral
        {
            ComprobanteId = Id,
            Tipo = tipo,
            FormaPagoNombre = Validar.TextoOpcional(formaPagoNombre, "Forma de pago", LargoMaximoTexto) ?? string.Empty,
            Moneda = Validar.TextoOpcional(moneda, "Moneda", LargoMaximoMoneda)?.ToUpperInvariant() ?? Moneda,
            Monto = signo * monto,
        });
    }
}

/// <summary>ITBIS por tasa del comprobante, para el reporte de impuestos y el 607.</summary>
public sealed class ImpuestoVentaCentral : Entidad
{
    public Guid ComprobanteId { get; internal set; }
    public decimal Porcentaje { get; internal set; }
    public decimal Base { get; internal set; }
    public decimal Impuesto { get; internal set; }
}

/// <summary>Lo cobrado por forma de pago, para el cuadre y el reporte de ventas.</summary>
public sealed class PagoVentaCentral : Entidad
{
    public Guid ComprobanteId { get; internal set; }
    public TipoFormaPago Tipo { get; internal set; }
    public string FormaPagoNombre { get; internal set; } = string.Empty;
    public string Moneda { get; internal set; } = string.Empty;
    public decimal Monto { get; internal set; }
}
