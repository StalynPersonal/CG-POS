namespace CgPos.Dominio.Comun;

/// <summary>Validaciones de entrada comunes a las entidades (mensajes en español).</summary>
internal static class Validar
{
    public static string Texto(string? valor, string campo, int largoMaximo)
    {
        if (string.IsNullOrWhiteSpace(valor))
            throw new ArgumentException($"{campo} es obligatorio.", campo);

        var texto = valor.Trim();
        if (texto.Length > largoMaximo)
            throw new ArgumentException($"{campo} no puede exceder {largoMaximo} caracteres.", campo);

        return texto;
    }

    public static string? TextoOpcional(string? valor, string campo, int largoMaximo) =>
        string.IsNullOrWhiteSpace(valor) ? null : Texto(valor, campo, largoMaximo);

    public static string Digitos(string? valor, string campo, int largo)
    {
        var texto = Texto(valor, campo, largo);
        if (texto.Length != largo || !texto.All(char.IsAsciiDigit))
            throw new ArgumentException($"{campo} debe tener exactamente {largo} dígitos.", campo);

        return texto;
    }

    /// <summary>Código numérico de un catálogo (lo sugiere el Central y no cambia después de crear el registro).</summary>
    public static int Codigo(int valor, string campo, int maximo = CodigosCatalogo.Maximo)
    {
        if (valor < 1 || valor > maximo)
            throw new ArgumentException($"{campo} debe ser un número entre 1 y {maximo:N0}.", campo);

        return valor;
    }

    public static Guid Id(Guid valor, string campo)
    {
        if (valor == Guid.Empty)
            throw new ArgumentException($"{campo} es obligatorio.", campo);

        return valor;
    }
}
