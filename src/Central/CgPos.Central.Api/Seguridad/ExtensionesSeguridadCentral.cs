using System.Security.Authentication;
using CgPos.Central.Aplicacion.Abstracciones;
using CgPos.Central.Aplicacion.Dispositivos;
using CgPos.Central.Aplicacion.Seguridad;
using Microsoft.AspNetCore.Authorization;
using CgPos.Contratos.Central;
using CgPos.Dominio.Seguridad;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace CgPos.Central.Api.Seguridad;

public static class ExtensionesSeguridadCentral
{
    public const string ClaveExigirHttps = "Central:ExigirHttps";

    /// <summary>
    /// JWT del Central con políticas por permiso (nombre de política = código del permiso) y las de usuario y dispositivo.
    /// Cada token se contrasta con su sesión o credencial: cerrar sesión, revocar o deshabilitar surte efecto de inmediato.
    /// </summary>
    public static IServiceCollection AgregarSeguridadCentral(this IServiceCollection servicios)
    {
        servicios.AddSingleton<EmisorTokensCentral>();

        servicios.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        servicios.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<EmisorTokensCentral>((opciones, emisor) =>
            {
                opciones.MapInboundClaims = false;
                opciones.TokenValidationParameters = emisor.ParametrosValidacion();
                opciones.Events = new JwtBearerEvents { OnTokenValidated = ValidarVigenciaAsync };
            });

        servicios.AddAuthorization(opciones =>
        {
            opciones.AddPolicy(PoliticasCentral.Usuario, politica => politica
                .RequireAuthenticatedUser()
                .RequireClaim(AtributosTokenCentral.Tipo, AtributosTokenCentral.TipoUsuario));

            opciones.AddPolicy(PoliticasCentral.Dispositivo, politica => politica
                .RequireAuthenticatedUser()
                .RequireClaim(AtributosTokenCentral.Tipo, AtributosTokenCentral.TipoDispositivo));

            // El módulo de cuadre acepta dos entradas: el supervisor de tienda con su usuario de caja, y contabilidad con
            // su usuario del Central, que además ve todas las sucursales.
            AgregarPoliticaCuadre(opciones, PoliticasCentral.CuadreConsultar, CatalogoPermisos.ConsultarCuadre, CatalogoPermisosCentral.ConsultarReportes);
            AgregarPoliticaCuadre(opciones, PoliticasCentral.CuadreDeclarar, CatalogoPermisos.DeclararCuadre, CatalogoPermisosCentral.AjustarCierres);
            AgregarPoliticaCuadre(opciones, PoliticasCentral.CuadreCorregir, CatalogoPermisos.CorregirCuadre, CatalogoPermisosCentral.AjustarCierres);

            // Con una contraseña temporal pendiente de cambiar, ningún permiso aplica.
            foreach (var permiso in CatalogoPermisosCentral.Todos)
            {
                opciones.AddPolicy(permiso.Codigo, politica => politica
                    .RequireAuthenticatedUser()
                    .RequireClaim(AtributosTokenCentral.Tipo, AtributosTokenCentral.TipoUsuario)
                    .RequireClaim(AtributosTokenCentral.Permiso, permiso.Codigo)
                    .RequireAssertion(contexto => !contexto.User.HasClaim(AtributosTokenCentral.CambiarContrasena, "true")));
            }
        });

        return servicios;
    }

    /// <summary>Una puerta del módulo de cuadre: el permiso de caja del supervisor o el del usuario del Central.</summary>
    private static void AgregarPoliticaCuadre(AuthorizationOptions opciones, string politica, string permisoTienda, string permisoCentral) =>
        opciones.AddPolicy(politica, constructor => constructor
            .RequireAuthenticatedUser()
            .RequireAssertion(contexto =>
                (contexto.User.HasClaim(AtributosTokenCentral.Tipo, AtributosTokenCentral.TipoCuadre)
                 && contexto.User.HasClaim(AtributosTokenCentral.Permiso, permisoTienda))
                || (contexto.User.HasClaim(AtributosTokenCentral.Tipo, AtributosTokenCentral.TipoUsuario)
                    && contexto.User.HasClaim(AtributosTokenCentral.Permiso, permisoCentral)
                    && !contexto.User.HasClaim(AtributosTokenCentral.CambiarContrasena, "true"))));

    /// <summary>HTTPS del Central solo con TLS 1.2 o 1.3.</summary>
    public static WebApplicationBuilder ConfigurarTlsCentral(this WebApplicationBuilder constructor)
    {
        constructor.WebHost.ConfigureKestrel(kestrel =>
            kestrel.ConfigureHttpsDefaults(https => https.SslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13));
        return constructor;
    }

    /// <summary>
    /// Las API solo responden por HTTPS (credenciales de caja, contraseñas y tokens). Se desactiva con <c>Central:ExigirHttps = false</c>,
    /// solo para desarrollo local.
    /// </summary>
    public static WebApplication UsarHttpsObligatorio(this WebApplication aplicacion)
    {
        if (!aplicacion.Configuration.GetValue(ClaveExigirHttps, true))
        {
            aplicacion.Logger.LogWarning("HTTPS no exigido ({Clave} = false): use esta configuración solo en desarrollo.", ClaveExigirHttps);
            return aplicacion;
        }

        if (!aplicacion.Environment.IsDevelopment())
            aplicacion.UseHsts();

        aplicacion.Use(async (contexto, siguiente) =>
        {
            if (!contexto.Request.IsHttps && contexto.Request.Path.StartsWithSegments("/api"))
            {
                contexto.Response.StatusCode = StatusCodes.Status400BadRequest;
                contexto.Response.ContentType = "text/plain; charset=utf-8";
                await contexto.Response.WriteAsync("El Central solo acepta conexiones HTTPS (TLS 1.2 o superior).");
                return;
            }

            await siguiente(contexto);
        });

        return aplicacion;
    }

    public static OrigenSolicitud OrigenSolicitud(this HttpContext contexto) =>
        new(contexto.Connection.RemoteIpAddress?.ToString(), contexto.Request.Headers.UserAgent.ToString() is { Length: > 0 } agente ? agente : null);

    /// <summary>Respuestas con tokens o secretos: ningún intermediario las guarda.</summary>
    public static TBuilder SinCache<TBuilder>(this TBuilder constructor) where TBuilder : IEndpointConventionBuilder =>
        constructor.AddEndpointFilter(async (contexto, siguiente) =>
        {
            contexto.HttpContext.Response.Headers.CacheControl = "no-store";
            return await siguiente(contexto);
        });

    private static async Task ValidarVigenciaAsync(TokenValidatedContext contexto)
    {
        var principal = contexto.Principal!;
        var servicios = contexto.HttpContext.RequestServices;
        var cancelacion = contexto.HttpContext.RequestAborted;
        string? Valor(string tipo) => principal.FindFirst(tipo)?.Value;

        var vigente = Valor(AtributosTokenCentral.Tipo) switch
        {
            AtributosTokenCentral.TipoUsuario =>
                int.TryParse(Valor(AtributosTokenCentral.UsuarioId), out var usuarioId)
                && Guid.TryParse(Valor(AtributosTokenCentral.Sesion), out var sesionId)
                && await servicios.GetRequiredService<IServicioSesionesCentral>().EsSesionActivaAsync(sesionId, usuarioId, cancelacion),
            AtributosTokenCentral.TipoDispositivo =>
                int.TryParse(Valor(AtributosTokenCentral.Credencial), out var credencialId)
                && int.TryParse(Valor(AtributosTokenCentral.Caja), out var cajaId)
                && await servicios.GetRequiredService<IServicioDispositivos>().EsCredencialActivaAsync(credencialId, cajaId, cancelacion),
            // El módulo de cuadre no abre sesión en el Central: el token dura unos minutos y se vuelve a entrar con el usuario de caja.
            AtributosTokenCentral.TipoCuadre => true,
            _ => false,
        };

        if (!vigente)
            contexto.Fail("La sesión o la credencial ya no está vigente.");
    }
}
