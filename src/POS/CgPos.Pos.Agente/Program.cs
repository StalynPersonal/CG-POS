using System.Text.Json.Serialization;
using CgPos.Pos.Agente.Api;
using CgPos.Pos.Agente.Salud;
using CgPos.Pos.Agente.Seguridad;
using CgPos.Pos.Aplicacion.Organizacion;
using CgPos.Pos.Infraestructura;
using CgPos.Pos.Infraestructura.CargaInicial;
using CgPos.Pos.Infraestructura.Persistencia;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting.WindowsServices;
using Serilog;

// Logger mínimo para registrar fallos antes de que se lea la configuración.
Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateBootstrapLogger();

try
{
    var constructor = WebApplication.CreateBuilder(new WebApplicationOptions
    {
        Args = args,
        // Como servicio de Windows el directorio actual es System32: se usa la carpeta del ejecutable.
        ContentRootPath = WindowsServiceHelpers.IsWindowsService() ? AppContext.BaseDirectory : default,
    });

    constructor.Host.UseWindowsService(opciones => opciones.ServiceName = "CgPosAgente");

    constructor.Services.AddSerilog((servicios, configuracion) => configuracion
        .ReadFrom.Configuration(constructor.Configuration)
        .ReadFrom.Services(servicios)
        .Enrich.FromLogContext());

    // Mismo formato JSON que CgPos.Contratos.Serializacion.OpcionesJson (enums como texto, sin nulos).
    constructor.Services.ConfigureHttpJsonOptions(opciones =>
    {
        opciones.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        opciones.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

    constructor.Services.AgregarInfraestructuraPos(constructor.Configuration);
    constructor.Services.AgregarSeguridadAgente();

    constructor.Services.AddHealthChecks()
        .AddDbContextCheck<ContextoDatosPos>("base-datos");

    var aplicacion = constructor.Build();

    if (aplicacion.Environment.IsDevelopment())
        aplicacion.UseWebAssemblyDebugging();

    aplicacion.UseAuthentication();
    aplicacion.UseAuthorization();

    aplicacion.MapHealthChecks("/salud", new HealthCheckOptions { ResponseWriter = EscritorSalud.EscribirAsync });
    aplicacion.MapearApiSeguridad();

    // Pantallas de la caja (Blazor Wasm de CgPos.Pos.Web), servidas localmente.
    if (aplicacion.Configuration.GetValue("Agente:ServirPantallas", true))
    {
        aplicacion.MapStaticAssets();
        aplicacion.MapFallbackToFile("index.html");
    }

    await aplicacion.Services.InicializarBaseDatosPosAsync();

    // Datos de organización y seguridad desde archivo (desarrollo o instalación sin Central todavía).
    var archivoCargaInicial = aplicacion.Configuration["CargaInicial:Archivo"];
    if (!string.IsNullOrWhiteSpace(archivoCargaInicial))
        await aplicacion.Services.AplicarCargaInicialAsync(Path.GetFullPath(archivoCargaInicial, aplicacion.Environment.ContentRootPath));

    await using (var ambito = aplicacion.Services.CreateAsyncScope())
    {
        var estado = await ambito.ServiceProvider.GetRequiredService<IEstadoCaja>().ObtenerAsync();
        if (estado.Problema is not null)
            Log.Warning("Estado de la caja: {Problema}", estado.Problema);
        else
            Log.Information("Caja {Caja} ({Nombre}) habilitada en {Sucursal}", estado.CajaCodigo, estado.CajaNombre, estado.SucursalNombre);
    }

    await aplicacion.RunAsync();
}
catch (Exception excepcion) when (excepcion is not HostAbortedException)
{
    // Se relanza para que el proceso termine con error y Windows reinicie el servicio.
    Log.Fatal(excepcion, "CG-POS Agente terminó por un error no controlado");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}

/// <summary>Punto de entrada; parcial y público para las pruebas de integración de la API.</summary>
public partial class Program;
