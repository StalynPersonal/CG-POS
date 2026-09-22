using CgPos.Dominio.Comun;

namespace CgPos.Dominio.Devoluciones;

/// <summary>
/// Líneas de una factura que una caja retiene mientras emite su nota de crédito. Sin esto, dos tiendas que consultan la misma
/// factura a la vez verían las dos el mismo disponible y devolverían dos veces la misma mercancía. Vence sola: si la caja se
/// queda sin red a mitad de la devolución, la mercancía no queda bloqueada para siempre.
/// </summary>
public sealed class ReservaFacturaCentral : Entidad
{
    public const int LargoMaximoNumero = NumeroDocumento.LargoMaximo;
    public const int LargoMaximoCierre = 40;

    private readonly List<LineaReservaFacturaCentral> _lineas = [];

    private ReservaFacturaCentral()
    {
    }

    public string FacturaNumero { get; private set; } = string.Empty;
    public int CajaId { get; private set; }
    public DateTimeOffset CreadaEn { get; private set; }
    public DateTimeOffset VenceEn { get; private set; }
    public DateTimeOffset? CerradaEn { get; private set; }

    /// <summary>Cómo terminó: consumida por la nota, liberada por la caja o vencida sin usarse.</summary>
    public string? Cierre { get; private set; }

    public IReadOnlyList<LineaReservaFacturaCentral> Lineas => _lineas;

    public bool EstaVigente(DateTimeOffset ahora) => CerradaEn is null && VenceEn > ahora;

    public static ReservaFacturaCentral Crear(string facturaNumero, int cajaId, IReadOnlyDictionary<int, decimal> lineas, DateTimeOffset ahora,
        TimeSpan vigencia)
    {
        ArgumentNullException.ThrowIfNull(lineas);
        var reserva = new ReservaFacturaCentral
        {
            FacturaNumero = Validar.Texto(facturaNumero, "Número de la factura", LargoMaximoNumero).ToUpperInvariant(),
            CajaId = Validar.Id(cajaId, "Caja"),
            CreadaEn = ahora,
            VenceEn = ahora + vigencia,
        };

        foreach (var (numeroLinea, cantidad) in lineas.Where(l => l.Value > 0m).OrderBy(l => l.Key))
            reserva._lineas.Add(new LineaReservaFacturaCentral { ReservaId = reserva.Id, NumeroLinea = numeroLinea, Cantidad = cantidad });

        if (reserva._lineas.Count == 0)
            throw new ArgumentException("La reserva no tiene líneas con cantidad.", nameof(lineas));

        return reserva;
    }

    public void Cerrar(string cierre, DateTimeOffset ahora)
    {
        if (CerradaEn is not null)
            return;

        Cierre = Validar.Texto(cierre, "Cierre de la reserva", LargoMaximoCierre);
        CerradaEn = ahora;
    }
}

public sealed class LineaReservaFacturaCentral : Entidad
{
    public int ReservaId { get; internal set; }
    public int NumeroLinea { get; internal set; }
    public decimal Cantidad { get; internal set; }
}
