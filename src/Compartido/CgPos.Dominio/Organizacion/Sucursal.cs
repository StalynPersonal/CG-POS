using CgPos.Dominio.Comun;

namespace CgPos.Dominio.Organizacion;

public sealed class Sucursal : Entidad
{

    private Sucursal()
    {
    }

    public int EmpresaId { get; private set; }

    /// <summary>Código de la sucursal, de dos dígitos (01 a 99) y único en la empresa. Es el que va en los documentos.</summary>
    public string Codigo { get; private set; } = string.Empty;

    public string Nombre { get; private set; } = string.Empty;
    public string? Direccion { get; private set; }
    public string? Telefono { get; private set; }
    public bool Activa { get; private set; } = true;

    public static Sucursal Crear(int empresaId, string codigo, string nombre,
        string? direccion = null, string? telefono = null)
    {
        var sucursal = new Sucursal
        {
            EmpresaId = Validar.Id(empresaId, "Empresa"),
            Codigo = Validar.CodigoDosDigitos(codigo, "Código de sucursal"),
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
