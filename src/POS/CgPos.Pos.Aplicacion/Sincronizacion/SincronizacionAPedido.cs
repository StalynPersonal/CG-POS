using CgPos.Contratos.Sincronizacion;

namespace CgPos.Pos.Aplicacion.Sincronizacion;

/// <summary>
/// Sincronizar ahora, porque alguien lo pidió desde la caja. El servicio de fondo ya lo hace cada cierto tiempo; esto es
/// para cuando en la tienda acaban de cambiar un precio en el Central y lo quieren ver enseguida, sin esperar el ciclo.
/// </summary>
/// <remarks>
/// Comparte el candado con el ciclo automático: dos sincronizaciones a la vez se pisarían y el progreso saltaría de una a
/// otra. Si ya hay una corriendo, esta espera a que termine en vez de duplicarla.
/// </remarks>
public interface ISincronizacionAPedido
{
    Task<ResultadoSincronizacion> EjecutarAsync(CancellationToken cancelacion = default);

    /// <summary>Corre un ciclo del servicio de fondo bajo el mismo candado, para que no coincida con uno a pedido.</summary>
    Task<T> EnTurnoAsync<T>(Func<CancellationToken, Task<T>> ciclo, CancellationToken cancelacion = default);
}
