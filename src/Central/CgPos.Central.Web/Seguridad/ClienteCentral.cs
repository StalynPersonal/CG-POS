using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CgPos.Contratos.Central;
using CgPos.Contratos.Serializacion;
using CgPos.Dominio.Organizacion;

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

    // ---------- Organización ----------

    public async Task<DatosEmpresa?> ObtenerEmpresaAsync()
    {
        try
        {
            return await Http.GetFromJsonAsync<DatosEmpresa>("api/organizacion/empresa", OpcionesJson.Predeterminadas);
        }
        catch (Exception excepcion) when (excepcion is HttpRequestException or JsonException)
        {
            return null;
        }
    }

    public Task<RespuestaAdministracion> ActualizarEmpresaAsync(SolicitudEmpresa solicitud) => EnviarAsync(HttpMethod.Put, "api/organizacion/empresa", solicitud);

    public Task<IReadOnlyList<DatosSucursal>?> ListarSucursalesAsync() => ListarAsync<DatosSucursal>("api/organizacion/sucursales");

    public Task<RespuestaAdministracion> CrearSucursalAsync(SolicitudSucursal solicitud) => EnviarAsync(HttpMethod.Post, "api/organizacion/sucursales", solicitud);

    public Task<RespuestaAdministracion> ActualizarSucursalAsync(Guid sucursalId, SolicitudSucursal solicitud) =>
        EnviarAsync(HttpMethod.Put, $"api/organizacion/sucursales/{sucursalId}", solicitud);

    public Task<RespuestaAdministracion> CambiarEstadoSucursalAsync(Guid sucursalId, bool activa) =>
        EnviarAsync(HttpMethod.Post, $"api/organizacion/sucursales/{sucursalId}/{(activa ? "activar" : "desactivar")}");

    public Task<IReadOnlyList<DatosCaja>?> ListarCajasAsync() => ListarAsync<DatosCaja>("api/organizacion/cajas");

    public Task<RespuestaAdministracion> CrearCajaAsync(SolicitudCaja solicitud) => EnviarAsync(HttpMethod.Post, "api/organizacion/cajas", solicitud);

    public Task<RespuestaAdministracion> ActualizarCajaAsync(Guid cajaId, SolicitudActualizarCaja solicitud) =>
        EnviarAsync(HttpMethod.Put, $"api/organizacion/cajas/{cajaId}", solicitud);

    public Task<RespuestaAdministracion> CambiarEstadoCajaAsync(Guid cajaId, bool habilitada) =>
        EnviarAsync(HttpMethod.Post, $"api/organizacion/cajas/{cajaId}/{(habilitada ? "habilitar" : "deshabilitar")}");

    public Task<IReadOnlyList<DefinicionParametro>?> ListarCatalogoParametrosAsync() => ListarAsync<DefinicionParametro>("api/organizacion/parametros/catalogo");

    public Task<IReadOnlyList<DatosParametro>?> ListarParametrosAsync() => ListarAsync<DatosParametro>("api/organizacion/parametros");

    public Task<RespuestaAdministracion> CrearParametroAsync(SolicitudParametro solicitud) => EnviarAsync(HttpMethod.Post, "api/organizacion/parametros", solicitud);

    public Task<RespuestaAdministracion> CambiarValorParametroAsync(Guid parametroId, string valor) =>
        EnviarAsync(HttpMethod.Put, $"api/organizacion/parametros/{parametroId}", new SolicitudValorParametro(valor));

    // ---------- Credenciales de las cajas ----------

    /// <returns>La credencial recién emitida (su secreto solo se ve aquí) o el motivo por el que no se emitió.</returns>
    public async Task<(DatosCredencialDispositivo? Credencial, string? Error)> EmitirCredencialAsync(Guid cajaId)
    {
        try
        {
            using var respuesta = await Http.PostAsync($"api/cajas/{cajaId}/credencial", null);
            if (respuesta.IsSuccessStatusCode)
                return (await respuesta.Content.ReadFromJsonAsync<DatosCredencialDispositivo>(OpcionesJson.Predeterminadas), null);

            return (null, respuesta.StatusCode switch
            {
                HttpStatusCode.Forbidden => "No tiene permiso para emitir credenciales de caja.",
                HttpStatusCode.NotFound => "La caja no existe.",
                HttpStatusCode.Unauthorized => "La sesión venció. Ingrese nuevamente.",
                _ => $"El Central respondió {(int)respuesta.StatusCode}.",
            });
        }
        catch (Exception excepcion) when (excepcion is HttpRequestException or JsonException)
        {
            return (null, ServicioSesionCentral.SinComunicacion);
        }
    }

    public Task<RespuestaAdministracion> RevocarCredencialAsync(Guid cajaId, string motivo) =>
        EnviarAsync(HttpMethod.Post, $"api/cajas/{cajaId}/credencial/revocar", new SolicitudRevocacionCredencial(motivo));

    // ---------- Rangos de e-CF ----------

    public Task<IReadOnlyList<DatosSecuenciaEcfCentral>?> ListarSecuenciasAsync() => ListarAsync<DatosSecuenciaEcfCentral>("api/fiscal/secuencias");

    public Task<RespuestaAdministracion> AsignarSecuenciaAsync(SolicitudSecuenciaEcf solicitud) => EnviarAsync(HttpMethod.Post, "api/fiscal/secuencias", solicitud);

    public Task<RespuestaAdministracion> ActualizarSecuenciaAsync(Guid secuenciaId, SolicitudActualizarSecuenciaEcf solicitud) =>
        EnviarAsync(HttpMethod.Put, $"api/fiscal/secuencias/{secuenciaId}", solicitud);

    // ---------- Usuarios y roles de caja ----------

    public Task<IReadOnlyList<CgPos.Dominio.Seguridad.DefinicionPermiso>?> ListarPermisosCajaAsync() =>
        ListarAsync<CgPos.Dominio.Seguridad.DefinicionPermiso>("api/usuarios-caja/permisos");

    public Task<IReadOnlyList<DatosRolCaja>?> ListarRolesCajaAsync() => ListarAsync<DatosRolCaja>("api/usuarios-caja/roles");

    public Task<RespuestaAdministracion> GuardarRolCajaAsync(Guid? rolId, SolicitudRolCaja solicitud) =>
        rolId is { } id ? EnviarAsync(HttpMethod.Put, $"api/usuarios-caja/roles/{id}", solicitud) : EnviarAsync(HttpMethod.Post, "api/usuarios-caja/roles", solicitud);

    public Task<IReadOnlyList<DatosUsuarioCaja>?> ListarUsuariosCajaAsync() => ListarAsync<DatosUsuarioCaja>("api/usuarios-caja/usuarios");

    public Task<RespuestaAdministracion> GuardarUsuarioCajaAsync(Guid? usuarioId, SolicitudUsuarioCaja solicitud) =>
        usuarioId is { } id
            ? EnviarAsync(HttpMethod.Put, $"api/usuarios-caja/usuarios/{id}", solicitud)
            : EnviarAsync(HttpMethod.Post, "api/usuarios-caja/usuarios", solicitud);

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

            // Sin cuerpo (204) es un éxito; reglas sin configurar (422) y otros errores llegan como texto.
            var texto = await respuesta.Content.ReadAsStringAsync();
            if (respuesta.IsSuccessStatusCode)
                return new RespuestaAdministracion(true, string.IsNullOrWhiteSpace(texto) ? null : texto);

            return new RespuestaAdministracion(false, string.IsNullOrWhiteSpace(texto) ? $"Respuesta inesperada del Central ({(int)respuesta.StatusCode})." : texto);
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
