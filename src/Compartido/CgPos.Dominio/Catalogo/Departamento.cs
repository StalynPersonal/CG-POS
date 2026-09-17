using CgPos.Dominio.Comun;

namespace CgPos.Dominio.Catalogo;

/// <summary>Departamento de artículos, el primer nivel de su clasificación (departamento → categoría; la marca va aparte).</summary>
public sealed class Departamento : Entidad
{
    public const int LargoMaximoCodigo = 20;
    public const int LargoMaximoNombre = 100;

    private Departamento()
    {
    }

    public string Codigo { get; private set; } = string.Empty;
    public string Nombre { get; private set; } = string.Empty;

    /// <summary>Falso para departamentos que solo admiten ofertas (supermercado, panadería, repostería; RN-09).</summary>
    public bool PermiteDescuentoManual { get; private set; } = true;

    /// <summary>Departamento de productos no codificados (vegetales, especias) que se eligen de un listado alfabético (RF-134).</summary>
    public bool EsNoCodificada { get; private set; }

    public bool Activa { get; private set; } = true;

    public static Departamento Crear(string codigo, string nombre, bool permiteDescuentoManual = true, bool esNoCodificada = false, Guid? id = null)
    {
        var departamento = new Departamento
        {
            Id = id ?? Guid.CreateVersion7(),
            Codigo = Validar.Texto(codigo, "Código de departamento", LargoMaximoCodigo),
        };
        departamento.Actualizar(nombre, permiteDescuentoManual, esNoCodificada);
        return departamento;
    }

    public void Actualizar(string nombre, bool permiteDescuentoManual, bool esNoCodificada)
    {
        Nombre = Validar.Texto(nombre, "Nombre de departamento", LargoMaximoNombre);
        PermiteDescuentoManual = permiteDescuentoManual;
        EsNoCodificada = esNoCodificada;
    }

    public void Activar() => Activa = true;

    public void Desactivar() => Activa = false;
}
