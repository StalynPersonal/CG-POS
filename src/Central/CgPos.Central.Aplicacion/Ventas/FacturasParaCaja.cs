using CgPos.Contratos.Central;

namespace CgPos.Central.Aplicacion.Ventas;

/// <summary>
/// Facturas que el Central le entrega a una caja para devolverlas. Existe porque una devolución puede ser de cualquier
/// tienda: la caja solo tiene sus propias facturas, y el único que sabe cuánto se devolvió ya de cada línea —aquí o en otra
/// sucursal— es el Central.
/// </summary>
public interface IServicioFacturasParaCaja
{
    /// <summary>Tope de resultados de una búsqueda: la caja elige de una lista corta, nunca se le baja el histórico.</summary>
    public const int MaximoResultados = 50;

    /// <summary>La factura por su número o su e-NCF, con lo ya devuelto de cada línea; nula si no existe.</summary>
    /// <param name="cajaId">Caja que pregunta, para decirle si la factura es suya.</param>
    Task<DatosFacturaParaCaja?> BuscarAsync(string numeroOEncf, int cajaId, CancellationToken cancelacion = default);

    /// <summary>
    /// Facturas que coinciden con lo buscado (número, e-NCF o documento del cliente) dentro del rango de días, para cuando
    /// el cliente llega sin el ticket.
    /// </summary>
    Task<IReadOnlyList<ResumenFacturaParaCaja>> ListarAsync(string? buscar, DateOnly desde, DateOnly hasta, CancellationToken cancelacion = default);

    /// <summary>
    /// Retiene esas líneas para la caja mientras emite la nota de crédito. Pedirla otra vez para la misma factura y caja reemplaza
    /// la anterior, así un reintento no bloquea el doble.
    /// </summary>
    Task<RespuestaReservaFactura> ReservarAsync(string facturaNumero, int cajaId, IReadOnlyDictionary<int, decimal> lineas,
        CancellationToken cancelacion = default);

    /// <summary>Suelta lo que esa caja tenía retenido de la factura. Falso si no tenía nada.</summary>
    Task<bool> LiberarReservaAsync(string facturaNumero, int cajaId, CancellationToken cancelacion = default);
}
