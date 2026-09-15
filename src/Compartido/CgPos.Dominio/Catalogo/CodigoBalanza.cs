namespace CgPos.Dominio.Catalogo;

public enum TipoValorBalanza
{
    Peso,
    Precio,
}

/// <summary>
/// Formato de las etiquetas de balanza (RF-180). Por defecto EAN-13: prefijo (2) + código del artículo (5)
/// + valor (5) + dígito de control (1). Prefijo "21" = peso con 3 decimales; "22" = precio con 2 decimales.
/// </summary>
public sealed record FormatoCodigoBalanza(
    string PrefijoPeso = "21",
    string PrefijoPrecio = "22",
    int DigitosCodigoArticulo = 5,
    int DigitosValor = 5,
    int DecimalesPeso = 3,
    int DecimalesPrecio = 2)
{
    public int LargoTotal => PrefijoPeso.Length + DigitosCodigoArticulo + DigitosValor + 1;
}

public sealed record LecturaBalanza(string CodigoArticulo, TipoValorBalanza Tipo, decimal Valor);

public static class ReglasBalanza
{
    /// <summary>
    /// Peso a facturar de una lectura en vivo de la balanza: bruto menos la tara del empaque (RF-196), a 3 decimales.
    /// Las etiquetas impresas por la balanza ya traen el peso neto y no pasan por aquí.
    /// </summary>
    /// <returns>Nulo si la lectura no deja un peso neto positivo.</returns>
    public static decimal? PesoNeto(decimal pesoBruto, decimal? tara)
    {
        var neto = decimal.Round(pesoBruto - (tara ?? 0m), 3, MidpointRounding.AwayFromZero);
        return neto > 0 ? neto : null;
    }
}

public static class InterpreteCodigoBalanza
{
    public static bool TryInterpretar(string? codigo, FormatoCodigoBalanza formato, out LecturaBalanza? lectura)
    {
        ArgumentNullException.ThrowIfNull(formato);
        lectura = null;

        var limpio = codigo?.Trim() ?? string.Empty;
        if (limpio.Length != formato.LargoTotal || !limpio.All(char.IsAsciiDigit))
            return false;

        TipoValorBalanza tipo;
        string prefijo;
        if (limpio.StartsWith(formato.PrefijoPeso, StringComparison.Ordinal))
        {
            tipo = TipoValorBalanza.Peso;
            prefijo = formato.PrefijoPeso;
        }
        else if (limpio.StartsWith(formato.PrefijoPrecio, StringComparison.Ordinal))
        {
            tipo = TipoValorBalanza.Precio;
            prefijo = formato.PrefijoPrecio;
        }
        else
        {
            return false;
        }

        if (!DigitoControlGs1Valido(limpio))
            return false;

        var codigoArticulo = limpio.Substring(prefijo.Length, formato.DigitosCodigoArticulo);
        var textoValor = limpio.Substring(prefijo.Length + formato.DigitosCodigoArticulo, formato.DigitosValor);
        var decimales = tipo == TipoValorBalanza.Peso ? formato.DecimalesPeso : formato.DecimalesPrecio;
        var valor = long.Parse(textoValor) / Potencia10(decimales);

        lectura = new LecturaBalanza(codigoArticulo, tipo, valor);
        return true;
    }

    /// <summary>Dígito de control GS1 (EAN/UPC) para los datos sin el dígito final.</summary>
    public static int CalcularDigitoControlGs1(string datos)
    {
        var suma = 0;
        var peso = 3;
        for (var posicion = datos.Length - 1; posicion >= 0; posicion--)
        {
            suma += (datos[posicion] - '0') * peso;
            peso = peso == 3 ? 1 : 3;
        }

        return (10 - suma % 10) % 10;
    }

    public static bool DigitoControlGs1Valido(string codigo) =>
        codigo.Length > 1 && codigo.All(char.IsAsciiDigit) && CalcularDigitoControlGs1(codigo[..^1]) == codigo[^1] - '0';

    private static decimal Potencia10(int exponente)
    {
        var resultado = 1m;
        for (var i = 0; i < exponente; i++)
            resultado *= 10;
        return resultado;
    }
}
