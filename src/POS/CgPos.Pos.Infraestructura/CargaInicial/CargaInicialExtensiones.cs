using CgPos.Pos.Aplicacion.CargaInicial;
using Microsoft.Extensions.DependencyInjection;

namespace CgPos.Pos.Infraestructura.CargaInicial;

public static class CargaInicialExtensiones
{
    /// <summary>Aplica el archivo de carga inicial al arrancar. Es idempotente, así que puede correr en cada inicio.</summary>
    public static async Task<ResultadoCargaInicial> AplicarCargaInicialAsync(this IServiceProvider servicios, string rutaArchivo, CancellationToken cancelacion = default)
    {
        await using var ambito = servicios.CreateAsyncScope();
        var cargaInicial = ambito.ServiceProvider.GetRequiredService<ICargaInicial>();
        return await cargaInicial.AplicarDesdeArchivoAsync(rutaArchivo, cancelacion);
    }
}
