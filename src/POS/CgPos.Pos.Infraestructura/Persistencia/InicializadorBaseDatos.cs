using CgPos.Dominio.Comun;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CgPos.Pos.Infraestructura.Persistencia;

public static class InicializadorBaseDatos
{
    private const int IntentosMaximos = 10;
    private static readonly TimeSpan EsperaEntreIntentos = TimeSpan.FromSeconds(3);

    /// <summary>
    /// Comprueba que la base local exista y tenga sus tablas. El Agente no crea ni cambia la base: eso lo hace el script
    /// <c>scripts/base-datos/estructura_base_datos_pos.sql</c>, que se ejecuta al instalar la caja.
    /// Reintenta si SQL Server aún no está disponible (ej. el servicio de SQL Express arranca después del equipo).
    /// </summary>
    /// <exception cref="BaseDatosNoPreparadaExcepcion">La base no existe o le faltan tablas.</exception>
    public static async Task InicializarBaseDatosPosAsync(this IServiceProvider servicios, CancellationToken cancelacion = default)
    {
        await using var ambito = servicios.CreateAsyncScope();
        var contexto = ambito.ServiceProvider.GetRequiredService<ContextoDatosPos>();
        var logger = ambito.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(InicializadorBaseDatos).FullName!);

        for (var intento = 1; ; intento++)
        {
            try
            {
                if (!await contexto.Database.CanConnectAsync(cancelacion))
                    throw new BaseDatosNoPreparadaExcepcion(
                        "La base de datos de la caja no existe. Créela con scripts/base-datos/estructura_base_datos_pos.sql.");

                await contexto.Turnos.AnyAsync(cancelacion);
                logger.LogInformation("Base de datos local lista");
                return;
            }
            catch (SqlException excepcion) when (EsFaltanTablas(excepcion))
            {
                throw new BaseDatosNoPreparadaExcepcion(
                    "A la base de datos de la caja le faltan tablas. Ejecute scripts/base-datos/estructura_base_datos_pos.sql.", excepcion);
            }
            catch (SqlException excepcion) when (intento < IntentosMaximos)
            {
                logger.LogWarning(excepcion, "SQL Server no disponible (intento {Intento}/{Total}); reintentando en {Segundos}s",
                    intento, IntentosMaximos, EsperaEntreIntentos.TotalSeconds);
                await Task.Delay(EsperaEntreIntentos, cancelacion);
            }
        }
    }

    /// <summary>208 es «nombre de objeto no válido»: la base existe pero no tiene el esquema.</summary>
    internal static bool EsFaltanTablas(SqlException excepcion) => excepcion.Number == 208;
}
