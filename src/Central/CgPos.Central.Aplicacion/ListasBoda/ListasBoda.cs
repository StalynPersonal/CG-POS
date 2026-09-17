using CgPos.Central.Aplicacion.Abstracciones;
using CgPos.Central.Aplicacion.Seguridad;
using CgPos.Contratos.Central;
using CgPos.Dominio.ListasBoda;

namespace CgPos.Central.Aplicacion.ListasBoda;

/// <summary>
/// Listas de boda y de regalos (RF-73). Se crean en el Central con los datos de los festejados, del evento y los artículos que
/// pidieron; las cajas las consultan por su número al vender y, si el Central lo tiene configurado, lo comprado se descuenta de
/// las cantidades pedidas.
/// </summary>
public interface IServicioListasBoda
{
    /// <summary>La lista tal como la consulta una caja, con lo que falta por comprar de cada artículo.</summary>
    Task<DatosListaBodaParaCaja?> BuscarParaCajaAsync(string numero, CancellationToken cancelacion = default);

    Task<IReadOnlyList<DatosListaBoda>> ListarAsync(string? buscar, EstadoListaBoda? estado, CancellationToken cancelacion = default);

    Task<DatosListaBoda?> ObtenerAsync(int listaBodaId, CancellationToken cancelacion = default);

    Task<ResultadoAdministracion> CrearAsync(SolicitudListaBoda solicitud, UsuarioAuditoria actor, CancellationToken cancelacion = default);

    Task<ResultadoAdministracion> ActualizarAsync(int listaBodaId, SolicitudListaBoda solicitud, UsuarioAuditoria actor, CancellationToken cancelacion = default);

    /// <summary>Cierra la lista (el evento pasó) o la vuelve a abrir.</summary>
    Task<ResultadoAdministracion> CambiarEstadoAsync(int listaBodaId, bool cerrar, UsuarioAuditoria actor, CancellationToken cancelacion = default);
}
