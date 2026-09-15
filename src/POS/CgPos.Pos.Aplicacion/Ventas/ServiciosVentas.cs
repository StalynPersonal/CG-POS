using CgPos.Contratos.Ventas;
using CgPos.Pos.Aplicacion.Seguridad;

namespace CgPos.Pos.Aplicacion.Ventas;

public interface IServicioTurnos
{
    Task<DatosEstadoTurno> ObtenerEstadoAsync(SesionUsuario sesion, CancellationToken cancelacion = default);

    /// <summary>Abre el turno de la caja con el fondo indicado o el sugerido (RF-4, RF-259). Solo uno abierto por caja.</summary>
    Task<RespuestaTurno> AbrirAsync(SesionUsuario sesion, decimal? fondoInicial, CancellationToken cancelacion = default);
}

/// <summary>
/// Operaciones sobre la venta en curso. Cada operación se guarda al instante, así la venta se recupera
/// tras un cierre inesperado (RF-195). Las que requieren permiso aceptan una autorización de supervisor.
/// </summary>
public interface IServicioVentas
{
    /// <summary>Devuelve la venta en curso del usuario en su turno; si no hay, inicia una nueva.</summary>
    Task<RespuestaVenta> ObtenerActualAsync(SesionUsuario sesion, CancellationToken cancelacion = default);

    Task<RespuestaVenta> AgregarArticuloAsync(SesionUsuario sesion, Guid ventaId, string codigo, decimal? cantidad, CancellationToken cancelacion = default);

    Task<RespuestaVenta> CambiarCantidadAsync(SesionUsuario sesion, Guid ventaId, int numeroLinea, decimal cantidad, CancellationToken cancelacion = default);

    Task<RespuestaVenta> EliminarLineaAsync(SesionUsuario sesion, Guid ventaId, int numeroLinea, Guid? autorizacionId, CancellationToken cancelacion = default);

    Task<RespuestaVenta> EliminarPorCodigoAsync(SesionUsuario sesion, Guid ventaId, string codigo, Guid? autorizacionId, CancellationToken cancelacion = default);

    /// <summary>Limpia la pantalla: anula la venta en curso y empieza una nueva (RF-146).</summary>
    Task<RespuestaVenta> LimpiarAsync(SesionUsuario sesion, Guid ventaId, Guid? autorizacionId, CancellationToken cancelacion = default);
}

public sealed record ResultadoPermiso(bool Permitido, bool PorAutorizacion, bool AutorizacionRechazada, Guid? SupervisorId, string? SupervisorNombre, string? Motivo)
{
    public static ResultadoPermiso PermisoPropio { get; } = new(true, false, false, null, null, null);
}

public interface IValidadorAutorizaciones
{
    /// <summary>
    /// Permite la operación si el usuario tiene el permiso, o si trae una autorización de supervisor vigente,
    /// sin usar, del mismo permiso, solicitante y caja. La autorización se marca como usada sin guardar:
    /// se persiste con el mismo SaveChanges de la operación.
    /// </summary>
    Task<ResultadoPermiso> VerificarAsync(SesionUsuario sesion, string permiso, Guid? autorizacionId, string tipoEntidad, string? entidadId,
        CancellationToken cancelacion = default);
}

public interface IEstadoSincronizacion
{
    Task<DatosEstadoSincronizacion> ObtenerAsync(CancellationToken cancelacion = default);
}
