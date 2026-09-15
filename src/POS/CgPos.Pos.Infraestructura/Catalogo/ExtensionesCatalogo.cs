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

    /// <summary>Importa el padrón DGII desde un archivo local (inserta nuevos y actualiza cambios).</summary>
    public static async Task<ResultadoImportacionPadron> ImportarPadronDgiiAsync(this IServiceProvider servicios, string rutaArchivo, CancellationToken cancelacion = default)
    {
        if (!File.Exists(rutaArchivo))
            throw new FileNotFoundException($"No se encontró el archivo del padrón DGII: {rutaArchivo}", rutaArchivo);

        await using var ambito = servicios.CreateAsyncScope();
        await using var archivo = File.OpenRead(rutaArchivo);
        return await ambito.ServiceProvider.GetRequiredService<IImportadorPadronDgii>().ImportarAsync(archivo, cancelacion);
    }
}
