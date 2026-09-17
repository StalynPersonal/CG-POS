using CgPos.Dominio.Comun;

namespace CgPos.Dominio.Seguridad;

/// <summary>Rol de los usuarios del Central Manager: agrupa permisos del <see cref="CatalogoPermisosCentral"/>.</summary>
public sealed class RolCentral : Entidad
{
    public const int LargoMaximoCodigo = 30;
    public const int LargoMaximoNombre = 100;

    private readonly List<RolCentralPermiso> _permisosAsignados = [];

    private RolCentral()
    {
    }

    public string Codigo { get; private set; } = string.Empty;
    public string Nombre { get; private set; } = string.Empty;
    public bool Activo { get; private set; } = true;
    public IReadOnlyCollection<RolCentralPermiso> PermisosAsignados => _permisosAsignados;

    public static RolCentral Crear(string codigo, string nombre)
    {
        var rol = new RolCentral
        {
            Codigo = Validar.Texto(codigo, "Código de rol", LargoMaximoCodigo),
        };
        rol.CambiarNombre(nombre);
        return rol;
    }

    public void CambiarNombre(string nombre) => Nombre = Validar.Texto(nombre, "Nombre de rol", LargoMaximoNombre);

    public void AsignarPermiso(string codigoPermiso)
    {
        if (!CatalogoPermisosCentral.Existe(codigoPermiso))
            throw new ArgumentException($"El permiso '{codigoPermiso}' no existe en el catálogo del Central.", nameof(codigoPermiso));

        if (_permisosAsignados.Any(p => p.PermisoCodigo == codigoPermiso))
            return;

        _permisosAsignados.Add(new RolCentralPermiso(Id, codigoPermiso));
    }

    public void QuitarPermiso(string codigoPermiso) =>
        _permisosAsignados.RemoveAll(p => p.PermisoCodigo == codigoPermiso);

    /// <summary>Un rol inactivo no concede ningún permiso.</summary>
    public bool TienePermiso(string codigoPermiso) =>
        Activo && _permisosAsignados.Any(p => p.PermisoCodigo == codigoPermiso);

    public void Activar() => Activo = true;

    public void Desactivar() => Activo = false;
}

public sealed class RolCentralPermiso
{
    private RolCentralPermiso()
    {
    }

    internal RolCentralPermiso(int rolId, string permisoCodigo)
    {
        RolId = rolId;
        PermisoCodigo = permisoCodigo;
    }

    public int RolId { get; private set; }
    public string PermisoCodigo { get; private set; } = string.Empty;
}
