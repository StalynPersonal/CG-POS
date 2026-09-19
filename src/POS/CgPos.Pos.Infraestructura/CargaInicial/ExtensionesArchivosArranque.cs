using System.Security.Cryptography;
using CgPos.Pos.Aplicacion.Sincronizacion;
using CgPos.Pos.Infraestructura.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using CgPos.Dominio.Comun;

namespace CgPos.Pos.Infraestructura.CargaInicial;

public static class ExtensionesArchivosArranque
{
    /// <summary>Prefijo de las marcas que guardan la huella de los archivos aplicados al arrancar.</summary>
    public const string PrefijoMarca = "Archivo.";

    /// <summary>
    /// Aplica un archivo de datos al arrancar solo si cambió desde la última vez (la huella queda en las marcas de sincronización).
    /// Así un reinicio no deshace lo que llegó después desde el Central; si se modifica el archivo, se vuelve a aplicar.
    /// </summary>
    public static async Task AplicarArchivoSiCambioAsync(this IServiceProvider servicios, string clave, string ruta,
        Func<IServiceProvider, string, CancellationToken, Task> aplicar, CancellationToken cancelacion = default)
    {
        if (!File.Exists(ruta))
            throw new FileNotFoundException($"No se encontró el archivo: {ruta}", ruta);

        string huella;
        await using (var archivo = File.OpenRead(ruta))
            huella = Convert.ToHexString(await SHA256.HashDataAsync(archivo, cancelacion));

        await using var ambito = servicios.CreateAsyncScope();
        var contexto = ambito.ServiceProvider.GetRequiredService<ContextoDatosPos>();
        var registro = ambito.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(ExtensionesArchivosArranque));
        var claveMarca = PrefijoMarca + clave;

        var marca = await contexto.MarcasSincronizacion.SingleOrDefaultAsync(m => m.Clave == claveMarca, cancelacion);
        if (marca is not null && string.Equals(marca.Texto, huella, StringComparison.OrdinalIgnoreCase))
        {
            registro.LogInformation("{Clave}: {Archivo} no cambió desde que se aplicó; se conservan los datos actuales", clave, Path.GetFileName(ruta));
            return;
        }

        await aplicar(servicios, ruta, cancelacion);

        // La huella se marca solo después de aplicar: si falla, el próximo arranque lo vuelve a intentar.
        var ahora = ambito.ServiceProvider.GetRequiredService<TimeProvider>().Ahora();
        if (marca is null)
        {
            marca = MarcaSincronizacion.Crear(claveMarca, 0, ahora);
            contexto.MarcasSincronizacion.Add(marca);
        }

        marca.ActualizarTexto(huella, ahora);
        await contexto.SaveChangesAsync(cancelacion);
        registro.LogInformation("{Clave}: {Archivo} aplicado", clave, Path.GetFileName(ruta));
    }
}
