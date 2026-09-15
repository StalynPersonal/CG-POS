using System.Text.Json.Serialization;
using CgPos.Central.Api.Api;
using CgPos.Central.Api.Seguridad;
using CgPos.Central.Infraestructura;
using CgPos.Central.Infraestructura.CargaInicial;
using CgPos.Central.Infraestructura.Persistencia;
using CgPos.Dominio.Organizacion;
using Microsoft.Extensions.Hosting.WindowsServices;
using Serilog;

// Logger mínimo para registrar fallos antes de que se lea la configuración.
Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateBootstrapLogger();

try
{
    var constructor = WebApplication.CreateBuilder(new WebApplicationOptions
    {
        Args = args,
        ContentRootPath = WindowsServiceHelpers.IsWindowsService() ? AppContext.BaseDirectory : default,
    });

    constructor.Host.UseWindowsService(opciones => opciones.ServiceName = "CgPosCentral");

    constructor.Services.AddSerilog((servicios, configuracion) => configuracion
        .ReadFrom.Configuration(constructor.Configuration)
        .ReadFrom.Services(servicios)
        .Enrich.FromLogContext());

    constructor.ConfigurarTlsCentral();

    // Mismo formato JSON que CgPos.Contratos.Serializacion.OpcionesJson (enums como texto, sin nulos).
    constructor.Services.ConfigureHttpJsonOptions(opciones =>
    {
        opciones.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        opciones.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

    constructor.Services.AgregarInfraestructuraCentral(constructor.Configuration);
    constructor.Services.AgregarSeguridadCentral();

    constructor.Services.AddHealthChecks()
        .AddDbContextCheck<ContextoDatosCentral>("base-datos");

    var aplicacion = constructor.Build();

    // La clave de firma se valida al arrancar y no con la primera solicitud.
    _ = aplicacion.Services.GetRequiredService<EmisorTokensCentral>();

    aplicacion.UsarHttpsObligatorio();

    // Una regla de negocio sin configurar no se reemplaza por un valor fijo: la operación se rechaza con el motivo (422, texto).
    aplicacion.Use(async (contexto, siguiente) =>
    {
        try
        {
            await siguiente(contexto);
        }
        catch (ParametroNoConfiguradoExcepcion excepcion) when (!contexto.Response.HasStarted)
        {
            Log.Warning("Operación rechazada por configuración: {Mensaje}", excepcion.Message);
            contexto.Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
            contexto.Response.ContentType = "text/plain; charset=utf-8";
            await contexto.Response.WriteAsync(excepcion.Message);
        }
    });

    aplicacion.UseAuthentication();
    aplicacion.UseAuthorization();

    aplicacion.MapHealthChecks("/salud");
    aplicacion.MapearApiSesion();
    aplicacion.MapearApiDispositivos();

    await aplicacion.Services.InicializarBaseDatosCentralAsync();

    // Datos iniciales desde archivo (instalación o desarrollo). Es idempotente.
    if (aplicacion.Configuration["CargaInicial:Archivo"] is { Length: > 0 } archivoCarga)
        await aplicacion.Services.AplicarCargaInicialCentralAsync(Path.GetFullPath(archivoCarga, aplicacion.Environment.ContentRootPath));

    await aplicacion.RunAsync();
}
catch (Exception excepcion) when (excepcion is not HostAbortedException)
{
    Log.Fatal(excepcion, "CG-POS Central terminó por un error no controlado");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}

/// <summary>Punto de entrada; parcial y público para las pruebas de integración de la API.</summary>
public partial class Program;
