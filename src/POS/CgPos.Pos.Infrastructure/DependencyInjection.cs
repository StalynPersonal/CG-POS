using CgPos.Pos.Application.Abstracciones;
using CgPos.Pos.Infrastructure.Auditoria;
using CgPos.Pos.Infrastructure.Persistencia;
using CgPos.Pos.Infrastructure.Sincronizacion;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CgPos.Pos.Infrastructure;

public static class DependencyInjection
{
    public const string NombreConexion = "PosDb";

    /// <summary>
    /// Nivel de compatibilidad por defecto: SQL Server 2019 (150), mínimo exigido para la caja.
    /// Se puede bajar por configuración (ej. 120 para un SQL Server 2014 de desarrollo).
    /// </summary>
    public const int NivelCompatibilidadPorDefecto = 150;

    public static IServiceCollection AddPosInfraestructura(this IServiceCollection services, IConfiguration configuration)
    {
        var cadenaConexion = configuration.GetConnectionString(NombreConexion);
        if (string.IsNullOrWhiteSpace(cadenaConexion))
            throw new InvalidOperationException($"Falta la cadena de conexión 'ConnectionStrings:{NombreConexion}'.");

        var nivelCompatibilidad = int.TryParse(configuration["BaseDatos:NivelCompatibilidad"], out var nivel)
            ? nivel
            : NivelCompatibilidadPorDefecto;

        services.AddDbContext<PosDbContext>(opciones => ConfigurarSqlServer(opciones, cadenaConexion, nivelCompatibilidad));

        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<IOutbox, OutboxEscritor>();
        services.AddScoped<IAuditoria, AuditoriaEscritor>();

        return services;
    }

    /// <summary>Configuración de SQL Server compartida por la app y las pruebas de integración.</summary>
    public static DbContextOptionsBuilder ConfigurarSqlServer(DbContextOptionsBuilder opciones, string cadenaConexion, int nivelCompatibilidad) =>
        opciones.UseSqlServer(cadenaConexion, sql =>
        {
            sql.UseCompatibilityLevel(nivelCompatibilidad);
            sql.MigrationsHistoryTable("__HistorialMigraciones");
        });
}
