using CgPos.Dominio.Comun;

namespace CgPos.Dominio.Catalogo;

/// <summary>Unidad de medida: unidad, libra, pie, yarda, kilo… (RF-145, RF-198).</summary>
public sealed class UnidadMedida : Entidad
{
    public const int LargoMaximoAbreviatura = 10;
    public const int LargoMaximoNombre = 50;
    public const int DecimalesMaximos = 4;

    private UnidadMedida()
    {
    }

    /// <summary>Código numérico con que se sincroniza entre el Central y las cajas.</summary>
    public int Codigo { get; private set; }

    /// <summary>Abreviatura que se imprime en el ticket y va en el e-CF (ej. UND, LB).</summary>
    public string Abreviatura { get; private set; } = string.Empty;
    public string Nombre { get; private set; } = string.Empty;
    public bool PermiteDecimales { get; private set; }
    public int Decimales { get; private set; }

    public static UnidadMedida Crear(int codigo, string abreviatura, string nombre, bool permiteDecimales = false, int decimales = 0, Guid? id = null)
    {
        var unidad = new UnidadMedida
        {
            Id = id ?? Guid.CreateVersion7(),
            Codigo = Validar.Codigo(codigo, "Código de unidad"),
        };
        unidad.Actualizar(abreviatura, nombre, permiteDecimales, decimales);
        return unidad;
    }

    public void Actualizar(string abreviatura, string nombre, bool permiteDecimales, int decimales)
    {
        if (decimales is < 0 or > DecimalesMaximos)
            throw new ArgumentOutOfRangeException(nameof(decimales), decimales, $"Los decimales deben estar entre 0 y {DecimalesMaximos}.");

        Abreviatura = Validar.Texto(abreviatura, "Abreviatura de unidad", LargoMaximoAbreviatura).ToUpperInvariant();
        Nombre = Validar.Texto(nombre, "Nombre de unidad", LargoMaximoNombre);
        PermiteDecimales = permiteDecimales;
        Decimales = permiteDecimales ? decimales : 0;
    }

    /// <summary>Redondea la cantidad a los decimales de la unidad (medio punto hacia arriba).</summary>
    public decimal Redondear(decimal cantidad) => Math.Round(cantidad, Decimales, MidpointRounding.AwayFromZero);

    public bool EsCantidadValida(decimal cantidad) => cantidad > 0 && (PermiteDecimales || cantidad == decimal.Truncate(cantidad));
}
