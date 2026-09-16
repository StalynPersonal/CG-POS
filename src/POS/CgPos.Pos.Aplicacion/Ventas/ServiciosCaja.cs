using CgPos.Contratos.Ventas;
using CgPos.Pos.Aplicacion.Seguridad;

namespace CgPos.Pos.Aplicacion.Ventas;

/// <summary>
/// Operaciones del turno (M13): resumen, pre-cierre, retiros, relevo, cierre ciego por forma de pago y denominaciones, y reapertura.
/// Todo funciona sin conexión y queda en la bandeja de salida para el Central (RF-267).
/// </summary>
public interface IServicioCaja
{
    /// <summary>Estado del turno abierto de la caja; en cierre ciego oculta lo esperado a quien no tiene permiso de pre-cierre.</summary>
    Task<RespuestaCaja> ObtenerResumenAsync(SesionUsuario sesion, CancellationToken cancelacion = default);

    /// <summary>Imprime lo esperado por forma de pago sin cerrar el turno, con permiso o clave de supervisor (RF-8).</summary>
    Task<RespuestaCaja> PreCierreAsync(SesionUsuario sesion, Guid? autorizacionId, CancellationToken cancelacion = default);

    /// <summary>
    /// Cierra el lote del terminal y cuadra las tarjetas del turno contra lo que reportó (RF-215). No cambia nada de la venta:
    /// es una comprobación para el cierre.
    /// </summary>
    Task<DatosConciliacionTarjetas> ConciliarTarjetasAsync(SesionUsuario sesion, CancellationToken cancelacion = default);

    /// <summary>Retiro parcial de efectivo con autorización, comprobante impreso y apertura de gaveta (RF-261).</summary>
    Task<RespuestaCaja> RetirarEfectivoAsync(SesionUsuario sesion, decimal monto, string? motivo, Guid? autorizacionId, CancellationToken cancelacion = default);

    /// <summary>El usuario de la sesión toma el turno abierto de otro cajero sin cerrarlo (RF-260).</summary>
    Task<RespuestaCaja> RelevarAsync(SesionUsuario sesion, Guid? autorizacionId, CancellationToken cancelacion = default);

    /// <summary>
    /// Cierra el turno con lo declarado por forma de pago y el conteo por denominaciones (RF-262, RF-263). No se permite con facturas en espera,
    /// transacciones en curso con artículos o ventas sin e-CF firmado (RF-265, RN-22).
    /// </summary>
    Task<RespuestaCaja> CerrarAsync(SesionUsuario sesion, IReadOnlyList<SolicitudDeclaracionFormaPago> declaraciones,
        IReadOnlyList<SolicitudConteoDenominacion> conteo, Guid? autorizacionId, CancellationToken cancelacion = default);

    /// <summary>Reabre el último cierre de la caja con autorización de nivel superior y motivo (RF-266).</summary>
    Task<RespuestaCaja> ReabrirCierreAsync(SesionUsuario sesion, Guid cierreId, string? motivo, Guid? autorizacionId, CancellationToken cancelacion = default);

    Task<IReadOnlyList<DatosCierre>> ListarCierresAsync(SesionUsuario sesion, int maximo = 20, CancellationToken cancelacion = default);

    Task<RespuestaCaja> ReimprimirCierreAsync(SesionUsuario sesion, Guid cierreId, CancellationToken cancelacion = default);
}
