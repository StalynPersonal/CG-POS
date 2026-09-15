using CgPos.Pos.Aplicacion.Sincronizacion;

namespace CgPos.Pos.Agente.Sincronizacion;

/// <summary>
/// Sincronización con el Central dentro del Agente (RF-268, RF-270): en cada ciclo envía la bandeja de salida. Si la red o el Central fallan,
/// la caja sigue operando y el siguiente ciclo reintenta; un error nunca detiene el servicio.
/// </summary>
public sealed class TrabajadorSincronizacion(IServiceScopeFactory ambitos, IConfiguration configuracion, ILogger<TrabajadorSincronizacion> registro)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken detener)
    {
        var intervalo = TimeSpan.FromSeconds(Math.Max(1, configuracion.GetValue(ClavesSincronizacion.IntervaloSegundos, 30)));
        using var temporizador = new PeriodicTimer(intervalo);

        do
        {
            try
            {
                await using var ambito = ambitos.CreateAsyncScope();
                var resultado = await ambito.ServiceProvider.GetRequiredService<IProcesadorBandejaSalida>().ProcesarAsync(detener);
                if (resultado.Tomados > 0)
                    registro.LogInformation("Sincronización: {Confirmados} confirmados y {Fallidos} con error de {Tomados} mensajes.",
                        resultado.Confirmados, resultado.Fallidos, resultado.Tomados);
            }
            catch (Exception excepcion) when (excepcion is not OperationCanceledException)
            {
                registro.LogWarning(excepcion, "La sincronización con el Central falló; se reintenta en el próximo ciclo.");
            }
        }
        while (await temporizador.WaitForNextTickAsync(detener));
    }
}
