using CgPos.Dominio.Comun;

namespace CgPos.Dominio.Organizacion;

public sealed class Sucursal : Entidad
{

    private Sucursal()
    {
    }

    public Guid EmpresaId { get; private set; }

    /// <summary>Código de la sucursal (1 a 99), único en la empresa; en los números de documento va con dos dígitos (ej. 01).</summary>
    public int Codigo { get; private set; }

    public string Nombre { get; private set; } = string.Empty;
    public string? Direccion { get; private set; }
    public string? Telefono { get; private set; }
    public bool Activa { get; private set; } = true;

    public static Sucursal Crear(Guid empresaId, int codigo, string nombre,
        string? direccion = null, string? telefono = null, Guid? id = null)
    {
        var sucursal = new Sucursal
        {
            Id = id ?? Guid.CreateVersion7(),
            EmpresaId = Validar.Id(empresaId, "Empresa"),
            Codigo = Validar.Codigo(codigo, "Código de sucursal", CodigosCatalogo.MaximoSucursalCaja),
        };
        sucursal.ActualizarDatos(nombre, direccion, telefono);
        return sucursal;
    }

    public void ActualizarDatos(string nombre, string? direccion, string? telefono)
    {
        Nombre = Validar.Texto(nombre, "Nombre de sucursal", Empresa.LargoMaximoNombre);
        Direccion = Validar.TextoOpcional(direccion, "Dirección", Empresa.LargoMaximoDireccion);
        Telefono = Validar.TextoOpcional(telefono, "Teléfono", Empresa.LargoMaximoTelefono);
    }

    public void Activar() => Activa = true;

    public void Desactivar() => Activa = false;
}
