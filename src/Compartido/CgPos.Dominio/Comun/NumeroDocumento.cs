using System.Globalization;

namespace CgPos.Dominio.Comun;

/// <summary>Tipo de documento numerado; su dígito va en el número, después de la sucursal y la caja.</summary>
public enum TipoDocumentoNumerado
{
    Factura = 1,
    NotaCredito = 2,
    PendienteEntrega = 3,
}

/// <summary>
/// Número de un documento de la caja, único en toda la empresa: código de sucursal (2) + código de caja (2) + dígito del tipo (1) + secuencia
/// rellena con ceros a los dígitos configurados (ej. factura de la sucursal 01, caja 01, secuencia 1 con 7 dígitos = 010110000001).
/// Si la secuencia supera esos dígitos, el número crece: nunca se repite. Caja y Central identifican cada documento por él.
/// </summary>
public static class NumeroDocumento
{
    /// <summary>Largo con el que se guarda cualquier número de documento de la caja: todos guardan el mismo dato.</summary>
    public const int LargoMaximo = 40;

    public const int DigitosMinimosSecuencia = 5;

    /// <summary>Mayor cantidad de dígitos de la secuencia (un billón de documentos por caja y tipo).</summary>
    public const int DigitosMaximosSecuencia = 12;

    /// <summary>Largo de sucursal, caja y tipo antes de la secuencia.</summary>
    public const int LargoPrefijo = 5;

    public static string Formatear(string codigoSucursal, string codigoCaja, TipoDocumentoNumerado tipo, long secuencia, int digitos)
    {
        var sucursal = Codigo(codigoSucursal, nameof(codigoSucursal));
        var caja = Codigo(codigoCaja, nameof(codigoCaja));
        if (!Enum.IsDefined(tipo))
            throw new ArgumentOutOfRangeException(nameof(tipo), tipo, "Tipo de documento desconocido.");
        ArgumentOutOfRangeException.ThrowIfLessThan(secuencia, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(digitos, DigitosMinimosSecuencia);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(digitos, DigitosMaximosSecuencia);

        return sucursal + caja + ((int)tipo).ToString(CultureInfo.InvariantCulture)
            + secuencia.ToString(new string('0', digitos), CultureInfo.InvariantCulture);
    }

    /// <summary>Un código de sucursal o de caja entra con sus dos dígitos; se acepta «1» y se usa como «01».</summary>
    private static string Codigo(string valor, string campo)
    {
        var texto = (valor ?? string.Empty).Trim();
        if (texto.Length is 0 or > CodigosCatalogo.LargoSucursalCaja || !texto.All(char.IsAsciiDigit))
            throw new ArgumentException($"El código debe ser de dos dígitos, entre 01 y 99.", campo);

        var numero = int.Parse(texto, CultureInfo.InvariantCulture);
        ArgumentOutOfRangeException.ThrowIfLessThan(numero, 1, campo);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(numero, CodigosCatalogo.MaximoSucursalCaja, campo);
        return numero.ToString("00", CultureInfo.InvariantCulture);
    }

    /// <summary>Lee sucursal, caja y tipo de un número; <c>false</c> si no tiene el formato de un documento de caja.</summary>
    public static bool TryLeer(string? numero, out string codigoSucursal, out string codigoCaja, out TipoDocumentoNumerado tipo)
    {
        codigoSucursal = string.Empty;
        codigoCaja = string.Empty;
        tipo = default;
        var texto = numero?.Trim();
        if (texto is null || texto.Length < LargoPrefijo + DigitosMinimosSecuencia || !texto.All(char.IsAsciiDigit))
            return false;

        codigoSucursal = texto[..2];
        codigoCaja = texto[2..4];
        tipo = (TipoDocumentoNumerado)(texto[4] - '0');
        return codigoSucursal != "00" && codigoCaja != "00" && Enum.IsDefined(tipo)
            && long.Parse(texto[LargoPrefijo..], CultureInfo.InvariantCulture) >= 1;
    }

    /// <summary><c>true</c> si el número es de un documento de ese tipo.</summary>
    public static bool EsDeTipo(string? numero, TipoDocumentoNumerado tipo) => TryLeer(numero, out _, out _, out var leido) && leido == tipo;
}
