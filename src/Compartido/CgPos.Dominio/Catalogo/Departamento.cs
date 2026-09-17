using CgPos.Dominio.Comun;

namespace CgPos.Dominio.Catalogo;

/// <summary>Familia o departamento de artículos.</summary>
public sealed class Familia : Entidad
{
    public const int LargoMaximoCodigo = 20;
    public const int LargoMaximoNombre = 100;

    private Familia()
    {
    }

    public string Codigo { get; private set; } = string.Empty;
    public string Nombre { get; private set; } = string.Empty;

    /// <summary>Falso para familias que solo admiten ofertas (supermercado, panadería, repostería; RN-09).</summary>
    public bool PermiteDescuentoManual { get; private set; } = true;

    /// <summary>Familia de productos no codificados (vegetales, especias) que se eligen de un listado alfabético (RF-134).</summary>
    public bool EsNoCodificada { get; private set; }

    public bool Activa { get; private set; } = true;

    public static Familia Crear(string codigo, string nombre, bool permiteDescuentoManual = true, bool esNoCodificada = false, Guid? id = null)
    {
        var familia = new Familia
        {
            Id = id ?? Guid.CreateVersion7(),
            Codigo = Validar.Texto(codigo, "Código de familia", LargoMaximoCodigo),
        };
        familia.Actualizar(nombre, permiteDescuentoManual, esNoCodificada);
        return familia;
    }

    public void Actualizar(string nombre, bool permiteDescuentoManual, bool esNoCodificada)
    {
        Nombre = Validar.Texto(nombre, "Nombre de familia", LargoMaximoNombre);
        PermiteDescuentoManual = permiteDescuentoManual;
        EsNoCodificada = esNoCodificada;
    }

    public void Activar() => Activa = true;

    public void Desactivar() => Activa = false;
}
