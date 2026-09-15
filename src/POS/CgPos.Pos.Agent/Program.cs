using CgPos.Pos.Agent.Salud;
using CgPos.Pos.Infrastructure;
using CgPos.Pos.Infrastructure.Persistencia;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting.WindowsServices;
using Serilog;

// Logger mínimo para registrar fallos antes de que se lea la configuración.
Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(new WebApplicationOptions
    {
        Args = args,
        // Como servicio de Windows el directorio actual es System32: se usa la carpeta del ejecutable.
        ContentRootPath = WindowsServiceHelpers.IsWindowsService() ? AppContext.BaseDirectory : default,
    });

    builder.Host.UseWindowsService(opciones => opciones.ServiceName = "CgPosAgent");

    builder.Services.AddSerilog((services, configuracion) => configuracion
        .ReadFrom.Configuration(builder.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

    builder.Services.AddPosInfraestructura(builder.Configuration);

    builder.Services.AddHealthChecks()
        .AddDbContextCheck<PosDbContext>("base-datos");

    var app = builder.Build();

    if (app.Environment.IsDevelopment())
        app.UseWebAssemblyDebugging();

    app.MapHealthChecks("/salud", new HealthCheckOptions { ResponseWriter = EscritorSalud.EscribirAsync });

    // Pantallas de la caja (Blazor Wasm de CgPos.Pos.Web), servidas localmente.
    app.MapStaticAssets();
    app.MapFallbackToFile("index.html");

    await app.Services.InicializarBaseDatosPosAsync();

    await app.RunAsync();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    // Se relanza para que el proceso termine con error y Windows reinicie el servicio.
    Log.Fatal(ex, "CG-POS Agent terminó por un error no controlado");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}
