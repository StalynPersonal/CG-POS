using System.Security.Cryptography;
using CgPos.Central.Aplicacion.Abstracciones;
using CgPos.Central.Aplicacion.Actualizaciones;
using CgPos.Central.Aplicacion.Organizacion;
using CgPos.Central.Infraestructura.Persistencia;
using CgPos.Contratos.Central;
using Microsoft.EntityFrameworkCore;

namespace CgPos.Central.Infraestructura.Actualizaciones;

internal sealed class ServicioActualizacionesCaja(ContextoDatosCentral contexto, IParametrosCentral parametros, IAuditoriaCentral auditoria, TimeProvider reloj)
    : IServicioActualizacionesCaja
{
    public async Task<DatosActualizacionCaja?> PublicadaAsync(CancellationToken cancelacion = default)
    {
        if (await RutaPaqueteAsync(cancelacion) is not { } paquete)
            return null;

        var archivo = new FileInfo(paquete.Ruta);
        await using var flujo = archivo.OpenRead();
        var hash = Convert.ToHexString(await SHA256.HashDataAsync(flujo, cancelacion));

        return new DatosActualizacionCaja(paquete.Version, archivo.Name, hash, archivo.Length, archivo.LastWriteTimeUtc);
    }

    public async Task<Stream?> AbrirPaqueteAsync(CancellationToken cancelacion = default) =>
        await RutaPaqueteAsync(cancelacion) is { } paquete
            ? new FileStream(paquete.Ruta, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, useAsync: true)
            : null;

    public async Task ReportarVersionAsync(Guid cajaId, string version, CancellationToken cancelacion = default)
    {
        if (await contexto.Cajas.SingleOrDefaultAsync(c => c.Id == cajaId, cancelacion) is not { } caja)
            return;

        if (string.Equals(caja.VersionAgente, version, StringComparison.Ordinal))
            return;

        caja.ReportarVersion(version, reloj.GetUtcNow());
        auditoria.Registrar(new EntradaAuditoria("Actualizaciones.VersionCaja", "Caja", cajaId.ToString(), Detalle: new { caja.Codigo, Version = version }));
        await contexto.SaveChangesAsync(cancelacion);
    }

    public async Task<IReadOnlyList<DatosVersionCaja>> VersionesAsync(CancellationToken cancelacion = default)
    {
        var publicada = await parametros.ObtenerAsync(ClavesParametrosCentral.ActualizacionesVersionPublicada, cancelacion);
        var sucursales = await contexto.Sucursales.AsNoTracking().ToDictionaryAsync(s => s.Id, s => s.Codigo, cancelacion);
        var cajas = await contexto.Cajas.AsNoTracking().OrderBy(c => c.Codigo).ToListAsync(cancelacion);

        return cajas.Select(c => new DatosVersionCaja(c.Id, sucursales.GetValueOrDefault(c.SucursalId) ?? string.Empty, c.Codigo, c.Nombre, c.Habilitada,
            c.VersionAgente, c.VersionReportadaEn, publicada is { Length: > 0 } && string.Equals(c.VersionAgente, publicada, StringComparison.Ordinal))).ToList();
    }

    /// <summary>Paquete publicado en la carpeta configurada; nulo si falta la configuración o el archivo.</summary>
    private async Task<(string Version, string Ruta)?> RutaPaqueteAsync(CancellationToken cancelacion)
    {
        var carpeta = await parametros.ObtenerAsync(ClavesParametrosCentral.ActualizacionesCarpetaPaquetes, cancelacion);
        var version = await parametros.ObtenerAsync(ClavesParametrosCentral.ActualizacionesVersionPublicada, cancelacion);
        if (carpeta is not { Length: > 0 } || version is not { Length: > 0 })
            return null;

        // El nombre del paquete se arma con la versión, para no confundir dos despliegues.
        var ruta = Path.Combine(carpeta, $"cgpos-agente-{version}.zip");
        return File.Exists(ruta) ? (version, ruta) : null;
    }
}
