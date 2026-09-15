using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CgPos.Contratos.Central;
using CgPos.Contratos.Serializacion;

namespace CgPos.Central.Web.Seguridad;

/// <summary>Llamadas a la API del Central con la sesión del usuario.</summary>
public sealed class ClienteCentral(IHttpClientFactory fabricaHttp)
{
    private HttpClient Http => fabricaHttp.CreateClient(ServicioSesionCentral.NombreHttp);

    // ---------- Seguridad del Central ----------

    public Task<IReadOnlyList<DatosPermisoCentral>?> ListarPermisosAsync() => ListarAsync<DatosPermisoCentral>("api/seguridad/permisos");

    public Task<IReadOnlyList<DatosRolCentral>?> ListarRolesAsync() => ListarAsync<DatosRolCentral>("api/seguridad/roles");

    public Task<RespuestaAdministracion> CrearRolAsync(SolicitudRolCentral solicitud) => EnviarAsync(HttpMethod.Post, "api/seguridad/roles", solicitud);

    public Task<RespuestaAdministracion> ActualizarRolAsync(Guid rolId, SolicitudRolCentral solicitud) => EnviarAsync(HttpMethod.Put, $"api/seguridad/roles/{rolId}", solicitud);

    public Task<RespuestaAdministracion> CambiarEstadoRolAsync(Guid rolId, bool activo) =>
        EnviarAsync(HttpMethod.Post, $"api/seguridad/roles/{rolId}/{(activo ? "activar" : "desactivar")}");

    public Task<IReadOnlyList<DatosUsuarioCentral>?> ListarUsuariosAsync() => ListarAsync<DatosUsuarioCentral>("api/seguridad/usuarios");

    public Task<RespuestaAdministracion> CrearUsuarioAsync(SolicitudUsuarioCentral solicitud) => EnviarAsync(HttpMethod.Post, "api/seguridad/usuarios", solicitud);

    public Task<RespuestaAdministracion> ActualizarUsuarioAsync(Guid usuarioId, SolicitudActualizarUsuarioCentral solicitud) =>
        EnviarAsync(HttpMethod.Put, $"api/seguridad/usuarios/{usuarioId}", solicitud);

    public Task<RespuestaAdministracion> RestablecerContrasenaAsync(Guid usuarioId, string contrasenaTemporal) =>
        EnviarAsync(HttpMethod.Post, $"api/seguridad/usuarios/{usuarioId}/contrasena", new SolicitudContrasenaTemporal(contrasenaTemporal));

    public Task<RespuestaAdministracion> DesbloquearUsuarioAsync(Guid usuarioId) => EnviarAsync(HttpMethod.Post, $"api/seguridad/usuarios/{usuarioId}/desbloquear");

    public Task<RespuestaAdministracion> CambiarEstadoUsuarioAsync(Guid usuarioId, bool activo) =>
        EnviarAsync(HttpMethod.Post, $"api/seguridad/usuarios/{usuarioId}/{(activo ? "activar" : "desactivar")}");

    // ---------- Comunes ----------

    /// <returns>Nulo si no se pudo consultar (sin comunicación, sesión vencida o sin permiso).</returns>
    private async Task<IReadOnlyList<T>?> ListarAsync<T>(string ruta)
    {
        try
        {
            return await Http.GetFromJsonAsync<List<T>>(ruta, OpcionesJson.Predeterminadas);
        }
        catch (Exception excepcion) when (excepcion is HttpRequestException or JsonException)
        {
            return null;
        }
    }

    private async Task<RespuestaAdministracion> EnviarAsync(HttpMethod metodo, string ruta, object? cuerpo = null)
    {
        try
        {
            using var solicitud = new HttpRequestMessage(metodo, ruta)
            {
                Content = cuerpo is null ? null : JsonContent.Create(cuerpo, cuerpo.GetType(), options: OpcionesJson.Predeterminadas),
            };
            using var respuesta = await Http.SendAsync(solicitud);

            if (respuesta.StatusCode == HttpStatusCode.Unauthorized)
                return new RespuestaAdministracion(false, "La sesión venció. Ingrese nuevamente.");
            if (respuesta.StatusCode == HttpStatusCode.Forbidden)
                return new RespuestaAdministracion(false, "No tiene permiso para esta operación.");

            if (respuesta.Content.Headers.ContentType?.MediaType == "application/json"
                && await respuesta.Content.ReadFromJsonAsync<RespuestaAdministracion>(OpcionesJson.Predeterminadas) is { } datos)
                return datos;

            // Reglas de negocio sin configurar (422) y otros errores llegan como texto.
            var texto = await respuesta.Content.ReadAsStringAsync();
            return new RespuestaAdministracion(respuesta.IsSuccessStatusCode,
                string.IsNullOrWhiteSpace(texto) ? $"Respuesta inesperada del Central ({(int)respuesta.StatusCode})." : texto);
        }
        catch (HttpRequestException)
        {
            return new RespuestaAdministracion(false, ServicioSesionCentral.SinComunicacion);
        }
        catch (JsonException)
        {
            return new RespuestaAdministracion(false, "La respuesta del Central no es válida.");
        }
    }
}
