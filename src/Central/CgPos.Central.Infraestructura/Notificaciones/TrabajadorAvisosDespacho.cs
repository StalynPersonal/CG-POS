using CgPos.Central.Aplicacion.Notificaciones;
using CgPos.Central.Aplicacion.Organizacion;
using CgPos.Dominio.Organizacion;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CgPos.Central.Infraestructura.Notificaciones;

/// <summary>Avisa al cliente que su pedido está preparado (RF-256). Si el correo falla, el pedido sigue igual y se reintenta.</summary>
internal sealed class TrabajadorAvisosDespacho(IServiceScopeFactory fabricaAmbitos, TimeProvider reloj, ILogger<TrabajadorAvisosDespacho> registro)
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
                var lote = await parametros.ObtenerEnteroPositivoAsync(ClavesParametrosCentral.DespachoLoteAvisos, detener);
                var enviados = await ambito.ServiceProvider.GetRequiredService<IAvisosDespacho>().AvisarPreparadosAsync(lote, detener);
                espera = TimeSpan.FromMinutes(await parametros.ObtenerEnteroPositivoAsync(ClavesParametrosCentral.DespachoMinutosCicloAvisos, detener));

                if (enviados > 0)
                    registro.LogInformation("Despacho: {Enviados} clientes avisados de que su pedido está listo", enviados);
            }
            catch (OperationCanceledException) when (detener.IsCancellationRequested)
            {
                return;
            }
            catch (ParametroNoConfiguradoExcepcion excepcion)
            {
                registro.LogWarning("El aviso al cliente está detenido por configuración: {Mensaje}", excepcion.Message);
            }
            catch (Exception excepcion)
            {
                registro.LogError(excepcion, "Falló el ciclo de avisos de despacho");
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
