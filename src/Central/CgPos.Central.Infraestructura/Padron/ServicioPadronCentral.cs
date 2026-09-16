using System.Security.Cryptography;
using CgPos.Central.Aplicacion.Organizacion;
using CgPos.Central.Aplicacion.Padron;
using CgPos.Contratos.Central;

namespace CgPos.Central.Infraestructura.Padron;

internal sealed class ServicioPadronCentral(IParametrosCentral parametros) : IServicioPadronCentral
{
    public async Task<DatosPadronPublicado?> PublicadoAsync(CancellationToken cancelacion = default)
    {
        if (await RutaAsync(cancelacion) is not { } publicado)
            return null;

        var archivo = new FileInfo(publicado.Ruta);
        await using var flujo = archivo.OpenRead();
        var hash = Convert.ToHexString(await SHA256.HashDataAsync(flujo, cancelacion));

        return new DatosPadronPublicado(publicado.Version, archivo.Name, hash, archivo.Length, archivo.LastWriteTimeUtc);
    }

    public async Task<Stream?> AbrirArchivoAsync(CancellationToken cancelacion = default) =>
        await RutaAsync(cancelacion) is { } publicado
            ? new FileStream(publicado.Ruta, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, useAsync: true)
            : null;

    /// <summary>Archivo publicado según los parámetros; nulo si falta la configuración o el archivo.</summary>
    private async Task<(string Version, string Ruta)?> RutaAsync(CancellationToken cancelacion)
    {
        var ruta = await parametros.ObtenerAsync(ClavesParametrosCentral.PadronArchivo, cancelacion);
        var version = await parametros.ObtenerAsync(ClavesParametrosCentral.PadronVersion, cancelacion);
        if (ruta is not { Length: > 0 } || version is not { Length: > 0 })
            return null;

        return File.Exists(ruta) ? (version, ruta) : null;
    }
}
