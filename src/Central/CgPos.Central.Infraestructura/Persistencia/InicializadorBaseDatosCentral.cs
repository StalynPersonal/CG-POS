using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CgPos.Central.Infraestructura.Persistencia;

public static class InicializadorBaseDatosCentral
{
    private const int IntentosMaximos = 10;
    private static readonly TimeSpan EsperaEntreIntentos = TimeSpan.FromSeconds(3);

    /// <summary>Aplica las migraciones pendientes al arrancar; reintenta mientras SQL Server no esté disponible.</summary>
    public static async Task InicializarBaseDatosCentralAsync(this IServiceProvider servicios, CancellationToken cancelacion = default)
    {
        await using var ambito = servicios.CreateAsyncScope();
        var contexto = ambito.ServiceProvider.GetRequiredService<ContextoDatosCentral>();
        var logger = ambito.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(InicializadorBaseDatosCentral).FullName!);

        for (var intento = 1; ; intento++)
        {
            try
            {
                var pendientes = (await contexto.Database.GetPendingMigrationsAsync(cancelacion)).ToList();
                if (pendientes.Count == 0)
                {
                    logger.LogInformation("Base de datos del Central al día");
                }
                else
                {
                    logger.LogInformation("Aplicando {Cantidad} migración(es): {Migraciones}", pendientes.Count, string.Join(", ", pendientes));
                    await contexto.Database.MigrateAsync(cancelacion);
                    logger.LogInformation("Migraciones aplicadas");
                }

                // Los maestros que aún estén en la tabla JSON pasan a su tabla.
                await Maestros.MigracionMaestrosATablas.EjecutarAsync(contexto, logger, cancelacion);
                return;
            }
            catch (SqlException excepcion) when (intento < IntentosMaximos)
            {
                logger.LogWarning(excepcion, "SQL Server no disponible (intento {Intento}/{Total}); reintentando en {Segundos}s",
                    intento, IntentosMaximos, EsperaEntreIntentos.TotalSeconds);
                await Task.Delay(EsperaEntreIntentos, cancelacion);
            }
        }
    }
}
