using CgPos.Central.Aplicacion.Fidelidad;
using CgPos.Central.Aplicacion.Organizacion;
using CgPos.Dominio.Organizacion;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CgPos.Central.Infraestructura.Fidelidad;

/// <summary>
/// Vence los puntos que cumplieron su plazo y publica el saldo nuevo para las cajas (RF-242). Sin movimientos el saldo no cambiaría solo,
/// y el cliente vería en la caja puntos que ya no valen.
/// </summary>
internal sealed class TrabajadorVencimientoPuntos(IServiceScopeFactory fabricaAmbitos, TimeProvider reloj, ILogger<TrabajadorVencimientoPuntos> registro)
    : BackgroundService
{
    /// <summary>Solo decide cada cuánto se vuelve a mirar la configuración si falta el parámetro; no es una regla de negocio.</summary>
    private static readonly TimeSpan EsperaSinConfiguracion = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken detener)
    {
        while (!detener.IsCancellationRequested)
        {
            var espera = EsperaSinConfiguracion;
            try
            {
                await using var ambito = fabricaAmbitos.CreateAsyncScope();
                var parametros = ambito.ServiceProvider.GetRequiredService<IParametrosCentral>();
                var lote = await parametros.ObtenerEnteroPositivoAsync(ClavesParametrosCentral.FidelidadLoteVencimiento, detener);
                var recalculados = await ambito.ServiceProvider.GetRequiredService<IServicioFidelidadCentral>().VencerPuntosAsync(lote, detener);
                espera = TimeSpan.FromMinutes(await parametros.ObtenerEnteroPositivoAsync(ClavesParametrosCentral.FidelidadMinutosCicloVencimiento, detener));

                if (recalculados > 0)
                    registro.LogInformation("Fidelidad: {Recalculados} miembros con puntos vencidos y saldo publicado", recalculados);
            }
            catch (OperationCanceledException) when (detener.IsCancellationRequested)
            {
                return;
            }
            catch (ParametroNoConfiguradoExcepcion excepcion)
            {
                registro.LogWarning("El vencimiento de puntos está detenido por configuración: {Mensaje}", excepcion.Message);
            }
            catch (Exception excepcion)
            {
                registro.LogError(excepcion, "Falló el ciclo de vencimiento de puntos");
            }

            try
            {
                await Task.Delay(espera, reloj, detener);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }
}
