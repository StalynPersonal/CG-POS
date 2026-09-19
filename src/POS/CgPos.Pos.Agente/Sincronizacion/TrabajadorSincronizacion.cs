using CgPos.Pos.Aplicacion.Organizacion;
using CgPos.Dominio.Comun;
﻿using CgPos.Pos.Aplicacion.Sincronizacion;

namespace CgPos.Pos.Agente.Sincronizacion;

/// <summary>
/// Sincronización con el Central dentro del Agente (RF-268, RF-270): cada ciclo baja los maestros cambiados cuando toca (RF-269) y envía la
/// bandeja de salida. Si la red o el Central fallan, la caja sigue operando y el siguiente ciclo reintenta; un error nunca detiene el servicio.
/// </summary>
public sealed class TrabajadorSincronizacion(IServiceScopeFactory ambitos, TimeProvider reloj, ILogger<TrabajadorSincronizacion> registro)
    : BackgroundService
{
    /// <summary>Cada cuánto se vuelve a mirar el reloj. El intervalo real lo dicen los parámetros y se consulta en cada vuelta.</summary>
    private static readonly TimeSpan Latido = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken detener)
    {
        var proximaDescarga = DateTimeOffset.MinValue;
        var proximoCiclo = DateTimeOffset.MinValue;
        using var temporizador = new PeriodicTimer(Latido);

        // Mientras la caja no tenga credencial no hay nada que sincronizar: lo único que corresponde es pedirla y esperar a
        // que la acepten en el Central. El aviso se repite solo cuando cambia, para no llenar el registro.
        string? ultimoAviso = null;

        do
        {
            // El intervalo se configura en el Central: cambiarlo allá se nota aquí sin reiniciar el servicio.
            if (reloj.Ahora() < proximoCiclo)
                continue;

            proximoCiclo = reloj.Ahora() + await RitmoAsync(r => r.IntervaloSincronizacionAsync(detener), TimeSpan.FromSeconds(30), detener);

            var credencial = await CredencialAsync(detener);
            if (!credencial.Lista)
            {
                if (credencial.Mensaje is { Length: > 0 } aviso && aviso != ultimoAviso)
                {
                    registro.LogWarning("Caja sin credencial del Central: {Mensaje}", aviso);
                    ultimoAviso = aviso;
                }

                continue;
            }

            if (ultimoAviso is not null)
            {
                registro.LogInformation("La caja ya tiene su credencial del Central: comienza a sincronizar.");
                ultimoAviso = null;
            }

            // Primero bajan los maestros: una caja nueva se aprovisiona en el primer ciclo (RF-281).
            if (reloj.Ahora() >= proximaDescarga)
            {
                proximaDescarga = reloj.Ahora() + await RitmoAsync(r => r.IntervaloMaestrosAsync(detener), TimeSpan.FromMinutes(5), detener);
                await EjecutarAsync<IDescargaMaestros>(async descarga =>
                {
                    var resultado = await descarga.DescargarAsync(detener);
                    if (resultado.Error is not null)
                        registro.LogWarning("Descarga de maestros sin aplicar: {Error}", resultado.Error);
                }, "La descarga de maestros del Central falló", detener);
            }

            await EjecutarAsync<IProcesadorBandejaSalida>(async procesador =>
            {
                var resultado = await procesador.ProcesarAsync(detener);
                if (resultado.Tomados > 0)
                    registro.LogInformation("Sincronización: {Confirmados} confirmados y {Fallidos} con error de {Tomados} mensajes.",
                        resultado.Confirmados, resultado.Fallidos, resultado.Tomados);
            }, "La sincronización con el Central falló", detener);
        }
        while (await temporizador.WaitForNextTickAsync(detener));
    }

    /// <summary>Consulta un ritmo configurado. Si la base no responde, se usa el de siempre y el ciclo no se detiene.</summary>
    private async Task<TimeSpan> RitmoAsync(Func<IRitmosOperacion, Task<TimeSpan>> cual, TimeSpan siNoSePuede, CancellationToken detener)
    {
        try
        {
            await using var ambito = ambitos.CreateAsyncScope();
            return await cual(ambito.ServiceProvider.GetRequiredService<IRitmosOperacion>());
        }
        catch (Exception excepcion) when (excepcion is not OperationCanceledException || !detener.IsCancellationRequested)
        {
            registro.LogWarning(excepcion, "No se pudieron leer los intervalos de sincronización; se usa {Predeterminado}.", siNoSePuede);
            return siNoSePuede;
        }
    }

    /// <summary>Pide la credencial si falta. Un fallo aquí no detiene el servicio: el próximo ciclo vuelve a intentarlo.</summary>
    private async Task<EstadoCredencialCaja> CredencialAsync(CancellationToken detener)
    {
        try
        {
            await using var ambito = ambitos.CreateAsyncScope();
            return await ambito.ServiceProvider.GetRequiredService<IClienteCentral>().AsegurarCredencialAsync(detener);
        }
        catch (Exception excepcion) when (excepcion is not OperationCanceledException || !detener.IsCancellationRequested)
        {
            return new EstadoCredencialCaja(false, excepcion.Message);
        }
    }

    private async Task EjecutarAsync<TServicio>(Func<TServicio, Task> accion, string mensajeError, CancellationToken detener) where TServicio : notnull
    {
        try
        {
            await using var ambito = ambitos.CreateAsyncScope();
            await accion(ambito.ServiceProvider.GetRequiredService<TServicio>());
        }
        catch (Exception excepcion) when (excepcion is not OperationCanceledException || !detener.IsCancellationRequested)
        {
            registro.LogWarning(excepcion, "{Mensaje}; se reintenta en el próximo ciclo.", mensajeError);
        }
    }
}
