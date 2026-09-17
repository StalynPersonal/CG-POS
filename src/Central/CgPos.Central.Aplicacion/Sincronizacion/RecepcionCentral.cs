using CgPos.Contratos.Sincronizacion;

namespace CgPos.Central.Aplicacion.Sincronizacion;

/// <summary>Caja autenticada que envía el mensaje (sale de su token de dispositivo, no del mensaje).</summary>
/// <summary>La caja autenticada: sus Id en el Central y los códigos con que se identifica.</summary>
public sealed record CajaRemitente(Guid CajaId, Guid SucursalId, int SucursalCodigo = 0, int CajaCodigo = 0);

public interface IServicioRecepcion
{
    /// <summary>
    /// Recibe un documento de la bandeja de salida de una caja (RF-274): valida que sea de la caja autenticada, el hash del contenido y el del XML
    /// del e-CF (RF-276), lo guarda una sola vez (RF-271, RN-25) y registra los conflictos (RF-272, RN-24).
    /// </summary>
    Task<RespuestaRecepcionCentral> RecibirAsync(MensajeSincronizacion mensaje, CajaRemitente remitente, CancellationToken cancelacion = default);
}
