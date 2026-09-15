namespace CgPos.Contratos.Sincronizacion;

/// <summary>
/// Mensaje de la bandeja de salida tal como viaja al Central. <paramref name="Id"/> es la clave de idempotencia (RN-25) y
/// <paramref name="HashContenido"/> el SHA-256 del contenido, que el Central verifica antes de confirmar.
/// </summary>
public sealed record MensajeSincronizacion(
    Guid Id,
    string TipoMensaje,
    Guid AgregadoId,
    string Contenido,
    string HashContenido,
    Guid CajaId,
    DateTimeOffset CreadoEn);

public enum EstadoRecepcion
{
    Recibido,

    /// <summary>Ya se había recibido con el mismo contenido: se confirma sin duplicar.</summary>
    Duplicado,

    Rechazado,
}

/// <summary>Respuesta del Central a la recepción de un mensaje.</summary>
public sealed record RespuestaRecepcionCentral(EstadoRecepcion Estado, string? Error = null);
