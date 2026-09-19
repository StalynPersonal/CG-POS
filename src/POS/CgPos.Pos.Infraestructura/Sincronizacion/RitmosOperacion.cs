using CgPos.Pos.Aplicacion.Organizacion;
using CgPos.Pos.Aplicacion.Sincronizacion;
using Microsoft.Extensions.Logging;

namespace CgPos.Pos.Infraestructura.Sincronizacion;

/// <summary>
/// Cada cuánto trabaja la caja por dentro: sincronizar, bajar maestros, mantenerse y respaldar. Son reglas del negocio, así
/// que las configura un usuario en el Central y pueden fijarse en general, por sucursal o por caja.
/// </summary>
/// <remarks>
/// Se leen en cada ciclo, no al arrancar: cambiar el intervalo en el Central lo cambia en las cajas sin reinstalar ni
/// reiniciar nada. Lo que el Central no tenga configurado se toma del <c>appsettings</c> de esa instalación, y si tampoco
/// está, del valor de arranque. Así una caja recién instalada —que todavía no bajó parámetros— funciona igual.
/// </remarks>
internal sealed class RitmosOperacion(
    IParametros parametros,
    IContextoCaja contextoCaja,
    OpcionesSincronizacion sincronizacion,
    OpcionesMantenimiento mantenimiento,
    ILogger<RitmosOperacion> registro) : IRitmosOperacion
{
    public Task<TimeSpan> IntervaloSincronizacionAsync(CancellationToken cancelacion = default) =>
        SegundosAsync(ClavesParametros.IntervaloSincronizacionSegundos, sincronizacion.Intervalo, 5, 3600, cancelacion);

    public Task<TimeSpan> IntervaloMaestrosAsync(CancellationToken cancelacion = default) =>
        SegundosAsync(ClavesParametros.IntervaloMaestrosSegundos, sincronizacion.IntervaloMaestros, 30, 86400, cancelacion);

    public Task<TimeSpan> IntervaloMantenimientoAsync(CancellationToken cancelacion = default) =>
        MinutosAsync(ClavesParametros.IntervaloMantenimientoMinutos, mantenimiento.Intervalo, 1, 1440, cancelacion);

    public async Task<int> TamanoLoteAsync(CancellationToken cancelacion = default) =>
        await EnteroAsync(ClavesParametros.TamanoLoteSincronizacion, sincronizacion.TamanoLote, 1, 500, cancelacion);

    public async Task<OpcionesEspera> EsperasAsync(CancellationToken cancelacion = default) =>
        new(await SegundosAsync(ClavesParametros.EsperaInicialSegundos, sincronizacion.EsperaInicial, 1, 3600, cancelacion),
            await SegundosAsync(ClavesParametros.EsperaMaximaSegundos, sincronizacion.EsperaMaxima, 1, 86400, cancelacion));

    public async Task<int?> HoraRespaldoAsync(CancellationToken cancelacion = default)
    {
        var configurada = await EnteroOpcionalAsync(ClavesParametros.HoraRespaldo, 0, 23, cancelacion);
        return configurada ?? mantenimiento.HoraRespaldo;
    }

    public async Task<string?> ServidorHoraAsync(CancellationToken cancelacion = default)
    {
        var configurado = await parametros.ObtenerAsync(ClavesParametros.ServidorHora, contextoCaja.CajaId, cancelacion);
        return configurado is { Length: > 0 } ? configurado.Trim() : mantenimiento.ServidorHora;
    }

    private async Task<TimeSpan> SegundosAsync(string clave, TimeSpan instalacion, int minimo, int maximo, CancellationToken cancelacion) =>
        TimeSpan.FromSeconds(await EnteroAsync(clave, (int)Math.Round(instalacion.TotalSeconds), minimo, maximo, cancelacion));

    private async Task<TimeSpan> MinutosAsync(string clave, TimeSpan instalacion, int minimo, int maximo, CancellationToken cancelacion) =>
        TimeSpan.FromMinutes(await EnteroAsync(clave, (int)Math.Round(instalacion.TotalMinutes), minimo, maximo, cancelacion));

    private async Task<int> EnteroAsync(string clave, int instalacion, int minimo, int maximo, CancellationToken cancelacion) =>
        await EnteroOpcionalAsync(clave, minimo, maximo, cancelacion) ?? Math.Clamp(instalacion, minimo, maximo);

    /// <summary>El valor del Central, si está y sirve. Un valor mal escrito no detiene la caja: se avisa y sigue con el suyo.</summary>
    private async Task<int?> EnteroOpcionalAsync(string clave, int minimo, int maximo, CancellationToken cancelacion)
    {
        var texto = await parametros.ObtenerAsync(clave, contextoCaja.CajaId, cancelacion);
        if (texto is not { Length: > 0 })
            return null;

        if (!int.TryParse(texto.Trim(), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var valor)
            || valor < minimo || valor > maximo)
        {
            registro.LogWarning("El parámetro {Clave} tiene el valor «{Valor}», que no está entre {Minimo} y {Maximo}: se usa el de esta caja.",
                clave, texto, minimo, maximo);
            return null;
        }

        return valor;
    }
}
