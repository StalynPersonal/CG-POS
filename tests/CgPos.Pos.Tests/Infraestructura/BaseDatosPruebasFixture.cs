using CgPos.Pos.Infrastructure;
using CgPos.Pos.Infrastructure.Persistencia;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CgPos.Pos.Tests.Infraestructura;

/// <summary>
/// Crea una base de datos SQL Server temporal (con todas las migraciones) para las pruebas de integración
/// y la elimina al terminar.
/// Configuración: user-secrets de este proyecto o variables de entorno con prefijo CGPOS_
/// (ej. <c>CGPOS_ConnectionStrings__ServidorPruebas</c>, <c>CGPOS_BaseDatos__NivelCompatibilidad</c>).
/// Si no hay servidor configurado, las pruebas se omiten.
/// </summary>
public sealed class BaseDatosPruebasFixture : IAsyncLifetime
{
    public ServiceProvider? Servicios { get; private set; }

    /// <summary>Motivo por el que se omiten las pruebas; nulo si la base está disponible.</summary>
    public string? MotivoOmision { get; private set; }

    public async Task InitializeAsync()
    {
        var configuracion = new ConfigurationBuilder()
            .AddUserSecrets<BaseDatosPruebasFixture>(optional: true)
            .AddEnvironmentVariables("CGPOS_")
            .Build();

        var servidor = configuracion.GetConnectionString("ServidorPruebas");
        if (string.IsNullOrWhiteSpace(servidor))
        {
            MotivoOmision = "No hay servidor SQL de pruebas configurado (ConnectionStrings:ServidorPruebas).";
            return;
        }

        var cadena = new SqlConnectionStringBuilder(servidor)
        {
            InitialCatalog = $"CgPosPruebas_{Guid.NewGuid():N}",
        }.ConnectionString;

        var configuracionApp = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"ConnectionStrings:{DependencyInjection.NombreConexion}"] = cadena,
                ["BaseDatos:NivelCompatibilidad"] = configuracion["BaseDatos:NivelCompatibilidad"],
            })
            .Build();

        Servicios = new ServiceCollection()
            .AddPosInfraestructura(configuracionApp)
            .BuildServiceProvider();

        await using var scope = Servicios.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<PosDbContext>().Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        if (Servicios is null)
            return;

        await using (var scope = Servicios.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<PosDbContext>().Database.EnsureDeletedAsync();
        }

        await Servicios.DisposeAsync();
    }
}
