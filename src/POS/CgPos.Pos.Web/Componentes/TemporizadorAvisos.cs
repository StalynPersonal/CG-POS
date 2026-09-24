using CgPos.Contratos.Seguridad;

namespace CgPos.Pos.Web.Componentes;

/// <summary>
/// Quita solo el aviso que está en pantalla, pasados los segundos que fija el Central. En la caja nadie va cerrando
/// avisos con el ratón: si no se van, se acumulan encima de la venta y el cajero deja de leerlos.
/// </summary>
/// <remarks>
/// Se programa mirando lo que hay en pantalla, no en cada sitio que pone un aviso: son veinte sitios distintos y bastaba
/// olvidarse de uno para que ese aviso se quedara clavado.
/// </remarks>
public sealed class TemporizadorAvisos : IDisposable
{
    private Timer? _temporizador;
    private string? _programado;

    /// <summary>Cuánto dura un aviso. Con 0 se queda hasta que el cajero lo cierre.</summary>
    public int Segundos { get; set; } = AvisosPantalla.SegundosPredeterminados;

    /// <summary>Deja programado quitar <paramref name="enPantalla"/>; con nulo no hay nada que quitar.</summary>
    /// <param name="alVencer">Quita el aviso que se le pasa, si es el que sigue en pantalla.</param>
    public void Programar(string? enPantalla, Func<string, Task> alVencer)
    {
        ArgumentNullException.ThrowIfNull(alVencer);

        if (enPantalla is null)
        {
            _programado = null;
            return;
        }

        // El mismo aviso no se reprograma en cada dibujado: si no, con la pantalla trabajando nunca se cumpliría el plazo.
        if (Segundos <= 0 || enPantalla == _programado)
            return;

        _programado = enPantalla;
        _temporizador?.Dispose();
        _temporizador = new Timer(_ =>
        {
            // Si mientras tanto salió otro aviso, ese tiene su propio plazo y este ya no manda.
            if (_programado is not { } vencido)
                return;

            _programado = null;
            _ = alVencer(vencido);
        }, null, TimeSpan.FromSeconds(Segundos), Timeout.InfiniteTimeSpan);
    }

    public void Dispose() => _temporizador?.Dispose();
}
