namespace CgPos.Pos.Aplicacion.Abstracciones;

/// <summary>
/// Encola mensajes para el Central dentro de la unidad de trabajo actual.
/// No guarda por sí mismo: el mensaje se persiste con el mismo <c>SaveChanges</c> del documento (misma transacción).
/// </summary>
public interface IBandejaSalida
{
    /// <param name="referencia">Número del documento o su llave natural; nunca un Id de la caja.</param>
    /// <returns>Id del mensaje, que es también su clave de idempotencia.</returns>
    Guid Encolar<T>(string tipoMensaje, string referencia, T contenido);
}
