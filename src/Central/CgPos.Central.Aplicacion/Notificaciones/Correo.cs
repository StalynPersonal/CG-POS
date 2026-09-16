namespace CgPos.Central.Aplicacion.Notificaciones;

/// <summary>Correo que el Central envía al cliente o al personal (ej. su pedido está listo).</summary>
public sealed record MensajeCorreo(string Destinatario, string Asunto, string Cuerpo);

public sealed record ResultadoCorreo(bool Enviado, string? Error)
{
    public static ResultadoCorreo Correcto() => new(true, null);

    public static ResultadoCorreo Fallo(string error) => new(false, error);
}

/// <summary>
/// Envío de correo desde el Central por el servidor SMTP de la empresa. Sin servidor configurado no se envía nada y la operación
/// sigue su curso: avisar al cliente nunca puede detener el despacho.
/// </summary>
public interface IServicioCorreo
{
    /// <summary>Hay servidor y remitente configurados.</summary>
    Task<bool> ConfiguradoAsync(CancellationToken cancelacion = default);

    Task<ResultadoCorreo> EnviarAsync(MensajeCorreo mensaje, CancellationToken cancelacion = default);
}

/// <summary>Avisa al cliente que su pedido quedó preparado (RF-256); los que no tienen correo quedan para llamarlos por teléfono.</summary>
public interface IAvisosDespacho
{
    /// <returns>Cuántos avisos se enviaron.</returns>
    Task<int> AvisarPreparadosAsync(int maximo, CancellationToken cancelacion = default);
}
