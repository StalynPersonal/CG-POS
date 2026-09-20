using System.Net;
using System.Net.Http.Json;
using CgPos.Contratos.Central;
using CgPos.Contratos.Serializacion;
using CgPos.Pos.Aplicacion.Sincronizacion;
using Microsoft.Extensions.Configuration;

namespace CgPos.Pos.Infraestructura.Sincronizacion;

internal static class FabricaValidadorConfiguracion
{
    public static IValidadorConfiguracionCaja Crear(IConfiguration configuracion)
    {
        // Con el Central simulado no hay a quién preguntarle: la configuración se guarda tal como se escribió.
        if (string.Equals(configuracion[ClavesSincronizacion.ModoCentral], ClavesSincronizacion.ModoSimulado, StringComparison.OrdinalIgnoreCase))
            return new ValidacionConfiguracionOmitida();

        return new ValidadorConfiguracionCentral(OpcionesSincronizacion.Leer(configuracion).TiempoEspera);
    }
}

/// <summary>No pregunta nada y acepta: es para desarrollo y pruebas, donde no hay un Central de verdad.</summary>
internal sealed class ValidacionConfiguracionOmitida : IValidadorConfiguracionCaja
{
    public Task<string?> ValidarAsync(SolicitudConfigurarCaja solicitud, CancellationToken cancelacion = default) => Task.FromResult<string?>(null);
}

/// <summary>
/// Le pregunta al Central, con la dirección que se acaba de escribir en la pantalla, si acepta esta caja y a quien la está
/// configurando. Así el técnico se entera en el momento —y no en el primer ciclo de sincronización— de que la dirección del
/// Central, la credencial o su propio usuario no sirven.
/// </summary>
internal sealed class ValidadorConfiguracionCentral(TimeSpan tiempoEspera) : IValidadorConfiguracionCaja
{
    private const string Ruta = "/api/dispositivos/configuracion";

    public async Task<string?> ValidarAsync(SolicitudConfigurarCaja solicitud, CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(solicitud);

        if (!Uri.TryCreate(solicitud.UrlCentral?.Trim(), UriKind.Absolute, out var central))
            return $"«{solicitud.UrlCentral}» no es una dirección válida del Central. Escríbala completa, por ejemplo http://central:5280.";

        var peticion = new SolicitudValidarConfiguracionCaja(solicitud.SucursalCodigo, solicitud.CajaCodigo, solicitud.Secreto, solicitud.DireccionIp,
            solicitud.Usuario, solicitud.Contrasena);

        try
        {
            using var http = new HttpClient { Timeout = tiempoEspera };
            using var respuesta = await http.PostAsJsonAsync(new Uri(central, Ruta), peticion, OpcionesJson.Predeterminadas, cancelacion);
            if (respuesta.StatusCode is HttpStatusCode.OK)
                return null;

            var cuerpo = respuesta.Content.Headers.ContentType?.MediaType == "application/json"
                ? await respuesta.Content.ReadFromJsonAsync<RespuestaValidarConfiguracionCaja>(OpcionesJson.Predeterminadas, cancelacion)
                : null;

            return cuerpo?.Mensaje is { Length: > 0 } motivo
                ? motivo
                : $"El Central no aceptó la configuración (respondió {(int)respuesta.StatusCode}).";
        }
        catch (Exception excepcion) when (excepcion is HttpRequestException or TaskCanceledException or UriFormatException)
        {
            return $"No se pudo hablar con el Central en {central}: {excepcion.Message}";
        }
    }
}
