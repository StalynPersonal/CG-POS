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

    /// <summary>
    /// Código de sucursal o de caja: siempre dos dígitos (01 a 99). Se acepta escrito con o sin el cero delante y se guarda
    /// con él, para que «1» y «01» sean la misma caja y los números de documento salgan siempre igual.
    /// </summary>
    public static string CodigoDosDigitos(string? valor, string campo)
    {
        var texto = Texto(valor, campo, CodigosCatalogo.LargoSucursalCaja);
        if (texto.Length > CodigosCatalogo.LargoSucursalCaja || !texto.All(char.IsAsciiDigit))
            throw new ArgumentException($"{campo} debe ser un número de dos dígitos, entre 01 y 99.", campo);

        var numero = int.Parse(texto, System.Globalization.CultureInfo.InvariantCulture);
        if (numero is < 1 or > CodigosCatalogo.MaximoSucursalCaja)
            throw new ArgumentException($"{campo} debe ser un número de dos dígitos, entre 01 y 99.", campo);

        return numero.ToString("00", System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>Código numérico de un catálogo (lo sugiere el Central y no cambia después de crear el registro).</summary>
    public static int Codigo(int valor, string campo, int maximo = CodigosCatalogo.Maximo)
    {
        if (valor < 1 || valor > maximo)
            throw new ArgumentException($"{campo} debe ser un número entre 1 y {maximo:N0}.", campo);

        return valor;
    }

    /// <summary>Id de una entidad ya agregada a la base (mayor que cero).</summary>
    public static int Id(int valor, string campo)
    {
        if (valor <= 0)
            throw new ArgumentException($"{campo} es obligatorio.", campo);

        return valor;
    }
}
