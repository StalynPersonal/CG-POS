using CgPos.Contratos.Seguridad;
using CgPos.Dominio.Seguridad;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace CgPos.Pos.Agente.Seguridad;

public static class ExtensionesSeguridadAgente
{
    /// <summary>JWT local y una política de autorización por cada permiso del catálogo (nombre de política = código del permiso).</summary>
    public static IServiceCollection AgregarSeguridadAgente(this IServiceCollection servicios)
    {
        servicios.AddSingleton<EmisorTokens>();

        servicios.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        servicios.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<EmisorTokens>((opciones, emisor) =>
            {
                opciones.MapInboundClaims = false;
                opciones.TokenValidationParameters = emisor.ParametrosValidacion();
            });

        servicios.AddAuthorization(opciones =>
        {
            foreach (var permiso in CatalogoPermisos.Todos)
            {
                opciones.AddPolicy(permiso.Codigo, politica => politica
                    .RequireAuthenticatedUser()
                    .RequireClaim(AtributosToken.Permiso, permiso.Codigo));
            }
        });

        return servicios;
    }
}
