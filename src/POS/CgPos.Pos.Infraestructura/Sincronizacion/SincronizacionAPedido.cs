using CgPos.Contratos.Sincronizacion;
using CgPos.Pos.Aplicacion.Sincronizacion;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CgPos.Pos.Infraestructura.Sincronizacion;

/// <summary>
/// Ejecuta una sincronización completa cuando alguien la pide desde la caja: primero baja los maestros y después sube lo
/// que haya en la bandeja. Es el mismo trabajo del ciclo automático, con el mismo candado, para que no coincidan.
/// </summary>
internal sealed class SincronizacionAPedido(IServiceScopeFactory ambitos, ILogger<SincronizacionAPedido> registro) : ISincronizacionAPedido
{
    private readonly SemaphoreSlim _turno = new(1, 1);

    public async Task<ResultadoSincronizacion> EjecutarAsync(CancellationToken cancelacion = default)
    {
        // Si el ciclo automático está corriendo, se espera a que termine: lanzar otra en paralelo no adelanta nada y deja
        // el progreso saltando entre las dos.
        var yaEnCurso = _turno.CurrentCount == 0;
        await _turno.WaitAsync(cancelacion);
        try
        {
            await using var ambito = ambitos.CreateAsyncScope();
            var descarga = await ambito.ServiceProvider.GetRequiredService<IDescargaMaestros>().DescargarAsync(cancelacion);
            if (descarga.Error is not null)
                return new ResultadoSincronizacion(false, yaEnCurso, $"No se pudieron bajar los datos del Central: {descarga.Error}");

            var envio = await ambito.ServiceProvider.GetRequiredService<IProcesadorBandejaSalida>().ProcesarAsync(cancelacion);
            registro.LogInformation("Sincronización a pedido: {Creados} creados, {Actualizados} actualizados, {Confirmados} de {Tomados} enviados.",
                descarga.Creados, descarga.Actualizados, envio.Confirmados, envio.Tomados);

            return new ResultadoSincronizacion(envio.Fallidos == 0, yaEnCurso, Resumen(descarga, envio));
        }
        catch (Exception excepcion) when (excepcion is not OperationCanceledException)
        {
            registro.LogWarning(excepcion, "La sincronización a pedido falló");
            return new ResultadoSincronizacion(false, yaEnCurso, "No se pudo sincronizar con el Central. La caja sigue vendiendo con lo que tiene.");
        }
        finally
        {
            _turno.Release();
        }
    }

    public async Task<T> EnTurnoAsync<T>(Func<CancellationToken, Task<T>> ciclo, CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(ciclo);

        await _turno.WaitAsync(cancelacion);
        try
        {
            return await ciclo(cancelacion);
        }
        finally
        {
            _turno.Release();
        }
    }

    /// <summary>
    /// Lo que pasó, contado como se lo diría un técnico al cajero. Siempre se dicen las dos mitades, aunque una sea
    /// «nada»: quien aprieta el botón quiere saber que se le preguntó al Central, y callarlo parecía que no se hizo.
    /// </summary>
    private static string Resumen(ResultadoDescargaMaestros descarga, ResultadoProcesoBandeja envio)
    {
        var bajaron = descarga.Creados + descarga.Actualizados;
        var partes = new List<string>
        {
            bajaron > 0 ? $"bajaron {bajaron:N0} datos del Central" : "el Central no tenía nada nuevo",

            // «Pendientes» y no «documentos»: lo que espera en la bandeja son ventas y cierres, pero también avisos de la
            // propia caja, como el ingreso del cajero. Decir «documentos» hacía buscar una factura que no existía.
            envio.Confirmados switch
            {
                0 => "no había nada por subir",
                1 => "subió 1 pendiente de la caja",
                var cuantos => $"subieron {cuantos:N0} pendientes de la caja",
            },
        };

        if (envio.Fallidos > 0)
            partes.Add(envio.Fallidos == 1 ? "quedó 1 sin subir" : $"quedaron {envio.Fallidos:N0} sin subir");

        return $"Listo: {string.Join(", ", partes)}.";
    }
}
