using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using CgPos.Contratos.Seguridad;
using Microsoft.AspNetCore.Components.Authorization;

namespace CgPos.Pos.Web.Seguridad;

/// <summary>
/// Sesión activa en esta pantalla. El token vive solo en memoria: recargar la página exige volver a ingresar.
/// </summary>
public sealed class AlmacenSesion
{
    public string? Token { get; private set; }
    public DateTimeOffset? ExpiraEn { get; private set; }
    public DatosSesion? Sesion { get; private set; }

    public bool Activa => Token is not null && Sesion is not null && ExpiraEn > DateTimeOffset.UtcNow;

    public event Action? Cambio;

    public void Establecer(string token, DateTimeOffset expiraEn, DatosSesion sesion)
    {
        Token = token;
        ExpiraEn = expiraEn;
        Sesion = sesion;
        Cambio?.Invoke();
    }

    public void Limpiar()
    {
        if (Token is null && Sesion is null)
            return;

        Token = null;
        ExpiraEn = null;
        Sesion = null;
        Cambio?.Invoke();
    }

    public bool TienePermiso(string permiso) => Activa && Sesion!.Permisos.Contains(permiso);
}

/// <summary>Agrega el token a las llamadas al Agente y cierra la sesión si el Agente la rechaza (401).</summary>
public sealed class ManejadorTokenAgente(AlmacenSesion almacen) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage solicitud, CancellationToken cancelacion)
    {
        var token = almacen.Token;
        if (token is not null)
            solicitud.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var respuesta = await base.SendAsync(solicitud, cancelacion);

        if (token is not null && respuesta.StatusCode == HttpStatusCode.Unauthorized)
            almacen.Limpiar();

        return respuesta;
    }
}

/// <summary>Expone la sesión a Blazor (AuthorizeView, [Authorize], políticas por permiso).</summary>
public sealed class EstadoAutenticacionCaja : AuthenticationStateProvider, IDisposable
{
    private readonly AlmacenSesion _almacen;

    public EstadoAutenticacionCaja(AlmacenSesion almacen)
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
            new(AtributosToken.UsuarioId, sesion.UsuarioId.ToString()),
            new(AtributosToken.Codigo, sesion.Codigo),
            new(AtributosToken.Nombre, sesion.Nombre),
            new(AtributosToken.Rol, sesion.RolCodigo),
            new(AtributosToken.Nivel, sesion.Nivel.ToString(CultureInfo.InvariantCulture)),
            new(AtributosToken.Caja, sesion.CajaId.ToString()),
        };
        atributos.AddRange(sesion.Permisos.Select(p => new Claim(AtributosToken.Permiso, p)));

        return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity(atributos, "CgPos", AtributosToken.Nombre, AtributosToken.Rol)));
    }
}
