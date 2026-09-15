using System.Globalization;

namespace CgPos.Dominio.Globalizacion;

/// <summary>
/// Cultura de República Dominicana con formatos fijados explícitamente (RNF-27):
/// montos 2,175.34 · fecha dd/MM/yyyy. El símbolo de moneda es el de la moneda local configurada en el Central.
/// No depende de los datos ICU/NLS del equipo o del navegador, que varían entre plataformas.
/// </summary>
public static class CulturaRd
{
    public const string Nombre = "es-DO";

    /// <param name="simboloMoneda">Símbolo de la moneda local; sin él el formato "C" no muestra símbolo.</param>
    public static CultureInfo Crear(string? simboloMoneda = null)
    {
        var cultura = (CultureInfo)CultureInfo.GetCultureInfo(Nombre).Clone();

        var numeros = cultura.NumberFormat;
        numeros.NumberDecimalSeparator = ".";
        numeros.NumberGroupSeparator = ",";
        numeros.CurrencyDecimalSeparator = ".";
        numeros.CurrencyGroupSeparator = ",";
        numeros.CurrencySymbol = simboloMoneda ?? string.Empty;
        numeros.CurrencyDecimalDigits = 2;
        numeros.CurrencyPositivePattern = 0; // RD$850.00
        numeros.CurrencyNegativePattern = 1; // -RD$850.00
        numeros.PercentDecimalSeparator = ".";
        numeros.PercentGroupSeparator = ",";

        var fechas = cultura.DateTimeFormat;
        fechas.ShortDatePattern = "dd/MM/yyyy";
        fechas.DateSeparator = "/";

        return cultura;
    }

    /// <summary>Fija la cultura RD como actual y por defecto para todos los hilos.</summary>
    public static CultureInfo Aplicar(string? simboloMoneda = null)
    {
        var cultura = Crear(simboloMoneda);
        CultureInfo.DefaultThreadCurrentCulture = cultura;
        CultureInfo.DefaultThreadCurrentUICulture = cultura;
        CultureInfo.CurrentCulture = cultura;
        CultureInfo.CurrentUICulture = cultura;
        return cultura;
    }
}
