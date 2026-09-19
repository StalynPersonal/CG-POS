using CgPos.Dominio.Organizacion;
using CgPos.Pos.Aplicacion.Organizacion;
using CgPos.Pos.Aplicacion.Sincronizacion;
using CgPos.Pos.Infraestructura.Persistencia;
using CgPos.Pos.Infraestructura.Sincronizacion;
using CgPos.Pos.Pruebas.Infraestructura;
using CgPos.Pos.Pruebas.Soporte;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CgPos.Pos.Pruebas.Sincronizacion;

/// <summary>
/// Los ritmos de la caja (cada cuánto sincroniza, se mantiene y respalda) son parámetros del Central. Lo que el Central no
/// configure se toma de la instalación, para que una caja recién puesta funcione antes de bajar nada.
/// </summary>
public class RitmosOperacionPruebas(BaseDatosPruebas baseDatos) : IClassFixture<BaseDatosPruebas>
{
    private static readonly int Empresa = Ids.Siguiente();

    [SkippableFact]
    public async Task El_parametro_del_Central_manda_y_sin_el_queda_el_valor_de_la_instalacion()
    {
        Skip.If(baseDatos.MotivoOmision is not null, baseDatos.MotivoOmision);
        await EscenarioSeguridad.CrearAsync(baseDatos, Empresa);

        // Sin parámetros configurados, los ritmos son los del appsettings de esta instalación.
        await using (var sinParametros = baseDatos.Servicios!.CreateAsyncScope())
        {
            var ritmos = sinParametros.ServiceProvider.GetRequiredService<IRitmosOperacion>();
            var instalacion = sinParametros.ServiceProvider.GetRequiredService<OpcionesSincronizacion>();
            Assert.Equal(instalacion.Intervalo, await ritmos.IntervaloSincronizacionAsync());
            Assert.Equal(instalacion.TamanoLote, await ritmos.TamanoLoteAsync());
        }

        await GuardarAsync(
            (ClavesParametros.IntervaloSincronizacionSegundos, "45"),
            (ClavesParametros.IntervaloMaestrosSegundos, "600"),
            (ClavesParametros.TamanoLoteSincronizacion, "120"),
            (ClavesParametros.IntervaloMantenimientoMinutos, "15"),
            (ClavesParametros.EsperaInicialSegundos, "10"),
            (ClavesParametros.EsperaMaximaSegundos, "600"),
            (ClavesParametros.HoraRespaldo, "23"),
            (ClavesParametros.ServidorHora, "hora.contreras.do"));

        await using var ambito = baseDatos.Servicios!.CreateAsyncScope();
        var configurados = ambito.ServiceProvider.GetRequiredService<IRitmosOperacion>();

        Assert.Equal(TimeSpan.FromSeconds(45), await configurados.IntervaloSincronizacionAsync());
        Assert.Equal(TimeSpan.FromMinutes(10), await configurados.IntervaloMaestrosAsync());
        Assert.Equal(120, await configurados.TamanoLoteAsync());
        Assert.Equal(TimeSpan.FromMinutes(15), await configurados.IntervaloMantenimientoAsync());
        Assert.Equal(23, await configurados.HoraRespaldoAsync());
        Assert.Equal("hora.contreras.do", await configurados.ServidorHoraAsync());

        // La espera se duplica con cada intento hasta el tope, y un rechazo del Central espera el tope de una vez.
        var esperas = await configurados.EsperasAsync();
        Assert.Equal(TimeSpan.FromSeconds(10), esperas.Para(1, rechazado: false));
        Assert.Equal(TimeSpan.FromSeconds(40), esperas.Para(3, rechazado: false));
        Assert.Equal(TimeSpan.FromSeconds(600), esperas.Para(20, rechazado: false));
        Assert.Equal(TimeSpan.FromSeconds(600), esperas.Para(1, rechazado: true));
    }

    [SkippableFact]
    public async Task Un_valor_fuera_de_rango_o_mal_escrito_no_detiene_la_caja()
    {
        Skip.If(baseDatos.MotivoOmision is not null, baseDatos.MotivoOmision);
        await EscenarioSeguridad.CrearAsync(baseDatos, Empresa);

        await GuardarAsync(
            (ClavesParametros.IntervaloSincronizacionSegundos, "0"),
            (ClavesParametros.TamanoLoteSincronizacion, "muchos"),
            (ClavesParametros.HoraRespaldo, "99"));

        await using var ambito = baseDatos.Servicios!.CreateAsyncScope();
        var ritmos = ambito.ServiceProvider.GetRequiredService<IRitmosOperacion>();
        var instalacion = ambito.ServiceProvider.GetRequiredService<OpcionesSincronizacion>();

        // Un valor que no sirve se ignora y se sigue con el de la instalación: la caja nunca se queda sin sincronizar.
        Assert.Equal(instalacion.Intervalo, await ritmos.IntervaloSincronizacionAsync());
        Assert.Equal(instalacion.TamanoLote, await ritmos.TamanoLoteAsync());
        Assert.Null(await ritmos.HoraRespaldoAsync());
    }

    /// <summary>Deja esos parámetros generales con ese valor, como si los hubiera configurado el Central.</summary>
    private async Task GuardarAsync(params (string Clave, string Valor)[] valores)
    {
        await using var ambito = baseDatos.Servicios!.CreateAsyncScope();
        var contexto = ambito.ServiceProvider.GetRequiredService<ContextoDatosPos>();

        var claves = valores.Select(v => v.Clave).ToList();
        var previos = await contexto.Parametros.Where(p => claves.Contains(p.Clave)).ToListAsync();
        contexto.Parametros.RemoveRange(previos);
        await contexto.SaveChangesAsync();

        contexto.Parametros.AddRange(valores.Select(v => Parametro.Crear(v.Clave, v.Valor)));
        await contexto.SaveChangesAsync();
    }
}
