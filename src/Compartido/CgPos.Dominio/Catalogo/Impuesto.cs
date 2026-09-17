using CgPos.Dominio.Comun;

namespace CgPos.Dominio.Catalogo;

/// <summary>
/// Impuesto asociado al artículo (RF-183): ITBIS 18 %, 16 %, 0 % o exento.
/// Los precios de venta se guardan con el impuesto incluido; la base y el monto se calculan a partir de ellos.
/// </summary>
public sealed class Impuesto : Entidad
{
    public const int LargoMaximoCodigo = 20;
    public const int LargoMaximoNombre = 50;

    private Impuesto()
    {
    }

    public string Codigo { get; private set; } = string.Empty;
    public string Nombre { get; private set; } = string.Empty;
    public decimal Porcentaje { get; private set; }

    /// <summary>Indicador de facturación del e-CF: 1 = ITBIS 18 %, 2 = ITBIS 16 %, 3 = ITBIS 0 %, 4 = exento.</summary>
    public int IndicadorFacturacion { get; private set; }

    public bool Activo { get; private set; } = true;

    public static Impuesto Crear(string codigo, string nombre, decimal porcentaje, int indicadorFacturacion)
    {
        var impuesto = new Impuesto
        {
            Codigo = Validar.Texto(codigo, "Código de impuesto", LargoMaximoCodigo).ToUpperInvariant(),
        };
        impuesto.Actualizar(nombre, porcentaje, indicadorFacturacion);
        return impuesto;
    }

    public void Actualizar(string nombre, decimal porcentaje, int indicadorFacturacion)
    {
        if (porcentaje is < 0 or > 100)
            throw new ArgumentOutOfRangeException(nameof(porcentaje), porcentaje, "El porcentaje debe estar entre 0 y 100.");
        if (indicadorFacturacion is < 1 or > 4)
            throw new ArgumentOutOfRangeException(nameof(indicadorFacturacion), indicadorFacturacion, "El indicador de facturación debe estar entre 1 y 4.");

        Nombre = Validar.Texto(nombre, "Nombre de impuesto", LargoMaximoNombre);
        Porcentaje = porcentaje;
        IndicadorFacturacion = indicadorFacturacion;
    }

    /// <summary>Base imponible contenida en un precio con impuesto incluido (sin redondear).</summary>
    public decimal BaseDesdePrecioConImpuesto(decimal precioConImpuesto) =>
        Porcentaje == 0 ? precioConImpuesto : precioConImpuesto / (1 + Porcentaje / 100m);

    /// <summary>Monto de impuesto contenido en un precio con impuesto incluido (sin redondear).</summary>
    public decimal MontoDesdePrecioConImpuesto(decimal precioConImpuesto) =>
        precioConImpuesto - BaseDesdePrecioConImpuesto(precioConImpuesto);

    public void Activar() => Activo = true;

    public void Desactivar() => Activo = false;
}
