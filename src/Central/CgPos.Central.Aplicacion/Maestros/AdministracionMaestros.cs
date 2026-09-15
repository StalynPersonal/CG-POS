using CgPos.Central.Aplicacion.Abstracciones;
using CgPos.Central.Aplicacion.Seguridad;
using CgPos.Contratos.Catalogo;
using CgPos.Contratos.Central;
using CgPos.Dominio.Sincronizacion;

namespace CgPos.Central.Aplicacion.Maestros;

/// <summary>
/// Administración de los maestros que el Central publica para las cajas (RN-24). Cada cambio pasa por el publicador de maestros, con las reglas
/// del dominio de la caja y las referencias ya publicadas, y baja en la próxima sincronización.
/// </summary>
public interface IServicioMaestrosCentral
{
    /// <typeparam name="T">Registro de carga del tipo de maestro.</typeparam>
    Task<IReadOnlyList<DatosMaestroCentral<T>>> ListarAsync<T>(TipoMaestro tipo, CancellationToken cancelacion = default);

    /// <summary>Publica un paquete con un solo registro nuevo o cambiado.</summary>
    /// <param name="id">Id del registro publicado, para la respuesta.</param>
    Task<ResultadoAdministracion> PublicarAsync(PaqueteMaestros paquete, Guid id, UsuarioAuditoria actor, CancellationToken cancelacion = default);
}
