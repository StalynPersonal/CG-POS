using CgPos.Pos.Aplicacion.Catalogo;
using Microsoft.Extensions.DependencyInjection;

namespace CgPos.Pos.Infraestructura.Catalogo;

public static class ExtensionesCatalogo
{
    /// <summary>Aplica un archivo de maestros al arrancar. Es idempotente, así que puede correr en cada inicio.</summary>
    public static async Task<ResultadoCargaMaestros> AplicarMaestrosAsync(this IServiceProvider servicios, string rutaArchivo, CancellationToken cancelacion = default)
    {
        await using var ambito = servicios.CreateAsyncScope();
        return await ambito.ServiceProvider.GetRequiredService<ICargaMaestros>().AplicarDesdeArchivoAsync(rutaArchivo, cancelacion);
    }

}
