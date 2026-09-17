using CgPos.Contratos.Central;
using CgPos.Dominio.Reportes;

namespace CgPos.Central.Aplicacion.Ventas;

/// <param name="Desde">Día de operación desde el que se buscan los comprobantes.</param>
/// <param name="Buscar">Número, e-NCF, documento o nombre del cliente.</param>
public sealed record FiltroComprobantesRecibidos(
    DateOnly Desde,
    DateOnly Hasta,
    int? SucursalId = null,
    int? CajaId = null,
    TipoComprobanteVenta? Tipo = null,
    string? Buscar = null,
    int Pagina = 0,
    int Tamano = 25);

/// <summary>
/// Facturas y notas de crédito que las cajas subieron al Central (M16): se listan con sus filtros y se abre el detalle de cada una
/// con sus líneas, impuestos y formas de pago, tal como la cobró la caja.
/// </summary>
public interface IServicioComprobantesRecibidos
{
    public const int TamanoMaximoPagina = 200;

    Task<PaginaComprobantesRecibidos> ListarAsync(FiltroComprobantesRecibidos filtro, CancellationToken cancelacion = default);

    Task<DatosComprobanteRecibidoDetalle?> ObtenerAsync(int comprobanteId, CancellationToken cancelacion = default);
}
