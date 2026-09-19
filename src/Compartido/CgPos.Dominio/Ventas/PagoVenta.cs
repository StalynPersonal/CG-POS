using CgPos.Dominio.Comun;
using CgPos.Dominio.Pagos;

namespace CgPos.Dominio.Ventas;

/// <summary>Datos de la forma de pago del maestro que el cobro necesita (RF-184).</summary>
public sealed record FormaPagoParaCobro(
    int FormaPagoId,
    string Codigo,
    string Nombre,
    TipoFormaPago Tipo,
    string Moneda,
    bool PermiteDevuelta,
    bool RequiereReferencia,
    bool RequiereBanco,
    bool PermiteComprobanteFiscal,
    bool AbreGaveta);

/// <param name="MontoRecibido">En la moneda de la forma de pago.</param>
/// <param name="TasaCambio">Pesos por unidad de moneda extranjera, la tasa del día que publica el Central (RF-212).</param>
/// <param name="Referencia">Aprobación de tarjeta, número de transferencia o cheque, serial del bono (RF-32).</param>
/// <param name="AprobacionManual">Tarjeta aprobada a mano por contingencia de la pasarela; queda para conciliar (RF-213).</param>
public sealed record PagoSolicitado(
    FormaPagoParaCobro Forma,
    decimal MontoRecibido,
    decimal? TasaCambio = null,
    string? Referencia = null,
    int? BancoId = null,
    string? BancoNombre = null,
    int? TipoTarjetaId = null,
    string? TipoTarjetaNombre = null,
    string? UltimosDigitos = null,
    bool AprobacionManual = false,
    int? OperacionTerminalId = null);

/// <param name="TotalCobrado">Total de la venta después del redondeo del efectivo.</param>
/// <param name="Redondeo">Diferencia por redondeo del efectivo (RF-216); positiva o negativa.</param>
public sealed record ResultadoCobro(decimal Total, decimal TotalCobrado, decimal Pagado, decimal Devuelta, decimal Redondeo, bool AbreGaveta);

/// <summary>Pago registrado en una venta cobrada. Una venta puede tener varios (pago mixto, RF-211).</summary>
public sealed class PagoVenta : Entidad
{
    public const int LargoMaximoReferencia = 60;
    public const int LargoMaximoNombre = 100;

    private PagoVenta()
    {
    }

    public int VentaId { get; private set; }
    public int Numero { get; private set; }
    public int FormaPagoId { get; private set; }
    public string FormaPagoCodigo { get; private set; } = string.Empty;
    public string FormaPagoNombre { get; private set; } = string.Empty;
    public TipoFormaPago Tipo { get; private set; }
    public string Moneda { get; private set; } = string.Empty;
    public decimal MontoRecibido { get; private set; }
    public decimal? TasaCambio { get; private set; }

    /// <summary>Monto en pesos que abona a la factura.</summary>
    public decimal MontoAplicado { get; private set; }

    public string? Referencia { get; private set; }
    public int? BancoId { get; private set; }
    public string? BancoNombre { get; private set; }
    public int? TipoTarjetaId { get; private set; }
    public string? TipoTarjetaNombre { get; private set; }
    public string? UltimosDigitos { get; private set; }
    public bool AprobacionManual { get; private set; }

    /// <summary>Pendiente de conciliar contra la pasarela (aprobación manual, RF-213).</summary>
    public bool ParaConciliar { get; private set; }

    /// <summary>Operación del terminal de pago que aprobó la tarjeta, para anularla o conciliarla (RF-214, RF-215).</summary>
    public int? OperacionTerminalId { get; private set; }

    public bool PermiteDevuelta { get; private set; }

    /// <param name="monedaVenta">Moneda de la venta: los pagos en ella no llevan tasa.</param>
    internal static PagoVenta Crear(int ventaId, string monedaVenta, int numero, PagoSolicitado pago, decimal montoAplicado) =>
        new()
        {
            VentaId = ventaId,
            Numero = numero,
            FormaPagoId = pago.Forma.FormaPagoId,
            FormaPagoCodigo = pago.Forma.Codigo,
            FormaPagoNombre = pago.Forma.Nombre,
            Tipo = pago.Forma.Tipo,
            Moneda = pago.Forma.Moneda,
            MontoRecibido = decimal.Round(pago.MontoRecibido, 2, MidpointRounding.AwayFromZero),
            TasaCambio = pago.Forma.Moneda == monedaVenta ? null : pago.TasaCambio,
            MontoAplicado = montoAplicado,
            Referencia = Validar.TextoOpcional(pago.Referencia, "Referencia del pago", LargoMaximoReferencia),
            BancoId = pago.BancoId,
            BancoNombre = Validar.TextoOpcional(pago.BancoNombre, "Banco", LargoMaximoNombre),
            TipoTarjetaId = pago.TipoTarjetaId,
            TipoTarjetaNombre = Validar.TextoOpcional(pago.TipoTarjetaNombre, "Tipo de tarjeta", LargoMaximoNombre),
            UltimosDigitos = Validar.TextoOpcional(pago.UltimosDigitos, "Últimos dígitos", 4),
            AprobacionManual = pago.AprobacionManual,
            ParaConciliar = pago.AprobacionManual,
            OperacionTerminalId = pago.OperacionTerminalId,
            PermiteDevuelta = pago.Forma.PermiteDevuelta,
        };
}
