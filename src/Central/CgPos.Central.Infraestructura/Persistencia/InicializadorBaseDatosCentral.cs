using CgPos.Dominio.Comun;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CgPos.Central.Infraestructura.Persistencia;

public static class InicializadorBaseDatosCentral
{
    private const int IntentosMaximos = 10;
    private static readonly TimeSpan EsperaEntreIntentos = TimeSpan.FromSeconds(3);

    /// <summary>
    /// Comprueba que la base del Central exista y tenga sus tablas; reintenta mientras SQL Server no esté disponible.
    /// El Central no crea ni cambia la base: eso lo hace el script <c>scripts/base-datos/estructura_base_datos_central.sql</c>.
    /// </summary>
    /// <exception cref="BaseDatosNoPreparadaExcepcion">La base no existe o le faltan tablas.</exception>
    public static async Task InicializarBaseDatosCentralAsync(this IServiceProvider servicios, CancellationToken cancelacion = default)
    {
        await using var ambito = servicios.CreateAsyncScope();
        var contexto = ambito.ServiceProvider.GetRequiredService<ContextoDatosCentral>();
        var logger = ambito.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(InicializadorBaseDatosCentral).FullName!);

        for (var intento = 1; ; intento++)
        {
            try
            {
                if (!await contexto.Database.CanConnectAsync(cancelacion))
                    throw new BaseDatosNoPreparadaExcepcion(
                        "La base de datos del Central no existe. Créela con scripts/base-datos/estructura_base_datos_central.sql.");

                await contexto.UsuariosCentral.AnyAsync(cancelacion);
                logger.LogInformation("Base de datos del Central lista");
                return;
            }
            catch (SqlException excepcion) when (excepcion.Number == 208)
            {
                throw new BaseDatosNoPreparadaExcepcion(
                    "A la base de datos del Central le faltan tablas. Ejecute scripts/base-datos/estructura_base_datos_central.sql.", excepcion);
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
