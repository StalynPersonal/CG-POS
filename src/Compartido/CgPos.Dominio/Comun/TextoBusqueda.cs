using System.Globalization;
using System.Text;

namespace CgPos.Dominio.Comun;

public static class TextoBusqueda
{
    public const int LargoMaximo = 1000;

    /// <summary>Minúsculas y sin acentos, para que "jabon" encuentre "Jabón".</summary>
    public static string? Normalizar(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
            return null;

        var descompuesto = texto.Trim().Normalize(NormalizationForm.FormD);
        var limpio = new StringBuilder(descompuesto.Length);
        foreach (var caracter in descompuesto.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark))
            limpio.Append(char.ToLowerInvariant(caracter));

        var normalizado = limpio.ToString().Normalize(NormalizationForm.FormC);
        return normalizado.Length > LargoMaximo ? normalizado[..LargoMaximo] : normalizado;
    }
}
