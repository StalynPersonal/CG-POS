using CgPos.Central.Aplicacion.Abstracciones;
using CgPos.Contratos.Central;

namespace CgPos.Central.Aplicacion.Seguridad;

public sealed record ResultadoAdministracion(bool Exitosa, string? Mensaje, Guid? Id, bool NoEncontrado = false)
{
    public static ResultadoAdministracion Correcto(Guid? id = null) => new(true, null, id);

    public static ResultadoAdministracion Error(string mensaje) => new(false, mensaje, null);

    public static ResultadoAdministracion Inexistente(string mensaje) => new(false, mensaje, null, NoEncontrado: true);
}

/// <summary>
/// Usuarios y roles del Central Manager. Toda operación queda en auditoría con quien la hizo, y ninguna puede dejar al Central sin un usuario activo
/// que administre la seguridad.
/// </summary>
public interface IServicioAdministracionSeguridad
{
    Task<IReadOnlyList<DatosRolCentral>> ListarRolesAsync(CancellationToken cancelacion = default);

    Task<ResultadoAdministracion> CrearRolAsync(SolicitudRolCentral solicitud, UsuarioAuditoria actor, CancellationToken cancelacion = default);

    Task<ResultadoAdministracion> ActualizarRolAsync(Guid rolId, SolicitudRolCentral solicitud, UsuarioAuditoria actor, CancellationToken cancelacion = default);

    Task<ResultadoAdministracion> CambiarEstadoRolAsync(Guid rolId, bool activo, UsuarioAuditoria actor, CancellationToken cancelacion = default);

    Task<IReadOnlyList<DatosUsuarioCentral>> ListarUsuariosAsync(CancellationToken cancelacion = default);

    /// <summary>Crea el usuario con una contraseña temporal que cumple la política configurada; debe cambiarla al primer ingreso.</summary>
    Task<ResultadoAdministracion> CrearUsuarioAsync(SolicitudUsuarioCentral solicitud, UsuarioAuditoria actor, CancellationToken cancelacion = default);

    /// <summary>Un cambio de rol cierra las sesiones del usuario para que ingrese con sus permisos nuevos.</summary>
    Task<ResultadoAdministracion> ActualizarUsuarioAsync(Guid usuarioId, SolicitudActualizarUsuarioCentral solicitud, UsuarioAuditoria actor, CancellationToken cancelacion = default);

    /// <summary>Asigna una contraseña temporal, desbloquea al usuario y cierra sus sesiones.</summary>
    Task<ResultadoAdministracion> RestablecerContrasenaAsync(Guid usuarioId, string contrasenaTemporal, UsuarioAuditoria actor, CancellationToken cancelacion = default);

    Task<ResultadoAdministracion> DesbloquearAsync(Guid usuarioId, UsuarioAuditoria actor, CancellationToken cancelacion = default);

    /// <summary>Desactivar cierra las sesiones del usuario; nadie puede desactivarse a sí mismo.</summary>
    Task<ResultadoAdministracion> CambiarEstadoUsuarioAsync(Guid usuarioId, bool activo, UsuarioAuditoria actor, CancellationToken cancelacion = default);
}
