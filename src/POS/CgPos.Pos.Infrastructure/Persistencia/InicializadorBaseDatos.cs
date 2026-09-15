using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CgPos.Pos.Infrastructure.Persistencia;

public static class InicializadorBaseDatos
{
    private const int IntentosMaximos = 10;
    private static readonly TimeSpan EsperaEntreIntentos = TimeSpan.FromSeconds(3);

    /// <summary>
    /// Aplica las migraciones pendientes de la base local al arrancar el Agent.
    /// Reintenta si SQL Server aún no está disponible (ej. el servicio de SQL Express arranca después del equipo).
    /// </summary>
    public static async Task InicializarBaseDatosPosAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PosDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(InicializadorBaseDatos).FullName!);

        for (var intento = 1; ; intento++)
        {
            try
            {
                var pendientes = (await db.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
                if (pendientes.Count == 0)
                {
                    logger.LogInformation("Base de datos local al día");
                    return;
                }

                logger.LogInformation("Aplicando {Cantidad} migración(es): {Migraciones}", pendientes.Count, string.Join(", ", pendientes));
                await db.Database.MigrateAsync(cancellationToken);
                logger.LogInformation("Migraciones aplicadas");
                return;
            }
            catch (SqlException ex) when (intento < IntentosMaximos)
            {
                logger.LogWarning(ex, "SQL Server no disponible (intento {Intento}/{Total}); reintentando en {Segundos}s",
                    intento, IntentosMaximos, EsperaEntreIntentos.TotalSeconds);
                await Task.Delay(EsperaEntreIntentos, cancellationToken);
            }
        }
    }
}
