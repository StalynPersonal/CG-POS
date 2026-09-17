using System.Security.Cryptography;
using CgPos.Central.Infraestructura.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CgPos.Central.Infraestructura.CargaInicial;

/// <summary>Huella del último archivo de datos aplicado al arrancar, por clave de configuración (ej. "Maestros:Archivo").</summary>
public sealed class ArchivoArranqueAplicado
{
    public const int LargoMaximoClave = 100;

    private ArchivoArranqueAplicado()
    {
    }

    public string Clave { get; private set; } = string.Empty;

    /// <summary>SHA-256 del contenido en hexadecimal.</summary>
    public string Huella { get; private set; } = string.Empty;

    public DateTimeOffset AplicadoEn { get; private set; }

    internal static ArchivoArranqueAplicado Crear(string clave) => new() { Clave = clave };

    internal void Marcar(string huella, DateTimeOffset ahora)
    {
        Huella = huella;
        AplicadoEn = ahora;
    }
}

internal sealed class ArchivoArranqueAplicadoConfiguracion : IEntityTypeConfiguration<ArchivoArranqueAplicado>
{
    public void Configure(EntityTypeBuilder<ArchivoArranqueAplicado> constructor)
    {
        constructor.ToTable("ArchivosArranqueAplicados");
        constructor.HasKey(a => a.Clave);
        constructor.Property(a => a.Clave).HasMaxLength(ArchivoArranqueAplicado.LargoMaximoClave).IsUnicode(false);
        constructor.Property(a => a.Huella).HasMaxLength(64).IsUnicode(false);
    }
}

public static class ExtensionesArchivosArranque
{
    /// <summary>
    /// Aplica un archivo de datos al arrancar solo si cambió desde la última vez. Lo que un usuario edita después en el Manager
    /// no se pisa en cada reinicio; si se modifica el archivo, se vuelve a aplicar completo.
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
        var contexto = ambito.ServiceProvider.GetRequiredService<ContextoDatosCentral>();
        var registro = ambito.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(ExtensionesArchivosArranque));

        var marca = await contexto.ArchivosArranqueAplicados.SingleOrDefaultAsync(a => a.Clave == clave, cancelacion);
        if (marca is not null && string.Equals(marca.Huella, huella, StringComparison.OrdinalIgnoreCase))
        {
            registro.LogInformation("{Clave}: {Archivo} no cambió desde que se aplicó; se conservan los datos actuales", clave, Path.GetFileName(ruta));
            return;
        }

        await aplicar(servicios, ruta, cancelacion);

        // La huella se marca solo después de aplicar: si falla, el próximo arranque lo vuelve a intentar.
        if (marca is null)
        {
            marca = ArchivoArranqueAplicado.Crear(clave);
            contexto.ArchivosArranqueAplicados.Add(marca);
        }

        marca.Marcar(huella, ambito.ServiceProvider.GetRequiredService<TimeProvider>().GetUtcNow());
        await contexto.SaveChangesAsync(cancelacion);
        registro.LogInformation("{Clave}: {Archivo} aplicado", clave, Path.GetFileName(ruta));
    }
}
