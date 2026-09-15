using CgPos.Pos.Infraestructura;
using CgPos.Pos.Infraestructura.Persistencia;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CgPos.Pos.Pruebas.Infraestructura;

/// <summary>
/// Crea una base de datos SQL Server temporal (con todas las migraciones) para las pruebas de integración
/// y la elimina al terminar.
/// Configuración: user-secrets de este proyecto o variables de entorno con prefijo CGPOS_
/// (ej. <c>CGPOS_ConnectionStrings__ServidorPruebas</c>, <c>CGPOS_BaseDatos__NivelCompatibilidad</c>).
/// Si no hay servidor configurado, las pruebas se omiten.
/// </summary>
public sealed class BaseDatosPruebas : IAsyncLifetime
{
    private IConfiguration? _configuracionAplicacion;

    public ServiceProvider? Servicios { get; private set; }

    /// <summary>Motivo por el que se omiten las pruebas; nulo si la base está disponible.</summary>
    public string? MotivoOmision { get; private set; }

    public string? CadenaConexion { get; private set; }

    public string? NivelCompatibilidad { get; private set; }

    /// <summary>Carpeta temporal donde la impresora de archivo deja los tickets de esta base de pruebas.</summary>
    public string CarpetaImpresiones { get; } = Path.Combine(Path.GetTempPath(), "CgPosPruebas", $"impresiones-{Guid.NewGuid():N}");

    public async Task InitializeAsync()
    {
        var configuracion = new ConfigurationBuilder()
            .AddUserSecrets<BaseDatosPruebas>(optional: true)
            .AddEnvironmentVariables("CGPOS_")
            .Build();

        var servidor = configuracion.GetConnectionString("ServidorPruebas");
        if (string.IsNullOrWhiteSpace(servidor))
        {
            MotivoOmision = "No hay servidor SQL de pruebas configurado (ConnectionStrings:ServidorPruebas).";
            return;
        }

        CadenaConexion = new SqlConnectionStringBuilder(servidor)
        {
            InitialCatalog = $"CgPosPruebas_{Guid.NewGuid():N}",
        }.ConnectionString;
        NivelCompatibilidad = configuracion["BaseDatos:NivelCompatibilidad"];

        _configuracionAplicacion = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"ConnectionStrings:{InyeccionDependencias.NombreConexion}"] = CadenaConexion,
                ["BaseDatos:NivelCompatibilidad"] = NivelCompatibilidad,
                ["Perifericos:Impresora:Carpeta"] = CarpetaImpresiones,
            })
            .Build();

        Servicios = CrearProveedor();

        await using var ambito = Servicios.CreateAsyncScope();
        await ambito.ServiceProvider.GetRequiredService<ContextoDatosPos>().Database.MigrateAsync();
    }

    /// <summary>
    /// Proveedor de servicios contra la misma base temporal, con ajustes propios de una prueba
    /// (reloj, caja actual, lector de huella…). Quien lo crea debe liberarlo.
    /// </summary>
    public ServiceProvider CrearProveedor(Action<IServiceCollection>? ajustar = null)
    {
        if (_configuracionAplicacion is null)
            throw new InvalidOperationException(MotivoOmision ?? "La base de pruebas no está inicializada.");

        var servicios = new ServiceCollection()
            .AddLogging()
            .AgregarInfraestructuraPos(_configuracionAplicacion);

        ajustar?.Invoke(servicios);
        return servicios.BuildServiceProvider();
    }

    public async Task DisposeAsync()
    {
        if (Servicios is null)
            return;

        await using (var ambito = Servicios.CreateAsyncScope())
        {
            await ambito.ServiceProvider.GetRequiredService<ContextoDatosPos>().Database.EnsureDeletedAsync();
        }

        await Servicios.DisposeAsync();

        if (Directory.Exists(CarpetaImpresiones))
            Directory.Delete(CarpetaImpresiones, recursive: true);
    }
}
