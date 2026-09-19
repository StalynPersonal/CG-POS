using System.Reflection;
using CgPos.Central.Infraestructura.Persistencia;
using CgPos.Dominio.Comun;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CgPos.Central.Api.Salud;

/// <summary>
/// Respuesta de /salud del Central. Dice lo suficiente para saber que el servidor está bien y contra qué base trabaja, que es
/// lo que se pregunta al instalarlo o al soportarlo.
/// </summary>
/// <remarks>
/// A propósito no dice qué falta configurar (la clave de firma, la DGII, las cajas sin credencial): esta dirección responde a
/// cualquiera en la red, y eso sería un mapa de por dónde entrar. Ese detalle vive en la aplicación, con sesión y permiso.
/// </remarks>
public static class EscritorSaludCentral
{
    private static readonly string? Version =
        typeof(EscritorSaludCentral).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;


    /// <summary>El semáforo en palabras del negocio: nadie tiene que saber inglés para leer esta respuesta.</summary>
    private static string EnPalabras(HealthStatus estado) => estado switch
    {
        HealthStatus.Healthy => "Correcto",
        HealthStatus.Degraded => "Con pendientes",
        _ => "Con fallas",
    };

    public static Task EscribirAsync(HttpContext contexto, HealthReport reporte)
    {
        var servicios = contexto.RequestServices;
        var ambiente = servicios.GetRequiredService<IHostEnvironment>();
        var reloj = servicios.GetRequiredService<TimeProvider>();
        var conexion = servicios.GetRequiredService<ContextoDatosCentral>().Database.GetDbConnection();

        var cuerpo = new
        {
            estado = EnPalabras(reporte.Status),
            version = Version,
            ambiente = ambiente.EnvironmentName,
            servidor = Environment.MachineName,
            hora = reloj.Ahora(),
            baseDatos = new
            {
                nombre = conexion.Database,
                servidor = conexion.DataSource,
            },
            duracionMs = (int)reporte.TotalDuration.TotalMilliseconds,
            verificaciones = reporte.Entries.ToDictionary(
                e => e.Key,
                e => new
                {
                    estado = EnPalabras(e.Value.Status),
                    duracionMs = (int)e.Value.Duration.TotalMilliseconds,
                    error = e.Value.Exception?.Message,
                }),
        };

        return contexto.Response.WriteAsJsonAsync(cuerpo);
    }
}
