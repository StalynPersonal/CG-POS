using CgPos.Dominio.Comun;

namespace CgPos.Dominio.Fiscal;

/// <summary>Registro del padrón de RNC/cédula de la DGII, disponible en la base local para validar sin conexión (RF-182).</summary>
public sealed class ContribuyenteDgii
{
    public const int LargoMaximoNombre = 250;
    public const int LargoMaximoEstado = 50;

    private ContribuyenteDgii()
    {
    }

    /// <summary>RNC (9 dígitos) o cédula (11 dígitos), sin guiones.</summary>
    public string Documento { get; private set; } = string.Empty;

    public string RazonSocial { get; private set; } = string.Empty;
    public string? NombreComercial { get; private set; }

    /// <summary>Estado según la DGII, ej. "ACTIVO", "SUSPENDIDO", "DADO DE BAJA".</summary>
    public string? Estado { get; private set; }

    public string? RegimenPago { get; private set; }
    public DateTimeOffset ActualizadoEn { get; private set; }

    public bool EstaActivo => string.Equals(Estado?.Trim(), "ACTIVO", StringComparison.OrdinalIgnoreCase);

    public static ContribuyenteDgii Crear(string documento, string razonSocial, string? nombreComercial, string? estado, string? regimenPago, DateTimeOffset actualizadoEn)
    {
        var normalizado = DocumentoIdentidad.Normalizar(documento);
        if (normalizado.Length is not (DocumentoIdentidad.LargoRnc or DocumentoIdentidad.LargoCedula) || !normalizado.All(char.IsAsciiDigit))
            throw new ArgumentException($"El documento '{documento}' no es un RNC ni una cédula.", nameof(documento));

        var contribuyente = new ContribuyenteDgii { Documento = normalizado };
        contribuyente.Actualizar(razonSocial, nombreComercial, estado, regimenPago, actualizadoEn);
        return contribuyente;
    }

    public void Actualizar(string razonSocial, string? nombreComercial, string? estado, string? regimenPago, DateTimeOffset actualizadoEn)
    {
        RazonSocial = Validar.Texto(razonSocial, "Razón social", LargoMaximoNombre);
        NombreComercial = Validar.TextoOpcional(nombreComercial, "Nombre comercial", LargoMaximoNombre);
        Estado = Validar.TextoOpcional(estado, "Estado", LargoMaximoEstado);
        RegimenPago = Validar.TextoOpcional(regimenPago, "Régimen de pago", LargoMaximoEstado);
        ActualizadoEn = actualizadoEn;
    }
}
