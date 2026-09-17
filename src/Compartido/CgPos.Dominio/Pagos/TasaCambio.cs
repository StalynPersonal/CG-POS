using CgPos.Dominio.Comun;

namespace CgPos.Dominio.Pagos;

/// <summary>Tasa del día de una moneda extranjera, recibida de SAP B1 por el Central (RF-25, RF-212).</summary>
public sealed class TasaCambio : Entidad
{
    private TasaCambio()
    {
    }

    public string Moneda { get; private set; } = string.Empty;

    /// <summary>Pesos dominicanos por unidad de la moneda.</summary>
    public decimal Tasa { get; private set; }

    public DateTimeOffset VigenteDesde { get; private set; }

    public static TasaCambio Registrar(string moneda, decimal tasa, DateTimeOffset vigenteDesde)
    {
        var registro = new TasaCambio
        {
            Moneda = FormaPago.ValidarMoneda(moneda),
        };
        registro.Actualizar(tasa, vigenteDesde);
        return registro;
    }

    public void Actualizar(decimal tasa, DateTimeOffset vigenteDesde)
    {
        if (tasa <= 0)
            throw new ArgumentOutOfRangeException(nameof(tasa), tasa, "La tasa de cambio debe ser mayor que cero.");

        Tasa = decimal.Round(tasa, 4, MidpointRounding.AwayFromZero);
        VigenteDesde = vigenteDesde;
    }

    /// <returns>La tasa más reciente ya vigente para la moneda, o nulo si no hay.</returns>
    public static decimal? Vigente(IEnumerable<TasaCambio> tasas, string moneda, DateTimeOffset ahora) =>
        tasas
            .Where(t => string.Equals(t.Moneda, moneda, StringComparison.OrdinalIgnoreCase) && t.VigenteDesde <= ahora)
            .OrderByDescending(t => t.VigenteDesde)
            .Select(t => (decimal?)t.Tasa)
            .FirstOrDefault();
}
