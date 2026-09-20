using CgPos.Central.Aplicacion.Abstracciones;
using CgPos.Central.Aplicacion.Seguridad;
using CgPos.Contratos.Central;
using CgPos.Contratos.Ventas;
using CgPos.Dominio.Entregas;

namespace CgPos.Central.Aplicacion.Entregas;

/// <summary>
/// Pendientes de entrega y envíos de todas las sucursales (RF-249 a RF-256). El despacho vive en el Central y no en la caja:
/// no emite comprobante fiscal, no toca la gaveta ni el turno, y quien atiende a un cliente que llama o que llega a otra
/// tienda necesita verlos todos. La caja solo los crea al cobrar; de ahí en adelante los opera el Central.
/// </summary>
public interface IServicioDespachoCentral
{
    public const int TamanoMaximoPagina = 100;

    Task<PaginaPendientesCentral> ListarAsync(string? buscar, EstadoPendiente? estado, MetodoEntrega? metodo, int? sucursalId, bool soloAtrasados,
        bool soloAbiertos, int pagina, int tamano, CancellationToken cancelacion = default);

    Task<DetallePendienteCentral?> ObtenerAsync(int pendienteId, CancellationToken cancelacion = default);

    Task<ResumenDespachoCentral> ResumenAsync(CancellationToken cancelacion = default);

    /// <summary>Avanza la preparación: en preparación, preparado y, para envíos, despachado con el transportista (RF-252).</summary>
    Task<ResultadoAdministracion> CambiarEstadoAsync(int pendienteId, SolicitudEstadoPendiente solicitud, UsuarioAuditoria actor,
        CancellationToken cancelacion = default);

    /// <summary>Registra la entrega total o parcial con quien recibe y el serial de los serializados (RF-253, RF-254).</summary>
    Task<ResultadoAdministracion> EntregarAsync(int pendienteId, SolicitudEntregaPendiente solicitud, UsuarioAuditoria actor,
        CancellationToken cancelacion = default);

    /// <summary>Anula un pendiente sin entregas con motivo, liberando la mercancía (RF-255).</summary>
    Task<ResultadoAdministracion> AnularAsync(int pendienteId, SolicitudAnularPendiente solicitud, UsuarioAuditoria actor,
        CancellationToken cancelacion = default);

    /// <summary>
    /// Constancia de entrega en carta para que la firme quien recibe (RF-254). El almacén imprime en una impresora normal, no en
    /// la de tickets de la caja. Nula si el pendiente o esa entrega no existen.
    /// </summary>
    Task<byte[]?> ConstanciaAsync(int pendienteId, int numeroEntrega, CancellationToken cancelacion = default);
}
