using CgPos.Dominio.Comun;

namespace CgPos.Dominio.Organizacion;

/// <summary>Empresa única del sistema (una empresa, múltiples sucursales).</summary>
public sealed class Empresa : Entidad
{
    /// <summary>El documento de la empresa puede ser un RNC (9 dígitos) o una cédula (11), si factura una persona física.</summary>
    public const int LargoMaximoRnc = Fiscal.DocumentoIdentidad.LargoCedula;
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
            Rnc = NormalizarRnc(rnc),
        };
        empresa.ActualizarDatos(razonSocial, nombreComercial, direccion, telefono);
        return empresa;
    }

    /// <summary>
    /// Cambia el RNC con el que se factura. Se corrige cuando se registró mal al instalar; los comprobantes ya emitidos
    /// conservan el que llevaban, así que el cambio queda en la auditoría.
    /// </summary>
    public void CambiarRnc(string rnc) => Rnc = NormalizarRnc(rnc);

    /// <summary>
    /// Acepta un RNC de 9 dígitos o una cédula de 11, con o sin guiones. Al RNC se le exige el dígito verificador de la
    /// DGII; a la cédula no, porque hay cédulas antiguas legítimas que no lo cumplen.
    /// </summary>
    private static string NormalizarRnc(string rnc)
    {
        var documento = Fiscal.DocumentoIdentidad.Validar(rnc);
        if (!documento.FormatoValido)
            throw new ArgumentException(
                $"El RNC o cédula '{documento.Documento}' no tiene el formato correcto: 9 dígitos si es RNC u 11 si es cédula.",
                nameof(rnc));

        if (documento.Tipo == Fiscal.TipoDocumentoIdentidad.Rnc && !documento.DigitoVerificadorValido)
            throw new ArgumentException($"El RNC '{documento.Documento}' no es válido: su dígito verificador no corresponde.",
                nameof(rnc));

        return documento.Documento;
    }

    public void ActualizarDatos(string razonSocial, string? nombreComercial, string? direccion, string? telefono)
    {
        RazonSocial = Validar.Texto(razonSocial, "Razón social", LargoMaximoNombre);
        NombreComercial = Validar.TextoOpcional(nombreComercial, "Nombre comercial", LargoMaximoNombre);
        Direccion = Validar.TextoOpcional(direccion, "Dirección", LargoMaximoDireccion);
        Telefono = Validar.TextoOpcional(telefono, "Teléfono", LargoMaximoTelefono);
    }
}
