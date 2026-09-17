using CgPos.Dominio.Comun;

namespace CgPos.Dominio.Devoluciones;

public enum EstadoNotaCreditoCentral
{
    Vigente,
    Consumida,
    Vencida,
}

/// <summary>
/// Nota de crédito emitida por una caja y registrada en el Central para poder consumirla en cualquier sucursal (RF-38, RF-43).
/// El saldo es el total menos lo consumido; una reserva retiene saldo mientras otra caja termina de cobrar.
/// </summary>
public sealed class NotaCreditoCentral : Entidad
{
    public const int LargoMaximoNumero = 40;
    public const int LargoMaximoEncf = 13;
    public const int LargoMaximoTexto = 200;
    public const int LargoMaximoMoneda = 3;
    public const int LargoMaximoMotivo = 500;

    private NotaCreditoCentral()
    {
    }

    public string Numero { get; private set; } = string.Empty;
    public string? Encf { get; private set; }
    public int CajaId { get; private set; }
    public int SucursalId { get; private set; }
    public string ClienteDocumento { get; private set; } = string.Empty;
    public string ClienteNombre { get; private set; } = string.Empty;
    public string Moneda { get; private set; } = string.Empty;
    public decimal Total { get; private set; }

    /// <summary>Suma de los consumos informados por las cajas.</summary>
    public decimal Consumido { get; private set; }

    /// <summary>Día de emisión en la caja: la vigencia se cuenta desde aquí con los días configurados.</summary>
    public DateOnly FechaEmision { get; private set; }

    public DateTimeOffset EmitidaEn { get; private set; }
    public DateTimeOffset RegistradaEn { get; private set; }

    public decimal Saldo => Total - Consumido;

    /// <summary>Cajas sin conexión consumieron más que el total: hay que revisarlo con la sucursal.</summary>
    public bool Sobregirada => Consumido > Total;

    public DateOnly VenceEn(int diasVigencia) => VigenciaNotaCredito.VenceEn(FechaEmision, diasVigencia);

    public EstadoNotaCreditoCentral Estado(DateOnly hoy, int diasVigencia) =>
        Saldo <= 0 ? EstadoNotaCreditoCentral.Consumida
        : hoy > VenceEn(diasVigencia) ? EstadoNotaCreditoCentral.Vencida
        : EstadoNotaCreditoCentral.Vigente;

    public static NotaCreditoCentral Registrar(string numero, string? encf, int cajaId, int sucursalId, string? clienteDocumento, string? clienteNombre,
        string moneda, decimal total, DateOnly fechaEmision, DateTimeOffset emitidaEn, DateTimeOffset ahora)
    {
        if (total <= 0)
            throw new ArgumentOutOfRangeException(nameof(total), total, "El total de la nota de crédito debe ser mayor que cero.");

        return new NotaCreditoCentral
        {
            Numero = Validar.Texto(numero, "Número de la nota de crédito", LargoMaximoNumero),
            Encf = Validar.TextoOpcional(encf, "e-NCF", LargoMaximoEncf),
            CajaId = Validar.Id(cajaId, "Caja"),
            SucursalId = Validar.Id(sucursalId, "Sucursal"),
            ClienteDocumento = Validar.TextoOpcional(clienteDocumento, "Documento del cliente", LargoMaximoTexto) ?? string.Empty,
            ClienteNombre = Validar.TextoOpcional(clienteNombre, "Nombre del cliente", LargoMaximoTexto) ?? string.Empty,
            Moneda = Validar.Texto(moneda, "Moneda", LargoMaximoMoneda).ToUpperInvariant(),
            Total = total,
            FechaEmision = fechaEmision,
            EmitidaEn = emitidaEn,
            RegistradaEn = ahora,
        };
    }

    /// <summary>Consumo ya hecho por una caja: se registra aunque deje el saldo en negativo, para que el Central lo muestre.</summary>
    public void AplicarConsumo(decimal monto)
    {
        if (monto <= 0)
            throw new ArgumentOutOfRangeException(nameof(monto), monto, "El monto consumido debe ser mayor que cero.");

        Consumido += monto;
    }
}

/// <summary>
/// Consumo de una nota de crédito informado por una caja. Una factura la consume una sola vez (RF-38): la nota y la factura, por sus números,
/// identifican el consumo, que puede llegar antes que la nota.
/// </summary>
public sealed class ConsumoNotaCreditoCentral : Entidad
{
    private ConsumoNotaCreditoCentral()
    {
    }

    public string NotaCreditoNumero { get; private set; } = string.Empty;
    public string VentaNumero { get; private set; } = string.Empty;
    public int CajaId { get; private set; }
    public decimal Monto { get; private set; }
    public DateTimeOffset Fecha { get; private set; }
    public DateTimeOffset RegistradoEn { get; private set; }

    public static ConsumoNotaCreditoCentral Registrar(string notaCreditoNumero, string ventaNumero, int cajaId, decimal monto, DateTimeOffset fecha,
        DateTimeOffset ahora)
    {
        if (monto <= 0)
            throw new ArgumentOutOfRangeException(nameof(monto), monto, "El monto consumido debe ser mayor que cero.");

        return new ConsumoNotaCreditoCentral
        {
            NotaCreditoNumero = Validar.Texto(notaCreditoNumero, "Número de la nota de crédito", NotaCreditoCentral.LargoMaximoNumero),
            VentaNumero = Validar.Texto(ventaNumero, "Número de la venta", NotaCreditoCentral.LargoMaximoNumero),
            CajaId = Validar.Id(cajaId, "Caja"),
            Monto = monto,
            Fecha = fecha,
            RegistradoEn = ahora,
        };
    }
}

/// <summary>
/// Saldo retenido para una caja mientras cobra con una nota de crédito de otra sucursal: evita que dos cajas consuman el mismo saldo.
/// Vence sola si la caja no confirma el consumo.
/// </summary>
public sealed class ReservaNotaCreditoCentral : Entidad
{
    public const int LargoMaximoCierre = 40;

    private ReservaNotaCreditoCentral()
    {
    }

    public int NotaCreditoId { get; private set; }
    public int CajaId { get; private set; }

    /// <summary>Factura de la caja para la que se retuvo el saldo: con ella la caja la libera o la confirma al consumir.</summary>
    public string VentaNumero { get; private set; } = string.Empty;

    public decimal Monto { get; private set; }
    public DateTimeOffset CreadaEn { get; private set; }
    public DateTimeOffset VenceEn { get; private set; }
    public DateTimeOffset? CerradaEn { get; private set; }

    /// <summary>Cómo terminó: consumida por la venta, liberada por la caja o vencida sin usarse.</summary>
    public string? Cierre { get; private set; }

    public bool EstaVigente(DateTimeOffset ahora) => CerradaEn is null && VenceEn > ahora;

    public static ReservaNotaCreditoCentral Crear(int notaCreditoId, int cajaId, string ventaNumero, decimal monto, DateTimeOffset ahora, TimeSpan vigencia)
    {
        if (monto <= 0)
            throw new ArgumentOutOfRangeException(nameof(monto), monto, "El monto de la reserva debe ser mayor que cero.");

        return new ReservaNotaCreditoCentral
        {
            NotaCreditoId = Validar.Id(notaCreditoId, "Nota de crédito"),
            CajaId = Validar.Id(cajaId, "Caja"),
            VentaNumero = Validar.Texto(ventaNumero, "Número de la venta", NotaCreditoCentral.LargoMaximoNumero),
            Monto = monto,
            CreadaEn = ahora,
            VenceEn = ahora + vigencia,
        };
    }

    public void Cerrar(string cierre, DateTimeOffset ahora)
    {
        if (CerradaEn is not null)
            return;

        CerradaEn = ahora;
        Cierre = Validar.Texto(cierre, "Cierre", LargoMaximoCierre);
    }
}
