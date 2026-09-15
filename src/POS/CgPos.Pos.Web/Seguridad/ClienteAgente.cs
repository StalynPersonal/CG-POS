using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CgPos.Contratos.Seguridad;
using CgPos.Contratos.Serializacion;

namespace CgPos.Pos.Web.Seguridad;

/// <summary>Llamadas al servicio local CG-POS Agente (mismo origen que la pantalla).</summary>
public sealed class ClienteAgente(IHttpClientFactory fabricaHttp, AlmacenSesion almacen)
{
    public const string NombreHttp = "Agente";
    private const string SinComunicacion = "No hay comunicación con el servicio de la caja (CG-POS Agente).";

    private HttpClient Http => fabricaHttp.CreateClient(NombreHttp);

    public async Task<DatosEstadoCaja?> ObtenerEstadoCajaAsync(CancellationToken cancelacion = default)
    {
        try
        {
            return await Http.GetFromJsonAsync<DatosEstadoCaja>("api/caja/estado", OpcionesJson.Predeterminadas, cancelacion);
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    public Task<RespuestaIngreso> IngresarConPinAsync(string codigoUsuario, string pin, CancellationToken cancelacion = default) =>
        IngresarAsync("api/sesion/pin", new SolicitudIngresoPin(codigoUsuario, pin), cancelacion);

    public Task<RespuestaIngreso> IngresarConCarneAsync(string codigoBarras, CancellationToken cancelacion = default) =>
        IngresarAsync("api/sesion/carne", new SolicitudIngresoCarne(codigoBarras), cancelacion);

    public Task<RespuestaIngreso> IngresarConHuellaAsync(CancellationToken cancelacion = default) =>
        IngresarAsync<object?>("api/sesion/huella", null, cancelacion);

    public async Task<RespuestaAutorizacion> SolicitarAutorizacionAsync(SolicitudAutorizacion solicitud, CancellationToken cancelacion = default)
    {
        try
        {
            using var respuesta = await Http.PostAsJsonAsync("api/autorizaciones", solicitud, OpcionesJson.Predeterminadas, cancelacion);
            if (respuesta.StatusCode == HttpStatusCode.Unauthorized)
                return new RespuestaAutorizacion(false, "La sesión expiró. Vuelva a iniciar sesión.");

            return await LeerAsync<RespuestaAutorizacion>(respuesta, cancelacion)
                ?? new RespuestaAutorizacion(false, $"Respuesta inesperada del servicio ({(int)respuesta.StatusCode}).");
        }
        catch (HttpRequestException)
        {
            return new RespuestaAutorizacion(false, SinComunicacion);
        }
    }

    public async Task CerrarSesionAsync(CancellationToken cancelacion = default)
    {
        try
        {
            using var respuesta = await Http.PostAsync("api/sesion/cerrar", null, cancelacion);
        }
        catch (HttpRequestException)
        {
            // Sin comunicación: la sesión igual se cierra en esta pantalla.
        }
        finally
        {
            almacen.Limpiar();
        }
    }

    private async Task<RespuestaIngreso> IngresarAsync<TCuerpo>(string ruta, TCuerpo cuerpo, CancellationToken cancelacion)
    {
        try
        {
            using var respuesta = await Http.PostAsJsonAsync(ruta, cuerpo, OpcionesJson.Predeterminadas, cancelacion);
            var resultado = await LeerAsync<RespuestaIngreso>(respuesta, cancelacion)
                ?? new RespuestaIngreso(false, $"Respuesta inesperada del servicio ({(int)respuesta.StatusCode}).");

            if (resultado is { Exitoso: true, Token: { } token, ExpiraEn: { } expiraEn, Sesion: { } sesion })
                almacen.Establecer(token, expiraEn, sesion);

            return resultado;
        }
        catch (HttpRequestException)
        {
            return new RespuestaIngreso(false, SinComunicacion);
        }
    }

    private static async Task<TResultado?> LeerAsync<TResultado>(HttpResponseMessage respuesta, CancellationToken cancelacion)
        where TResultado : class
    {
        if (respuesta.Content.Headers.ContentType?.MediaType != "application/json")
            return null;

        try
        {
            return await respuesta.Content.ReadFromJsonAsync<TResultado>(OpcionesJson.Predeterminadas, cancelacion);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
