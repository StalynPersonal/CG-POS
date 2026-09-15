using CgPos.Central.Aplicacion.Abstracciones;
using CgPos.Central.Aplicacion.Seguridad;
using CgPos.Contratos.Central;

namespace CgPos.Central.Aplicacion.Organizacion;

/// <summary>
/// Empresa, sucursales, cajas y parámetros (M01). Todo cambio queda en auditoría y baja a las cajas en su próxima descarga. Los parámetros se validan
/// con el catálogo antes de guardarse y no se eliminan: una caja no se enteraría del borrado.
/// </summary>
public interface IServicioOrganizacion
{
    Task<DatosEmpresa?> ObtenerEmpresaAsync(CancellationToken cancelacion = default);

    Task<ResultadoAdministracion> ActualizarEmpresaAsync(SolicitudEmpresa solicitud, UsuarioAuditoria actor, CancellationToken cancelacion = default);

    Task<IReadOnlyList<DatosSucursal>> ListarSucursalesAsync(CancellationToken cancelacion = default);

    Task<ResultadoAdministracion> CrearSucursalAsync(SolicitudSucursal solicitud, UsuarioAuditoria actor, CancellationToken cancelacion = default);

    Task<ResultadoAdministracion> ActualizarSucursalAsync(Guid sucursalId, SolicitudSucursal solicitud, UsuarioAuditoria actor, CancellationToken cancelacion = default);

    /// <summary>Las cajas de una sucursal inactiva no se autentican ante el Central.</summary>
    Task<ResultadoAdministracion> CambiarEstadoSucursalAsync(Guid sucursalId, bool activa, UsuarioAuditoria actor, CancellationToken cancelacion = default);

    Task<IReadOnlyList<DatosCaja>> ListarCajasAsync(CancellationToken cancelacion = default);

    Task<ResultadoAdministracion> CrearCajaAsync(SolicitudCaja solicitud, UsuarioAuditoria actor, CancellationToken cancelacion = default);

    Task<ResultadoAdministracion> ActualizarCajaAsync(Guid cajaId, SolicitudActualizarCaja solicitud, UsuarioAuditoria actor, CancellationToken cancelacion = default);

    /// <summary>Una caja deshabilitada no abre turno (RF-169) ni se autentica ante el Central.</summary>
    Task<ResultadoAdministracion> CambiarEstadoCajaAsync(Guid cajaId, bool habilitada, UsuarioAuditoria actor, CancellationToken cancelacion = default);

    Task<IReadOnlyList<DatosParametro>> ListarParametrosAsync(CancellationToken cancelacion = default);

    Task<ResultadoAdministracion> CrearParametroAsync(SolicitudParametro solicitud, UsuarioAuditoria actor, CancellationToken cancelacion = default);

    Task<ResultadoAdministracion> CambiarValorParametroAsync(Guid parametroId, string valor, UsuarioAuditoria actor, CancellationToken cancelacion = default);
}
