using CgPos.Dominio.Comun;

namespace CgPos.Dominio.Seguridad;

/// <summary>Registro persistido de un permiso del <see cref="CatalogoPermisos"/>.</summary>
public sealed class Permiso
{
    public const int LargoMaximoCodigo = 100;
    public const int LargoMaximoModulo = 50;
    public const int LargoMaximoDescripcion = 250;

    private Permiso()
    {
    }

    public string Codigo { get; private set; } = string.Empty;
    public string Modulo { get; private set; } = string.Empty;
    public string Descripcion { get; private set; } = string.Empty;

    public static Permiso Crear(DefinicionPermiso definicion)
    {
        ArgumentNullException.ThrowIfNull(definicion);
        if (!CatalogoPermisos.Existe(definicion.Codigo))
            throw new ArgumentException($"El permiso '{definicion.Codigo}' no existe en el catálogo.", nameof(definicion));

        var permiso = new Permiso { Codigo = definicion.Codigo };
        permiso.Actualizar(definicion);
        return permiso;
    }

    public void Actualizar(DefinicionPermiso definicion)
    {
        ArgumentNullException.ThrowIfNull(definicion);
        if (definicion.Codigo != Codigo)
            throw new ArgumentException("El código del permiso no se puede cambiar.", nameof(definicion));

        Modulo = Validar.Texto(definicion.Modulo, "Módulo", LargoMaximoModulo);
        Descripcion = Validar.Texto(definicion.Descripcion, "Descripción", LargoMaximoDescripcion);
    }
}
