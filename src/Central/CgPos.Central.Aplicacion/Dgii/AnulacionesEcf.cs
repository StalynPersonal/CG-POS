using CgPos.Central.Aplicacion.Abstracciones;
using CgPos.Central.Aplicacion.Seguridad;
using CgPos.Contratos.Central;

namespace CgPos.Central.Aplicacion.Dgii;

/// <summary>
/// Anulación de e-NCF no utilizados (ANECF): los que quedaron sin usar en un rango que la caja ya no usará (desactivado o vencido), por ejemplo por
/// una caja dañada o reemplazada. Se informa a la DGII para que esos números no queden como faltantes.
/// </summary>
public interface IServicioAnulacionesEcf
{
    Task<IReadOnlyList<DatosAnulacionEcf>> ListarAsync(CancellationToken cancelacion = default);

    /// <summary>
    /// Solo se anula dentro de un rango desactivado o vencido (la caja ya no lo usa), sin e-CF recibidos en el tramo y sin solapar otra anulación
    /// aceptada. La respuesta de la DGII queda registrada, sea aceptada, rechazada o fallida.
    /// </summary>
    Task<ResultadoAdministracion> AnularAsync(int secuenciaId, SolicitudAnulacionEcf solicitud, UsuarioAuditoria actor, CancellationToken cancelacion = default);
}
