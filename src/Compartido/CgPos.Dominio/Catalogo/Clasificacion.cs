using CgPos.Dominio.Comun;

namespace CgPos.Dominio.Catalogo;

/// <summary>Categoría de artículos dentro de un departamento (ej. Eléctrico → Cables).</summary>
public sealed class Categoria : Entidad
{
    public const int LargoMaximoNombre = 100;

    private Categoria()
    {
    }

    public int Codigo { get; private set; }
    public string Nombre { get; private set; } = string.Empty;
    public Guid DepartamentoId { get; private set; }
    public bool Activa { get; private set; } = true;

    public static Categoria Crear(int codigo, string nombre, Guid departamentoId, Guid? id = null)
    {
        var categoria = new Categoria
        {
            Id = id ?? Guid.CreateVersion7(),
            Codigo = Validar.Codigo(codigo, "Código de categoría"),
        };
        categoria.Actualizar(nombre, departamentoId);
        return categoria;
    }

    public void Actualizar(string nombre, Guid departamentoId)
    {
        Nombre = Validar.Texto(nombre, "Nombre de categoría", LargoMaximoNombre);
        DepartamentoId = Validar.Id(departamentoId, "Departamento");
    }

    public void Activar() => Activa = true;

    public void Desactivar() => Activa = false;
}

/// <summary>Marca del artículo. No depende del departamento: una marca puede tener artículos en varios.</summary>
public sealed class Marca : Entidad
{
    public const int LargoMaximoNombre = 100;

    private Marca()
    {
    }

    public int Codigo { get; private set; }
    public string Nombre { get; private set; } = string.Empty;
    public bool Activa { get; private set; } = true;

    public static Marca Crear(int codigo, string nombre, Guid? id = null)
    {
        var marca = new Marca
        {
            Id = id ?? Guid.CreateVersion7(),
            Codigo = Validar.Codigo(codigo, "Código de marca"),
        };
        marca.CambiarNombre(nombre);
        return marca;
    }

    public void CambiarNombre(string nombre) => Nombre = Validar.Texto(nombre, "Nombre de marca", LargoMaximoNombre);

    public void Activar() => Activa = true;

    public void Desactivar() => Activa = false;
}
