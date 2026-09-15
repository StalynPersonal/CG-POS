using CgPos.Pos.Aplicacion.Sincronizacion;

namespace CgPos.Pos.Agente.Sincronizacion;

/// <summary>
/// Mantenimiento periódico dentro del Agente: verifica la hora con el servidor NTP, toma el respaldo diario de la base cuando corresponde y
/// purga lo que el Central ya confirmó. Un fallo se registra y se reintenta en el próximo ciclo.
/// </summary>
public sealed class TrabajadorMantenimiento(IServiceScopeFactory ambitos, IConfiguration configuracion, TimeProvider reloj, ILogger<TrabajadorMantenimiento> registro)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken detener)
    {
        var intervalo = TimeSpan.FromMinutes(Math.Max(1, configuracion.GetValue(ClavesMantenimiento.IntervaloMinutos, 60)));
        using var temporizador = new PeriodicTimer(intervalo);

        do
        {
            try
            {
                await using var ambito = ambitos.CreateAsyncScope();
                var mantenimiento = ambito.ServiceProvider.GetRequiredService<IServicioMantenimiento>();

                var hora = await mantenimiento.VerificarHoraAsync(detener);
                if (!hora.Verificada && hora.Servidor is not null)
                    registro.LogInformation("Hora no verificada: {Error}", hora.Error);

                if (mantenimiento.CorrespondeRespaldo(reloj.GetLocalNow()))
                    await mantenimiento.RespaldarAsync(detener);

                await mantenimiento.PurgarAsync(detener);
            }
            catch (Exception excepcion) when (excepcion is not OperationCanceledException)
            {
                registro.LogWarning(excepcion, "El mantenimiento de la caja falló; se reintenta en el próximo ciclo.");
            }
        }
        while (await temporizador.WaitForNextTickAsync(detener));
    }
}
