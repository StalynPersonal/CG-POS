namespace CgPos.Pos.Application.Abstracciones;

/// <summary>
/// Encola mensajes para el Central dentro de la unidad de trabajo actual.
/// No guarda por sí mismo: el mensaje se persiste con el mismo <c>SaveChanges</c> del documento (misma transacción).
/// </summary>
public interface IOutbox
{
    /// <returns>Id del mensaje, que es también su clave de idempotencia.</returns>
    Guid Encolar<T>(string tipoMensaje, Guid agregadoId, T contenido);
}
