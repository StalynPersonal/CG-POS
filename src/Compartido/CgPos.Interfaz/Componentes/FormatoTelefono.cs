namespace CgPos.Interfaz.Componentes;

/// <summary>
/// Formato de los teléfonos en todas las pantallas: solo dígitos, diez como máximo, mostrados como 809-555-1234
/// (doce caracteres con los guiones).
/// </summary>
public static class FormatoTelefono
{
    /// <summary>Largo del teléfono ya formateado, con sus dos guiones.</summary>
    public const int Largo = 12;

    /// <summary>Patrón de la máscara del campo: cada 0 es un dígito.</summary>
    public const string Patron = "000-000-0000";

    private const int Digitos = 10;

    /// <summary>
    /// Deja solo los dígitos (hasta diez) y les pone los guiones a medida que se completan: «8095» queda «809-5». Sirve
    /// también para mostrar los teléfonos que se guardaron sin formato. Nulo si viene nulo.
    /// </summary>
    public static string? Formatear(string? texto)
    {
        if (texto is null)
            return null;

        var digitos = new string(texto.Where(char.IsAsciiDigit).Take(Digitos).ToArray());
        return digitos.Length switch
        {
            <= 3 => digitos,
            <= 6 => $"{digitos[..3]}-{digitos[3..]}",
            _ => $"{digitos[..3]}-{digitos[3..6]}-{digitos[6..]}",
        };
    }
}
