using CgPos.Dominio.Comun;

namespace CgPos.Dominio.Organizacion;

/// <summary>Empresa única del sistema (una empresa, múltiples sucursales).</summary>
public sealed class Empresa : Entidad
{
    public const int LargoRnc = 9;
    public const int LargoMaximoNombre = 150;
    public const int LargoMaximoDireccion = 250;
    public const int LargoMaximoTelefono = 20;

    private Empresa()
    {
    }

    public string Rnc { get; private set; } = string.Empty;
    public string RazonSocial { get; private set; } = string.Empty;
    public string? NombreComercial { get; private set; }
    public string? Direccion { get; private set; }
    public string? Telefono { get; private set; }

    /// <param name="id">Id asignado por el Central; si se omite se genera uno nuevo.</param>
    public static Empresa Crear(string rnc, string razonSocial, string? nombreComercial = null,
        string? direccion = null, string? telefono = null)
    {
        var empresa = new Empresa
        {
            Rnc = Validar.Digitos(rnc, "RNC", LargoRnc),
        };
        empresa.ActualizarDatos(razonSocial, nombreComercial, direccion, telefono);
        return empresa;
    }

    public void ActualizarDatos(string razonSocial, string? nombreComercial, string? direccion, string? telefono)
    {
        RazonSocial = Validar.Texto(razonSocial, "Razón social", LargoMaximoNombre);
        NombreComercial = Validar.TextoOpcional(nombreComercial, "Nombre comercial", LargoMaximoNombre);
        Direccion = Validar.TextoOpcional(direccion, "Dirección", LargoMaximoDireccion);
        Telefono = Validar.TextoOpcional(telefono, "Teléfono", LargoMaximoTelefono);
    }
}
