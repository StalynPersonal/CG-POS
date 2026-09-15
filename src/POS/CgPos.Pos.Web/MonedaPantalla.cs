using System.Globalization;

namespace CgPos.Pos.Web;

/// <summary>
/// Moneda local en las pantallas. La cultura se fija al arrancar con el símbolo de la moneda que el Central configuró para la caja,
/// así los montos se formatean con "C" y las etiquetas usan <see cref="Simbolo"/>.
/// </summary>
public static class MonedaPantalla
{
    public static string Simbolo => CultureInfo.CurrentCulture.NumberFormat.CurrencySymbol;
}
