using System.Net.Http.Headers;
using System.Net.Http.Json;
using CgPos.Contratos.Central;
using CgPos.Contratos.Serializacion;

namespace CgPos.Central.Web.Seguridad;

/// <summary>
/// Llamadas del módulo de cuadre. Lleva su propia sesión: el supervisor entra con su usuario de caja, no con el del
/// Central Manager, y esta computadora está en la tienda, así que la sesión vive solo mientras la pantalla está abierta.
/// </summary>
public sealed class ClienteCuadre(IHttpClientFactory fabricaHttp)
{
    private string? _token;

    public DatosSesionCuadre? Sesion { get; private set; }

    public bool Conectado => _token is { Length: > 0 } && Sesion is not null;

    public bool Puede(string permiso) => Sesion?.Permisos.Contains(permiso) ?? false;

    public async Task<string?> IngresarAsync(string usuario, string clave, CancellationToken cancelacion = default)
    {
        using var http = Crear();
        using var respuesta = await http.PostAsJsonAsync("api/cuadre/ingreso", new SolicitudIngresoCuadre(usuario, clave), OpcionesJson.Predeterminadas, cancelacion);
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<RespuestaSesionCuadre>(OpcionesJson.Predeterminadas, cancelacion);

        if (cuerpo is not { Exitosa: true, TokenAcceso: { Length: > 0 } token, Sesion: { } sesion })
            return cuerpo?.Mensaje ?? "No se pudo entrar al módulo de cuadre.";

        (_token, Sesion) = (token, sesion);
        return null;
    }

    public void Salir() => (_token, Sesion) = (null, null);

    /// <summary>Las denominaciones con las que el supervisor cuenta el efectivo.</summary>
    public async Task<IReadOnlyList<CgPos.Contratos.Catalogo.DatosDenominacion>> DenominacionesAsync(CancellationToken cancelacion = default)
    {
        using var http = Crear();
        try
        {
            return await http.GetFromJsonAsync<List<CgPos.Contratos.Catalogo.DatosDenominacion>>("api/cuadre/denominaciones", OpcionesJson.Predeterminadas, cancelacion) ?? [];
        }
        catch (Exception excepcion) when (excepcion is HttpRequestException or System.Text.Json.JsonException)
        {
            return [];
        }
    }

    /// <summary>Los cierres que las cajas entregaron y todavía nadie ha cuadrado.</summary>
    public Task<IReadOnlyList<DatosCierreCaja>> PendientesAsync(int sucursalId, CancellationToken cancelacion = default) =>
        ListarAsync($"api/cuadre/pendientes?sucursalId={sucursalId}", cancelacion);

    /// <summary>Los cierres de la sucursal en un rango de fechas, cuadrados o no.</summary>
    public Task<IReadOnlyList<DatosCierreCaja>> CierresAsync(int sucursalId, DateOnly desde, DateOnly hasta, CancellationToken cancelacion = default) =>
        ListarAsync($"api/cuadre/cierres?sucursalId={sucursalId}&desde={desde:yyyy-MM-dd}&hasta={hasta:yyyy-MM-dd}", cancelacion);

    public Task<RespuestaAdministracion> CuadrarAsync(int cierreId, SolicitudCuadreCierre solicitud, CancellationToken cancelacion = default) =>
        EnviarAsync($"api/cuadre/{cierreId}", solicitud, cancelacion);

    public Task<RespuestaAdministracion> CorregirAsync(int cierreId, SolicitudAjusteCierre solicitud, CancellationToken cancelacion = default) =>
        EnviarAsync($"api/cuadre/{cierreId}/correccion", solicitud, cancelacion);

    private HttpClient Crear()
    {
        var http = fabricaHttp.CreateClient(ServicioSesionCentral.NombreHttpSinSesion);
        if (_token is { Length: > 0 } token)
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return http;
    }

    private async Task<IReadOnlyList<DatosCierreCaja>> ListarAsync(string ruta, CancellationToken cancelacion)
    {
        using var http = Crear();
        try
        {
            return await http.GetFromJsonAsync<List<DatosCierreCaja>>(ruta, OpcionesJson.Predeterminadas, cancelacion) ?? [];
        }
        catch (Exception excepcion) when (excepcion is HttpRequestException or System.Text.Json.JsonException)
        {
            return [];
        }
    }

    private async Task<RespuestaAdministracion> EnviarAsync(string ruta, object solicitud, CancellationToken cancelacion)
    {
        using var http = Crear();
        try
        {
            using var respuesta = await http.PostAsJsonAsync(ruta, solicitud, OpcionesJson.Predeterminadas, cancelacion);
            return await respuesta.Content.ReadFromJsonAsync<RespuestaAdministracion>(OpcionesJson.Predeterminadas, cancelacion)
                   ?? new RespuestaAdministracion(false, "El Central no respondió.");
        }
        catch (Exception excepcion) when (excepcion is HttpRequestException or System.Text.Json.JsonException)
        {
            return new RespuestaAdministracion(false, "No hay comunicación con el Central.");
        }
    }
}
