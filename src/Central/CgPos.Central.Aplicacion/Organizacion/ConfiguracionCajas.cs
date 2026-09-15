using CgPos.Central.Aplicacion.Abstracciones;
using CgPos.Central.Aplicacion.Seguridad;
using CgPos.Contratos.Central;

namespace CgPos.Central.Aplicacion.Organizacion;

/// <summary>
/// Lo que el Central asigna a las cajas y baja en su sincronización: rangos de e-CF (RF-28), roles y usuarios de caja. Todo se publica con las
/// validaciones del publicador de maestros, para que ninguna caja reciba algo que no pueda aplicar.
/// </summary>
public interface IServicioConfiguracionCajas
{
    Task<IReadOnlyList<DatosSecuenciaEcfCentral>> ListarSecuenciasAsync(CancellationToken cancelacion = default);

    /// <summary>Asigna un rango nuevo a una caja; no puede solaparse con otro rango del mismo tipo en ninguna caja.</summary>
    Task<ResultadoAdministracion> AsignarSecuenciaAsync(SolicitudSecuenciaEcf solicitud, UsuarioAuditoria actor, CancellationToken cancelacion = default);

    /// <summary>Amplía, prorroga o desactiva un rango: nunca lo reduce.</summary>
    Task<ResultadoAdministracion> ActualizarSecuenciaAsync(Guid secuenciaId, SolicitudActualizarSecuenciaEcf solicitud, UsuarioAuditoria actor, CancellationToken cancelacion = default);

    Task<IReadOnlyList<DatosRolCaja>> ListarRolesCajaAsync(CancellationToken cancelacion = default);

    /// <param name="rolId">Nulo para crear el rol.</param>
    Task<ResultadoAdministracion> GuardarRolCajaAsync(Guid? rolId, SolicitudRolCaja solicitud, UsuarioAuditoria actor, CancellationToken cancelacion = default);

    Task<IReadOnlyList<DatosUsuarioCaja>> ListarUsuariosCajaAsync(CancellationToken cancelacion = default);

    /// <summary>El PIN y el carné se publican solo como hash; sin PIN nuevo se conserva el actual.</summary>
    /// <param name="usuarioId">Nulo para crear el usuario.</param>
    Task<ResultadoAdministracion> GuardarUsuarioCajaAsync(Guid? usuarioId, SolicitudUsuarioCaja solicitud, UsuarioAuditoria actor, CancellationToken cancelacion = default);
}
