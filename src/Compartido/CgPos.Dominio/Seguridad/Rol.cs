using CgPos.Dominio.Comun;

namespace CgPos.Dominio.Seguridad;

/// <summary>
/// Rol de usuario: agrupa permisos granulares y define un nivel de autorización
/// (1 cajero, 2 supervisor, 3 gerente…) usado para topes y jerarquía de autorizaciones.
/// </summary>
public sealed class Rol : Entidad
{
    public const int LargoMaximoCodigo = 30;
    public const int LargoMaximoNombre = 100;
    public const int NivelMinimo = 1;
    public const int NivelMaximo = 9;

    private readonly List<RolPermiso> _permisosAsignados = [];

    private Rol()
    {
    }

    public string Codigo { get; private set; } = string.Empty;
    public string Nombre { get; private set; } = string.Empty;
    public int Nivel { get; private set; }
    public bool Activo { get; private set; } = true;
    public IReadOnlyCollection<RolPermiso> PermisosAsignados => _permisosAsignados;

    public static Rol Crear(string codigo, string nombre, int nivel, Guid? id = null)
    {
        var rol = new Rol
        {
            Id = id ?? Guid.CreateVersion7(),
            Codigo = Validar.Texto(codigo, "Código de rol", LargoMaximoCodigo),
        };
        rol.Actualizar(nombre, nivel);
        return rol;
    }

    public void Actualizar(string nombre, int nivel)
    {
        if (nivel is < NivelMinimo or > NivelMaximo)
            throw new ArgumentOutOfRangeException(nameof(nivel), nivel, $"El nivel debe estar entre {NivelMinimo} y {NivelMaximo}.");

        Nombre = Validar.Texto(nombre, "Nombre de rol", LargoMaximoNombre);
        Nivel = nivel;
    }

    public void AsignarPermiso(string codigoPermiso)
    {
        if (!CatalogoPermisos.Existe(codigoPermiso))
            throw new ArgumentException($"El permiso '{codigoPermiso}' no existe en el catálogo.", nameof(codigoPermiso));

        if (_permisosAsignados.Any(p => p.PermisoCodigo == codigoPermiso))
            return;

        _permisosAsignados.Add(new RolPermiso(Id, codigoPermiso));
    }

    public void QuitarPermiso(string codigoPermiso) =>
        _permisosAsignados.RemoveAll(p => p.PermisoCodigo == codigoPermiso);

    /// <summary>Un rol inactivo no concede ningún permiso.</summary>
    public bool TienePermiso(string codigoPermiso) =>
        Activo && _permisosAsignados.Any(p => p.PermisoCodigo == codigoPermiso);

    public void Activar() => Activo = true;

    public void Desactivar() => Activo = false;
}

public sealed class RolPermiso
{
    private RolPermiso()
    {
    }

    internal RolPermiso(Guid rolId, string permisoCodigo)
    {
        RolId = rolId;
        PermisoCodigo = permisoCodigo;
    }

    public Guid RolId { get; private set; }
    public string PermisoCodigo { get; private set; } = string.Empty;
}
