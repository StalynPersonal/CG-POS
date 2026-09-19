using CgPos.Pos.Aplicacion.Sincronizacion;
using CgPos.Dominio.Comun;

namespace CgPos.Pos.Agente.Sincronizacion;

/// <summary>
/// Mantenimiento periódico dentro del Agente: verifica la hora con el servidor NTP, toma el respaldo diario de la base cuando corresponde y
/// purga lo que el Central ya confirmó. Un fallo se registra y se reintenta en el próximo ciclo.
/// </summary>
public sealed class TrabajadorMantenimiento(IServiceScopeFactory ambitos, TimeProvider reloj, ILogger<TrabajadorMantenimiento> registro)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken detener)
    {
        // El ciclo se mide corto y el intervalo real se consulta en cada vuelta: así, cambiarlo en el Central se nota
        // enseguida en las cajas, sin reiniciar el servicio.
        using var temporizador = new PeriodicTimer(TimeSpan.FromMinutes(1));
        var proximo = DateTimeOffset.MinValue;

        do
        {
            try
            {
                await using var ambito = ambitos.CreateAsyncScope();
                var mantenimiento = ambito.ServiceProvider.GetRequiredService<IServicioMantenimiento>();
                var ritmos = ambito.ServiceProvider.GetRequiredService<IRitmosOperacion>();

                var ahora = reloj.Ahora();
                if (ahora < proximo)
                    continue;

                proximo = ahora + await ritmos.IntervaloMantenimientoAsync(detener);

                var hora = await mantenimiento.VerificarHoraAsync(detener);
                if (!hora.Verificada && hora.Servidor is not null)
                    registro.LogInformation("Hora no verificada: {Error}", hora.Error);

                if (await mantenimiento.CorrespondeRespaldoAsync(reloj.Ahora(), detener))
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
