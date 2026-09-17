namespace CgPos.Dominio.Organizacion;

/// <summary>
/// Una regla de negocio sin configurar no se sustituye por un valor fijo en el código: la operación se rechaza con este motivo
/// para que un usuario la configure en el Central. La usan la caja y el Central.
/// </summary>
public sealed class ParametroNoConfiguradoExcepcion(string clave, string? detalle = null)
    : Exception(detalle is null
        ? $"Falta configurar el parámetro {Nombre(clave)}. Configúrelo en el Central."
        : $"El parámetro {Nombre(clave)} {detalle}. Corríjalo en el Central.")
{
    public string Clave { get; } = clave;

    /// <summary>Lo que ve el usuario: la descripción del catálogo y, entre paréntesis, la clave técnica para buscarla o darla a soporte.</summary>
    internal static string Nombre(string clave) =>
        CatalogoParametros.Buscar(clave) is { } definicion ? $"«{definicion.Descripcion}» ({definicion.Clave})" : $"«{clave}»";
}
