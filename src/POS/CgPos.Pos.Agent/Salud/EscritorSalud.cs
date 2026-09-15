using System.Reflection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CgPos.Pos.Agent.Salud;

/// <summary>Respuesta JSON del endpoint /salud (usada por monitoreo local y, más adelante, por el Central).</summary>
public static class EscritorSalud
{
    private static readonly string? Version =
        typeof(EscritorSalud).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

    public static Task EscribirAsync(HttpContext contexto, HealthReport reporte)
    {
        var cuerpo = new
        {
            estado = reporte.Status.ToString(),
            version = Version,
            duracionMs = (int)reporte.TotalDuration.TotalMilliseconds,
            verificaciones = reporte.Entries.ToDictionary(
                e => e.Key,
                e => new
                {
                    estado = e.Value.Status.ToString(),
                    duracionMs = (int)e.Value.Duration.TotalMilliseconds,
                    error = e.Value.Exception?.Message,
                }),
        };

        return contexto.Response.WriteAsJsonAsync(cuerpo);
    }
}
