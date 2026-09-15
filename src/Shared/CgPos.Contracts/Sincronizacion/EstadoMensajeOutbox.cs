namespace CgPos.Contracts.Sincronizacion;

/// <summary>Ciclo de vida de un mensaje de la caja hacia el Central (RF-270).</summary>
public enum EstadoMensajeOutbox
{
    /// <summary>Guardado junto al documento, esperando envío.</summary>
    Pendiente = 0,

    /// <summary>Tomado por el proceso de sincronización.</summary>
    EnProceso = 1,

    /// <summary>Transmitido al Central, sin confirmación todavía.</summary>
    Enviado = 2,

    /// <summary>El Central confirmó la recepción (idempotente).</summary>
    Confirmado = 3,

    /// <summary>Falló el envío; se reintenta en <c>ProximoIntentoEn</c>.</summary>
    Error = 4,
}
