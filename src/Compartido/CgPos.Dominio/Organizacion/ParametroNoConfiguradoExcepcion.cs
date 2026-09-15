namespace CgPos.Dominio.Organizacion;

/// <summary>
/// Una regla de negocio sin configurar no se sustituye por un valor fijo en el código: la operación se rechaza con este motivo
/// para que un usuario la configure en el Central. La usan la caja y el Central.
/// </summary>
public sealed class ParametroNoConfiguradoExcepcion(string clave, string? detalle = null)
    : Exception(detalle is null
        ? $"Falta configurar el parámetro «{clave}». Configúrelo en el Central."
        : $"El parámetro «{clave}» {detalle}. Corríjalo en el Central.")
{
    public string Clave { get; } = clave;
}
