using System.Globalization;

namespace CgPos.Dominio.Globalizacion;

/// <summary>
/// Cultura de República Dominicana con formatos fijados explícitamente (RNF-27):
/// montos 2,175.34 · moneda RD$ · fecha dd/MM/yyyy.
/// No depende de los datos ICU/NLS del equipo o del navegador, que varían entre plataformas.
/// </summary>
public static class CulturaRd
{
    public const string Nombre = "es-DO";

    public static CultureInfo Crear()
    {
        var cultura = (CultureInfo)CultureInfo.GetCultureInfo(Nombre).Clone();

        var numeros = cultura.NumberFormat;
        numeros.NumberDecimalSeparator = ".";
        numeros.NumberGroupSeparator = ",";
        numeros.CurrencyDecimalSeparator = ".";
        numeros.CurrencyGroupSeparator = ",";
        numeros.CurrencySymbol = "RD$";
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
    public static CultureInfo Aplicar()
    {
        var cultura = Crear();
        CultureInfo.DefaultThreadCurrentCulture = cultura;
        CultureInfo.DefaultThreadCurrentUICulture = cultura;
        CultureInfo.CurrentCulture = cultura;
        CultureInfo.CurrentUICulture = cultura;
        return cultura;
    }
}
