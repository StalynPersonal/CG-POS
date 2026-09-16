using CgPos.Central.Aplicacion.Dgii;
using CgPos.Central.Aplicacion.Organizacion;
using CgPos.Dominio.Organizacion;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CgPos.Central.Infraestructura.Dgii;

/// <summary>Ejecuta el despacho a la DGII cada <see cref="ClavesParametrosCentral.DgiiSegundosCiclo"/> segundos mientras esté activado.</summary>
internal sealed class TrabajadorDgii(IServiceScopeFactory fabricaAmbitos, TimeProvider reloj, ILogger<TrabajadorDgii> registro) : BackgroundService
{
    /// <summary>Solo decide cada cuánto se vuelve a mirar la configuración si el envío está desactivado o sin configurar; no es una regla de negocio.</summary>
    private static readonly TimeSpan EsperaSinConfiguracion = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken detener)
    {
        while (!detener.IsCancellationRequested)
        {
            var espera = EsperaSinConfiguracion;
            try
            {
                await using var ambito = fabricaAmbitos.CreateAsyncScope();
                var resultado = await ambito.ServiceProvider.GetRequiredService<IDespachadorDgii>().ProcesarAsync(detener);
                if (resultado.Habilitado)
                {
                    espera = TimeSpan.FromSeconds(await ambito.ServiceProvider.GetRequiredService<IParametrosCentral>()
                        .ObtenerEnteroPositivoAsync(ClavesParametrosCentral.DgiiSegundosCiclo, detener));
                    if (resultado.Enviados + resultado.Aceptados + resultado.Rechazados + resultado.Fallidos > 0)
                        registro.LogInformation("DGII: {Enviados} enviados, {Aceptados} aceptados, {Rechazados} rechazados, {Fallidos} con fallo, {EnProceso} en proceso",
                            resultado.Enviados, resultado.Aceptados, resultado.Rechazados, resultado.Fallidos, resultado.EnProceso);
                }
            }
            catch (OperationCanceledException) when (detener.IsCancellationRequested)
            {
                return;
            }
            catch (ParametroNoConfiguradoExcepcion excepcion)
            {
                registro.LogWarning("El envío a la DGII está detenido por configuración: {Mensaje}", excepcion.Message);
            }
            catch (Exception excepcion)
            {
                registro.LogError(excepcion, "Falló el ciclo de envío a la DGII");
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
