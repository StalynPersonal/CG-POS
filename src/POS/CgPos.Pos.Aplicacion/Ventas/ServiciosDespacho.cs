using CgPos.Contratos.Ventas;
using CgPos.Pos.Aplicacion.Seguridad;

namespace CgPos.Pos.Aplicacion.Ventas;

/// <summary>
/// Módulo de despacho (M12): se escanea el voucher o la factura, se prepara la mercancía y se registra la entrega (RF-251 a RF-255).
/// Opera con los pendientes de esta caja; los de otras cajas y sucursales llegan con el Central.
/// </summary>
public interface IServicioDespacho
{
    /// <summary>Pendientes del voucher (número de pendiente) o de la factura (número de transacción o e-NCF) escaneada.</summary>
    Task<RespuestaBusquedaPendientes> BuscarAsync(SesionUsuario sesion, string codigo, CancellationToken cancelacion = default);

    /// <summary>Pendientes abiertos de la caja, los de fecha comprometida más cercana primero.</summary>
    Task<IReadOnlyList<DatosPendienteEntrega>> ListarAbiertosAsync(SesionUsuario sesion, CancellationToken cancelacion = default);

    Task<RespuestaPendiente> CambiarEstadoAsync(SesionUsuario sesion, Guid pendienteId, SolicitudEstadoPendiente solicitud, CancellationToken cancelacion = default);

    /// <summary>Registra la entrega total o parcial e imprime la constancia firmada por quien recibe (RF-253, RF-254).</summary>
    Task<RespuestaPendiente> EntregarAsync(SesionUsuario sesion, Guid pendienteId, SolicitudEntregaPendiente solicitud, CancellationToken cancelacion = default);

    /// <summary>Anula un pendiente sin entregas con motivo y autorización, liberando la mercancía (RF-255).</summary>
    Task<RespuestaPendiente> AnularAsync(SesionUsuario sesion, Guid pendienteId, SolicitudAnularPendiente solicitud, CancellationToken cancelacion = default);
}
