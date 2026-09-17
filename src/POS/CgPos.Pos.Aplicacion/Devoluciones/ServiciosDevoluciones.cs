using CgPos.Contratos.Ventas;
using CgPos.Pos.Aplicacion.Seguridad;

namespace CgPos.Pos.Aplicacion.Devoluciones;

/// <summary>Devoluciones y notas de crédito de la caja (M10). Todo funciona sin conexión; las facturas de otra caja o sucursal esperan al Central.</summary>
public interface IServicioDevoluciones
{
    /// <summary>Llama la factura por número de transacción o e-NCF (RF-56, RF-161).</summary>
    Task<RespuestaFacturaDevolucion> BuscarFacturaAsync(SesionUsuario sesion, string numero, CancellationToken cancelacion = default);

    /// <summary>
    /// Registra la devolución con autorización del encargado (RF-162), emite la nota de crédito E34 en la misma transacción (RF-227)
    /// e imprime la copia del cliente y la de contabilidad (RF-163).
    /// </summary>
    Task<RespuestaDevolucion> RegistrarAsync(SesionUsuario sesion, SolicitudDevolucion solicitud, CancellationToken cancelacion = default);

    /// <summary>Saldo de una nota de crédito por su e-NCF o número, con mensaje si está consumida, vencida o no existe (RF-43).</summary>
    Task<RespuestaSaldoNotaCredito> ConsultarNotaCreditoAsync(SesionUsuario sesion, string codigo, CancellationToken cancelacion = default);

    Task<RespuestaDevolucion> ReimprimirAsync(SesionUsuario sesion, int devolucionId, CancellationToken cancelacion = default);

    Task<IReadOnlyList<DatosMotivoDevolucion>> ListarMotivosAsync(CancellationToken cancelacion = default);
}
