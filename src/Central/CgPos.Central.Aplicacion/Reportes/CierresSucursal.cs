using CgPos.Central.Aplicacion.Abstracciones;
using CgPos.Central.Aplicacion.Seguridad;
using CgPos.Contratos.Central;

namespace CgPos.Central.Aplicacion.Reportes;

/// <summary>
/// Cierre consolidado de sucursal (RF-264): al terminar el día se agrupan los cierres de caja de la sucursal, se calcula el efectivo a depositar
/// y se registran los depósitos. Solo se consolida cuando todos los turnos con ventas del día están cerrados.
/// </summary>
public interface IServicioCierresSucursal
{
    /// <returns>Nulo si la sucursal no existe.</returns>
    Task<DatosPreparacionCierreSucursal?> PrepararAsync(int sucursalId, DateOnly fechaOperacion, CancellationToken cancelacion = default);

    Task<ResultadoAdministracion> ConsolidarAsync(SolicitudCierreSucursal solicitud, UsuarioAuditoria actor, CancellationToken cancelacion = default);

    Task<IReadOnlyList<DatosCierreSucursal>> ListarAsync(int? sucursalId, DateOnly desde, DateOnly hasta, CancellationToken cancelacion = default);

    Task<DatosCierreSucursal?> ObtenerAsync(int cierreSucursalId, CancellationToken cancelacion = default);
}
