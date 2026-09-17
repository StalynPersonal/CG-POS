using CgPos.Contratos.Central;
using CgPos.Dominio.Entregas;

namespace CgPos.Central.Aplicacion.Entregas;

/// <summary>
/// Pendientes de entrega y envíos de todas las sucursales (RF-249, RF-252). El Central los ve juntos para seguir los atrasos
/// y atender a un cliente que llama o que llega a otra tienda; quien despacha sigue siendo la caja que los emitió.
/// </summary>
public interface IServicioDespachoCentral
{
    public const int TamanoMaximoPagina = 100;

    Task<PaginaPendientesCentral> ListarAsync(string? buscar, EstadoPendiente? estado, MetodoEntrega? metodo, int? sucursalId, bool soloAtrasados,
        bool soloAbiertos, int pagina, int tamano, CancellationToken cancelacion = default);

    Task<DetallePendienteCentral?> ObtenerAsync(int pendienteId, CancellationToken cancelacion = default);

    Task<ResumenDespachoCentral> ResumenAsync(CancellationToken cancelacion = default);
}
