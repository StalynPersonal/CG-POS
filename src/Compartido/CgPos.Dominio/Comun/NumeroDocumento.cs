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
    public const int DigitosMinimosSecuencia = 5;

    /// <summary>Mayor cantidad de dígitos de la secuencia (un billón de documentos por caja y tipo).</summary>
    public const int DigitosMaximosSecuencia = 12;

    /// <summary>Largo de sucursal, caja y tipo antes de la secuencia.</summary>
    public const int LargoPrefijo = 5;

    public static string Formatear(int codigoSucursal, int codigoCaja, TipoDocumentoNumerado tipo, long secuencia, int digitos)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(codigoSucursal, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(codigoSucursal, CodigosCatalogo.MaximoSucursalCaja);
        ArgumentOutOfRangeException.ThrowIfLessThan(codigoCaja, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(codigoCaja, CodigosCatalogo.MaximoSucursalCaja);
        if (!Enum.IsDefined(tipo))
            throw new ArgumentOutOfRangeException(nameof(tipo), tipo, "Tipo de documento desconocido.");
        ArgumentOutOfRangeException.ThrowIfLessThan(secuencia, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(digitos, DigitosMinimosSecuencia);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(digitos, DigitosMaximosSecuencia);

        return codigoSucursal.ToString("00", CultureInfo.InvariantCulture) + codigoCaja.ToString("00", CultureInfo.InvariantCulture)
            + ((int)tipo).ToString(CultureInfo.InvariantCulture) + secuencia.ToString(new string('0', digitos), CultureInfo.InvariantCulture);
    }

    /// <summary>Lee sucursal, caja y tipo de un número; <c>false</c> si no tiene el formato de un documento de caja.</summary>
    public static bool TryLeer(string? numero, out int codigoSucursal, out int codigoCaja, out TipoDocumentoNumerado tipo)
    {
        codigoSucursal = 0;
        codigoCaja = 0;
        tipo = default;
        var texto = numero?.Trim();
        if (texto is null || texto.Length < LargoPrefijo + DigitosMinimosSecuencia || !texto.All(char.IsAsciiDigit))
            return false;

        codigoSucursal = int.Parse(texto[..2], CultureInfo.InvariantCulture);
        codigoCaja = int.Parse(texto[2..4], CultureInfo.InvariantCulture);
        tipo = (TipoDocumentoNumerado)(texto[4] - '0');
        return codigoSucursal >= 1 && codigoCaja >= 1 && Enum.IsDefined(tipo) && long.Parse(texto[LargoPrefijo..], CultureInfo.InvariantCulture) >= 1;
    }

    /// <summary><c>true</c> si el número es de un documento de ese tipo.</summary>
    public static bool EsDeTipo(string? numero, TipoDocumentoNumerado tipo) => TryLeer(numero, out _, out _, out var leido) && leido == tipo;
}
