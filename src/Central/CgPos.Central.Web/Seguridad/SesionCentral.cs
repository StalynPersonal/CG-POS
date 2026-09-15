using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using CgPos.Contratos.Central;
using CgPos.Contratos.Serializacion;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;

namespace CgPos.Central.Web.Seguridad;

/// <summary>
/// Sesión del Central Manager en esta pestaña. El token de acceso vive solo en memoria; el de renovación se guarda en el almacenamiento de la pestaña
/// (sessionStorage) para que recargar no pida la contraseña. Cerrar la pestaña termina la sesión en este equipo.
/// </summary>
public sealed class AlmacenSesionCentral(IJSRuntime js)
{
    private const string ClaveRenovacion = "cgpos.central.renovacion";

    public string? TokenAcceso { get; private set; }
    public DateTimeOffset? AccesoExpiraEn { get; private set; }
    public DatosSesionCentral? Sesion { get; private set; }

    public bool Activa => TokenAcceso is not null && Sesion is not null;

    public event Action? Cambio;

    public async Task EstablecerAsync(RespuestaSesionCentral respuesta)
    {
        TokenAcceso = respuesta.TokenAcceso;
        AccesoExpiraEn = respuesta.AccesoExpiraEn;
        Sesion = respuesta.Sesion;
        if (respuesta.TokenRenovacion is { Length: > 0 } renovacion)
            await js.InvokeVoidAsync("sessionStorage.setItem", ClaveRenovacion, renovacion);

        Cambio?.Invoke();
    }

    public async Task<string?> LeerRenovacionAsync() => await js.InvokeAsync<string?>("sessionStorage.getItem", ClaveRenovacion);

    public async Task LimpiarAsync()
    {
        var habiaSesion = TokenAcceso is not null || Sesion is not null;
        TokenAcceso = null;
        AccesoExpiraEn = null;
        Sesion = null;
        await js.InvokeVoidAsync("sessionStorage.removeItem", ClaveRenovacion);

        if (habiaSesion)
            Cambio?.Invoke();
    }
}

/// <summary>Ingreso, renovación automática, cambio de contraseña y cierre de la sesión del Central.</summary>
public sealed class ServicioSesionCentral(IHttpClientFactory fabricaHttp, AlmacenSesionCentral almacen)
{
    /// <summary>Cliente sin el manejador de token: ingreso y renovación.</summary>
    public const string NombreHttpSinSesion = "CentralSinSesion";

    /// <summary>Cliente con el token de la sesión y renovación automática.</summary>
    public const string NombreHttp = "Central";

    public const string SinComunicacion = "No hay comunicación con el Central.";

    private readonly SemaphoreSlim _bloqueo = new(1, 1);

    public async Task<RespuestaSesionCentral> IngresarAsync(string usuario, string contrasena)
    {
        var respuesta = await PublicarAsync(NombreHttpSinSesion, "api/sesion/ingreso", new SolicitudIngresoCentral(usuario, contrasena));
        if (respuesta.Exitoso)
            await almacen.EstablecerAsync(respuesta);

        return respuesta;
    }

    /// <summary>Renueva con el token de renovación guardado.</summary>
    /// <param name="tokenVencido">Token con el que falló la llamada: si otra llamada ya renovó, no se vuelve a renovar.</param>
    /// <returns><c>true</c> si hay un token de acceso vigente al terminar.</returns>
    public async Task<bool> RenovarAsync(string? tokenVencido = null)
    {
        await _bloqueo.WaitAsync();
        try
        {
            if (tokenVencido is not null && almacen.Activa && almacen.TokenAcceso != tokenVencido)
                return true;

            var renovacion = await almacen.LeerRenovacionAsync();
            if (string.IsNullOrEmpty(renovacion))
            {
                await almacen.LimpiarAsync();
                return false;
            }

            var respuesta = await PublicarAsync(NombreHttpSinSesion, "api/sesion/renovar", new SolicitudRenovacionSesion(renovacion));
            if (respuesta.Exitoso)
            {
                await almacen.EstablecerAsync(respuesta);
                return true;
            }

            // Sin comunicación se conserva la sesión para reintentar; vencida o revocada se cierra.
            if (respuesta.Mensaje != SinComunicacion)
                await almacen.LimpiarAsync();

            return false;
        }
        finally
        {
            _bloqueo.Release();
        }
    }

    public Task<bool> RestaurarAsync() => RenovarAsync();

    public async Task<RespuestaSesionCentral> CambiarContrasenaAsync(string actual, string nueva)
    {
        var respuesta = await PublicarAsync(NombreHttp, "api/sesion/contrasena", new SolicitudCambioContrasena(actual, nueva));
        if (respuesta.Exitoso)
            await almacen.EstablecerAsync(respuesta);

        return respuesta;
    }

    public async Task CerrarAsync()
    {
        try
        {
            using var respuesta = await fabricaHttp.CreateClient(NombreHttp).PostAsync("api/sesion/cerrar", null);
        }
        catch (HttpRequestException)
        {
            // Aunque el Central no responda, la sesión se cierra en esta pestaña.
        }

        await almacen.LimpiarAsync();
    }

    private async Task<RespuestaSesionCentral> PublicarAsync<T>(string nombreCliente, string ruta, T cuerpo)
    {
        try
        {
            using var respuesta = await fabricaHttp.CreateClient(nombreCliente).PostAsJsonAsync(ruta, cuerpo, OpcionesJson.Predeterminadas);
            if (respuesta.Content.Headers.ContentType?.MediaType == "application/json")
                return await respuesta.Content.ReadFromJsonAsync<RespuestaSesionCentral>(OpcionesJson.Predeterminadas)
                    ?? new RespuestaSesionCentral(false, "El Central respondió sin datos.");

            var texto = await respuesta.Content.ReadAsStringAsync();
            return new RespuestaSesionCentral(false, string.IsNullOrWhiteSpace(texto) ? $"El Central respondió {(int)respuesta.StatusCode}." : texto);
        }
        catch (HttpRequestException)
        {
            return new RespuestaSesionCentral(false, SinComunicacion);
        }
        catch (JsonException)
        {
            return new RespuestaSesionCentral(false, "La respuesta del Central no es válida.");
        }
    }
}

/// <summary>
/// Agrega el token a las llamadas al Central y lo renueva antes de que venza. Si el Central rechaza el token (401) intenta renovar una vez y repite la
/// llamada; si no se puede renovar, la sesión se cierra.
/// </summary>
public sealed class ManejadorTokenCentral(AlmacenSesionCentral almacen, ServicioSesionCentral sesiones) : DelegatingHandler
{
    private static readonly TimeSpan MargenRenovacion = TimeSpan.FromSeconds(30);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage solicitud, CancellationToken cancelacion)
    {
        if (almacen.Activa && almacen.AccesoExpiraEn - MargenRenovacion <= DateTimeOffset.UtcNow)
            await sesiones.RenovarAsync(almacen.TokenAcceso);

        var token = almacen.TokenAcceso;
        Autorizar(solicitud, token);
        var respuesta = await base.SendAsync(solicitud, cancelacion);

        if (token is null || respuesta.StatusCode != HttpStatusCode.Unauthorized || !await sesiones.RenovarAsync(token))
            return respuesta;

        respuesta.Dispose();
        var reintento = new HttpRequestMessage(solicitud.Method, solicitud.RequestUri) { Content = solicitud.Content };
        foreach (var encabezado in solicitud.Headers.Where(e => e.Key != "Authorization"))
            reintento.Headers.TryAddWithoutValidation(encabezado.Key, encabezado.Value);

        Autorizar(reintento, almacen.TokenAcceso);
        return await base.SendAsync(reintento, cancelacion);
    }

    private static void Autorizar(HttpRequestMessage solicitud, string? token) =>
        solicitud.Headers.Authorization = token is null ? null : new AuthenticationHeaderValue("Bearer", token);
}

/// <summary>Expone la sesión a Blazor (AuthorizeView, [Authorize], políticas por permiso). Con contraseña temporal no aplica ningún permiso.</summary>
public sealed class EstadoAutenticacionCentral : AuthenticationStateProvider, IDisposable
{
    private readonly AlmacenSesionCentral _almacen;

    public EstadoAutenticacionCentral(AlmacenSesionCentral almacen)
    {
        _almacen = almacen;
        _almacen.Cambio += AlCambiarSesion;
    }

    public override Task<AuthenticationState> GetAuthenticationStateAsync() => Task.FromResult(Construir());

    public void Dispose() => _almacen.Cambio -= AlCambiarSesion;

    private void AlCambiarSesion() => NotifyAuthenticationStateChanged(Task.FromResult(Construir()));

    private AuthenticationState Construir()
    {
        if (!_almacen.Activa)
            return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));

        var sesion = _almacen.Sesion!;
        var atributos = new List<Claim>
        {
            new(AtributosTokenCentral.Tipo, AtributosTokenCentral.TipoUsuario),
            new(AtributosTokenCentral.UsuarioId, sesion.UsuarioId.ToString()),
            new(AtributosTokenCentral.Codigo, sesion.Codigo),
            new(AtributosTokenCentral.Nombre, sesion.Nombre),
            new(AtributosTokenCentral.Rol, sesion.RolCodigo),
            new(AtributosTokenCentral.RolNombre, sesion.RolNombre),
            new(AtributosTokenCentral.Sesion, sesion.SesionId.ToString()),
        };

        if (sesion.DebeCambiarContrasena)
            atributos.Add(new Claim(AtributosTokenCentral.CambiarContrasena, "true"));
        else
            atributos.AddRange(sesion.Permisos.Select(p => new Claim(AtributosTokenCentral.Permiso, p)));

        return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity(atributos, "CgPosCentral", AtributosTokenCentral.Nombre, AtributosTokenCentral.Rol)));
    }
}
