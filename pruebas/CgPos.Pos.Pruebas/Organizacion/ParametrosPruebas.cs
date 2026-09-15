using CgPos.Dominio.Organizacion;
using CgPos.Pos.Aplicacion.Organizacion;
using CgPos.Pos.Infraestructura.Persistencia;
using CgPos.Pos.Pruebas.Infraestructura;
using CgPos.Pos.Pruebas.Soporte;
using Microsoft.Extensions.DependencyInjection;

namespace CgPos.Pos.Pruebas.Organizacion;

public class ParametrosPruebas(BaseDatosPruebas baseDatos) : IClassFixture<BaseDatosPruebas>
{
    private static readonly Guid Empresa = Guid.CreateVersion7();

    [SkippableFact]
    public async Task Prevalece_caja_luego_sucursal_luego_general()
    {
        Skip.If(baseDatos.MotivoOmision is not null, baseDatos.MotivoOmision);
        var escenario = await EscenarioSeguridad.CrearAsync(baseDatos, Empresa);
        var clave = $"Prueba.Precedencia{escenario.Sufijo}";

        await using (var ambito = baseDatos.Servicios!.CreateAsyncScope())
        {
            var contexto = ambito.ServiceProvider.GetRequiredService<ContextoDatosPos>();
            contexto.Parametros.AddRange(
                Parametro.Crear(clave, "general"),
                Parametro.Crear(clave, "sucursal", sucursalId: escenario.Sucursal),
                Parametro.Crear(clave, "caja", cajaId: escenario.CajaUno));
            await contexto.SaveChangesAsync();
        }

        await using var ambitoLectura = baseDatos.Servicios!.CreateAsyncScope();
        var parametros = ambitoLectura.ServiceProvider.GetRequiredService<IParametros>();

        Assert.Equal("caja", await parametros.ObtenerAsync(clave, escenario.CajaUno));
        Assert.Equal("sucursal", await parametros.ObtenerAsync(clave, escenario.CajaDos));
        Assert.Equal("general", await parametros.ObtenerAsync(clave));
    }

    [SkippableFact]
    public async Task Parametro_inexistente_o_invalido_se_rechaza_sin_inventar_un_valor()
    {
        Skip.If(baseDatos.MotivoOmision is not null, baseDatos.MotivoOmision);
        var escenario = await EscenarioSeguridad.CrearAsync(baseDatos, Empresa);
        var claveTexto = $"Prueba.NoNumerico{escenario.Sufijo}";

        await using (var ambito = baseDatos.Servicios!.CreateAsyncScope())
        {
            var contexto = ambito.ServiceProvider.GetRequiredService<ContextoDatosPos>();
            contexto.Parametros.Add(Parametro.Crear(claveTexto, "no-es-numero", cajaId: escenario.CajaUno));
            await contexto.SaveChangesAsync();
        }

        await using var ambitoLectura = baseDatos.Servicios!.CreateAsyncScope();
        var parametros = ambitoLectura.ServiceProvider.GetRequiredService<IParametros>();

        Assert.Null(await parametros.ObtenerAsync($"Prueba.NoExiste{escenario.Sufijo}", escenario.CajaUno));
        var noExiste = await Assert.ThrowsAsync<ParametroNoConfiguradoExcepcion>(() =>
            parametros.ObtenerEnteroAsync($"Prueba.NoExiste{escenario.Sufijo}", escenario.CajaUno));
        Assert.Contains("Falta configurar", noExiste.Message);
        var invalido = await Assert.ThrowsAsync<ParametroNoConfiguradoExcepcion>(() => parametros.ObtenerEnteroAsync(claveTexto, escenario.CajaUno));
        Assert.Contains("no es un número entero", invalido.Message);
        Assert.Null(await parametros.ObtenerDecimalOpcionalAsync($"Prueba.NoExiste{escenario.Sufijo}", escenario.CajaUno));
        Assert.Equal(3, await parametros.ObtenerEnteroAsync(ClavesParametros.IntentosMaximosPin, escenario.CajaUno));
    }
}
