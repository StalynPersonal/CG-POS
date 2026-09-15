namespace CgPos.Dominio.Fiscal;

public enum TipoDocumentoIdentidad
{
    Cedula,
    Rnc,
    Pasaporte,
}

/// <summary>Tipos de comprobante fiscal electrónico (e-CF) de la DGII. El valor es el código del tipo.</summary>
public enum TipoComprobante
{
    FacturaCreditoFiscal = 31,
    FacturaConsumo = 32,
    NotaDebito = 33,
    NotaCredito = 34,
    Compras = 41,
    GastosMenores = 43,
    RegimenesEspeciales = 44,
    Gubernamental = 45,
    Exportaciones = 46,
    PagosExterior = 47,
}

public sealed record ResultadoValidacionDocumento(
    string Documento,
    TipoDocumentoIdentidad? Tipo,
    bool FormatoValido,
    bool DigitoVerificadorValido)
{
    public bool EsValido => FormatoValido && DigitoVerificadorValido;
}

/// <summary>
/// Validación de RNC (9 dígitos) y cédula (11 dígitos) con los algoritmos de dígito verificador de la DGII.
/// Hay cédulas antiguas que no cumplen el dígito verificador: por eso el resultado separa formato y dígito,
/// y la consulta al padrón local tiene la última palabra.
/// </summary>
public static class DocumentoIdentidad
{
    public const int LargoRnc = 9;
    public const int LargoCedula = 11;

    private static readonly int[] PesosRnc = [7, 9, 8, 6, 5, 4, 3, 2];

    /// <summary>Quita guiones, espacios y cualquier carácter que no sea letra o dígito.</summary>
    public static string Normalizar(string? documento) =>
        new string((documento ?? string.Empty).Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();

    public static ResultadoValidacionDocumento Validar(string? documento)
    {
        var normalizado = Normalizar(documento);

        if (normalizado.Length > 0 && normalizado.All(char.IsAsciiDigit))
        {
            if (normalizado.Length == LargoRnc)
                return new(normalizado, TipoDocumentoIdentidad.Rnc, true, RncValido(normalizado));
            if (normalizado.Length == LargoCedula)
                return new(normalizado, TipoDocumentoIdentidad.Cedula, true, CedulaValida(normalizado));
        }

        return new(normalizado, null, false, false);
    }

    public static bool RncValido(string rnc)
    {
        if (rnc is not { Length: LargoRnc } || !rnc.All(char.IsAsciiDigit))
            return false;

        var suma = 0;
        for (var posicion = 0; posicion < PesosRnc.Length; posicion++)
            suma += (rnc[posicion] - '0') * PesosRnc[posicion];

        var resto = suma % 11;
        var digito = resto switch
        {
            0 => 2,
            1 => 1,
            _ => 11 - resto,
        };

        return digito == rnc[LargoRnc - 1] - '0';
    }

    public static bool CedulaValida(string cedula)
    {
        if (cedula is not { Length: LargoCedula } || !cedula.All(char.IsAsciiDigit))
            return false;

        var suma = 0;
        for (var posicion = 0; posicion < LargoCedula - 1; posicion++)
        {
            var producto = (cedula[posicion] - '0') * (posicion % 2 == 0 ? 1 : 2);
            suma += producto >= 10 ? producto - 9 : producto;
        }

        var digito = (10 - suma % 10) % 10;
        return digito == cedula[LargoCedula - 1] - '0';
    }
}
