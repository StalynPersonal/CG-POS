using System.Net;
using System.Net.Http.Json;
using CgPos.Contratos.Serializacion;
using CgPos.Contratos.Ventas;
using CgPos.Pos.Aplicacion.Sincronizacion;
using CgPos.Pos.Infraestructura.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CgPos.Pos.Pruebas.Api;

/// <summary>
/// Volver a bajar los maestros del Central. La bajada normal solo trae lo que cambió allá, así que una caja a la que se le
/// borraron datos por su cuenta se queda sin ellos para siempre: esto es lo que la devuelve a cero.
/// </summary>
[Collection(ColeccionAgente.Nombre)]
public class ApiReaprovisionarPruebas(AgenteEnPruebas agente)
{
    [SkippableFact]
    public async Task Reaprovisionar_olvida_la_marca_y_exige_quien_lo_autoriza()
    {
        Skip.If(agente.MotivoOmision is not null, agente.MotivoOmision);
        using var cliente = agente.Fabrica!.CreateClient();

        // La caja va por una versión cualquiera: es lo que el Central usa para mandarle solo lo nuevo.
        await UsarContextoAsync(async contexto =>
        {
            contexto.MarcasSincronizacion.RemoveRange(await contexto.MarcasSincronizacion
                .Where(m => m.Clave == MarcaSincronizacion.VersionMaestros).ToListAsync());
            contexto.MarcasSincronizacion.Add(MarcaSincronizacion.Crear(MarcaSincronizacion.VersionMaestros, 925_234, DateTimeOffset.UtcNow));
            await contexto.SaveChangesAsync();
        });

        // Sin quién lo autorice no se hace: deja la caja sin vender un buen rato.
        var sinUsuario = await ReaprovisionarAsync(cliente, new SolicitudReaprovisionarCaja(null, null));
        Assert.Equal(HttpStatusCode.BadRequest, sinUsuario.Estado);
        Assert.Contains("usuario", sinUsuario.Cuerpo!.Mensaje, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(925_234, await MarcaAsync());

        var hecho = await ReaprovisionarAsync(cliente, new SolicitudReaprovisionarCaja("ADMIN", "Clave.Pruebas"));
        Assert.Equal(HttpStatusCode.OK, hecho.Estado);
        Assert.True(hecho.Cuerpo!.Exitosa, hecho.Cuerpo.Mensaje);

        // Sin marca, la próxima bajada pide desde cero y el Central manda el catálogo entero.
        Assert.Null(await MarcaAsync());
    }

    private static async Task<(HttpStatusCode Estado, RespuestaConfiguracion? Cuerpo)> ReaprovisionarAsync(HttpClient cliente,
        SolicitudReaprovisionarCaja solicitud)
    {
        using var respuesta = await cliente.PostAsJsonAsync("/api/configuracion/reaprovisionar", solicitud, OpcionesJson.Predeterminadas);
        return (respuesta.StatusCode, await respuesta.Content.ReadFromJsonAsync<RespuestaConfiguracion>(OpcionesJson.Predeterminadas));
    }

    private Task<long?> MarcaAsync() => UsarContextoAsync(contexto => contexto.MarcasSincronizacion.AsNoTracking()
        .Where(m => m.Clave == MarcaSincronizacion.VersionMaestros)
        .Select(m => (long?)m.Valor)
        .FirstOrDefaultAsync());

    private async Task UsarContextoAsync(Func<ContextoDatosPos, Task> accion)
    {
        await using var ambito = agente.Fabrica!.Services.CreateAsyncScope();
        await accion(ambito.ServiceProvider.GetRequiredService<ContextoDatosPos>());
    }

    private async Task<T> UsarContextoAsync<T>(Func<ContextoDatosPos, Task<T>> accion)
    {
        await using var ambito = agente.Fabrica!.Services.CreateAsyncScope();
        return await accion(ambito.ServiceProvider.GetRequiredService<ContextoDatosPos>());
    }
}
