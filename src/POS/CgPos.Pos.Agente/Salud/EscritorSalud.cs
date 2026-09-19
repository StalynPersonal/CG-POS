using System.Reflection;
using CgPos.Dominio.Comun;
using CgPos.Pos.Infraestructura.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CgPos.Pos.Agente.Salud;

/// <summary>Respuesta JSON del endpoint /salud (usada por monitoreo local y, más adelante, por el Central).</summary>
public static class EscritorSalud
{
    private static readonly string? Version =
        typeof(EscritorSalud).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;


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
        var cuerpo = new
        {
            estado = EnPalabras(reporte.Status),
            version = Version,
            ambiente = servicios.GetRequiredService<IHostEnvironment>().EnvironmentName,
            equipo = Environment.MachineName,
            hora = servicios.GetRequiredService<TimeProvider>().Ahora(),
            baseDatos = new
            {
                nombre = servicios.GetRequiredService<ContextoDatosPos>().Database.GetDbConnection().Database,
                servidor = servicios.GetRequiredService<ContextoDatosPos>().Database.GetDbConnection().DataSource,
            },
            duracionMs = (int)reporte.TotalDuration.TotalMilliseconds,

            // Lo que hay que resolver, junto y en orden, para no tener que leer todo el detalle.
            pendientes = reporte.Entries
                .Where(e => e.Value.Status != HealthStatus.Healthy)
                .Select(e => $"{e.Key}: {e.Value.Description ?? e.Value.Exception?.Message ?? "sin detalle"}")
                .ToList(),

            verificaciones = reporte.Entries.ToDictionary(
                e => e.Key,
                e => new
                {
                    estado = EnPalabras(e.Value.Status),
                    detalle = e.Value.Description,
                    duracionMs = (int)e.Value.Duration.TotalMilliseconds,
                    error = e.Value.Exception?.Message,
                    datos = e.Value.Data.Count == 0 ? null : e.Value.Data,
                }),
        };

        return contexto.Response.WriteAsJsonAsync(cuerpo);
    }
}
