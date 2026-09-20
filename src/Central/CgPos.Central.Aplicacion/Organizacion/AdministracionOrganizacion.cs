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

    Task<ResultadoAdministracion> ActualizarSucursalAsync(int sucursalId, SolicitudSucursal solicitud, UsuarioAuditoria actor, CancellationToken cancelacion = default);

    /// <summary>Las cajas de una sucursal inactiva no se autentican ante el Central.</summary>
    Task<ResultadoAdministracion> CambiarEstadoSucursalAsync(int sucursalId, bool activa, UsuarioAuditoria actor, CancellationToken cancelacion = default);

    Task<IReadOnlyList<DatosCaja>> ListarCajasAsync(CancellationToken cancelacion = default);

    Task<ResultadoAdministracion> CrearCajaAsync(SolicitudCaja solicitud, UsuarioAuditoria actor, CancellationToken cancelacion = default);

    Task<ResultadoAdministracion> ActualizarCajaAsync(int cajaId, SolicitudActualizarCaja solicitud, UsuarioAuditoria actor, CancellationToken cancelacion = default);

    /// <summary>Una caja deshabilitada no abre turno (RF-169) ni se autentica ante el Central.</summary>
    Task<ResultadoAdministracion> CambiarEstadoCajaAsync(int cajaId, bool habilitada, UsuarioAuditoria actor, CancellationToken cancelacion = default);

    Task<IReadOnlyList<DatosParametro>> ListarParametrosAsync(CancellationToken cancelacion = default);

    Task<ResultadoAdministracion> CrearParametroAsync(SolicitudParametro solicitud, UsuarioAuditoria actor, CancellationToken cancelacion = default);

    Task<ResultadoAdministracion> CambiarValorParametroAsync(int parametroId, string valor, UsuarioAuditoria actor, CancellationToken cancelacion = default);

    /// <summary>
    /// Numeración de los documentos que emite el Central. Cada documento necesita la suya para poder crearse; si falta o
    /// está desactivada, la operación se rechaza diciendo cuál es.
    /// </summary>
    Task<IReadOnlyList<DatosSecuenciaCentral>> ListarSecuenciasAsync(CancellationToken cancelacion = default);

    /// <summary>Crea la numeración de un documento nuevo, o corrige la de uno que ya existe.</summary>
    Task<ResultadoAdministracion> GuardarSecuenciaAsync(SolicitudSecuenciaCentral solicitud, UsuarioAuditoria actor, CancellationToken cancelacion = default);
}
