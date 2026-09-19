using System.Text.Json.Serialization;
using CgPos.Central.Api.Api;
using CgPos.Central.Api.Salud;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using CgPos.Central.Api.Seguridad;
using CgPos.Central.ECF;
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
    constructor.Services.AgregarEcfCentral(constructor.Configuration);
    constructor.Services.AgregarSeguridadCentral();

    // La bajada de maestros de una caja nueva puede ser grande: se comprime (solo esa ruta, sin tokens ni secretos en la respuesta).
    constructor.Services.AddResponseCompression(opciones =>
    {
        opciones.EnableForHttps = true;
        opciones.MimeTypes = ["application/json"];
    });

    constructor.Services.AddHealthChecks()
        .AddDbContextCheck<ContextoDatosCentral>("Base de datos");

    var aplicacion = constructor.Build();

    // La clave de firma se valida al arrancar y no con la primera solicitud.
    _ = aplicacion.Services.GetRequiredService<EmisorTokensCentral>();

    if (aplicacion.Environment.IsDevelopment())
        aplicacion.UseWebAssemblyDebugging();

    // Encabezados básicos del Central Manager: sin incrustarlo en otros sitios ni adivinar tipos de contenido.
    aplicacion.Use(async (contexto, siguiente) =>
    {
        contexto.Response.Headers.XContentTypeOptions = "nosniff";
        contexto.Response.Headers.XFrameOptions = "DENY";
        contexto.Response.Headers["Referrer-Policy"] = "no-referrer";
        await siguiente(contexto);
    });

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

    aplicacion.UseWhen(contexto => contexto.Request.Path.StartsWithSegments("/api/sincronizacion/maestros"), rama => rama.UseResponseCompression());

    // Central Manager (Blazor WebAssembly de CgPos.Central.Web): sus archivos son públicos y se sirven antes de autenticar.
    var servirManager = aplicacion.Configuration.GetValue("Central:ServirManager", true);
    if (servirManager)
    {
        aplicacion.UseBlazorFrameworkFiles();
        aplicacion.UseStaticFiles();
    }

    // El enrutamiento va después de servir los archivos y antes de autenticar, que es su sitio en una aplicación
    // WebAssembly hospedada. Si se pone antes, el fallback que entrega el index.html queda elegido desde el principio
    // y una petición a un archivo inexistente de /_framework muere dentro de esa rama, sin que nadie ejecute el
    // endpoint ya elegido: ahí sale «The request reached the end of the pipeline». Después de los archivos, esa
    // petición termina en un 404 normal, que es lo que corresponde.
    aplicacion.UseRouting();

    aplicacion.UseAuthentication();
    aplicacion.UseAuthorization();

    aplicacion.MapHealthChecks("/salud", new HealthCheckOptions { ResponseWriter = EscritorSaludCentral.EscribirAsync });
    aplicacion.MapearApiSesion();
    aplicacion.MapearApiDispositivos();
    aplicacion.MapearApiSincronizacion();
    aplicacion.MapearApiAdministracionSeguridad();
    aplicacion.MapearApiOrganizacion();
    aplicacion.MapearApiConfiguracionCajas();
    aplicacion.MapearApiMaestros();
    aplicacion.MapearApiPromociones();
    aplicacion.MapearApiMonitor();
    aplicacion.MapearApiNotasCredito();
    aplicacion.MapearApiListasBoda();
    aplicacion.MapearApiComprobantesRecibidos();
    aplicacion.MapearApiAuditoria();
    aplicacion.MapearApiChequeador();
    aplicacion.MapearApiCierresSucursal();
    aplicacion.MapearApiFidelidad();
    aplicacion.MapearApiDespacho();
    aplicacion.MapearApiReportes();
    aplicacion.MapearApiActualizaciones();

    // La aplicación responde en la raíz y en sus rutas; las rutas /api desconocidas dan 404, no el index.html.
    if (servirManager)
    {
        aplicacion.MapFallbackToFile("/", "index.html");
        aplicacion.MapFallbackToFile("{*ruta:nonfile:regex(^(?!api/).*$)}", "index.html");
    }

    await aplicacion.Services.InicializarBaseDatosCentralAsync();

    // Los datos del Central viven en su base: se crean con el script de estructura y se administran desde el Manager.
    // Los archivos de carga solo sirven para armar un entorno de desarrollo o de pruebas; en producción se ignoran.
    var datosDesdeArchivo = aplicacion.Environment.IsDevelopment() || aplicacion.Environment.IsEnvironment("Pruebas");

    string? RutaConfigurada(string clave)
    {
        if (aplicacion.Configuration[clave] is not { Length: > 0 } ruta)
            return null;

        if (!datosDesdeArchivo)
        {
            Log.Warning("Se ignora {Clave}: los datos del Central se administran en su base, no por archivo", clave);
            return null;
        }

        return Path.GetFullPath(ruta, aplicacion.Environment.ContentRootPath);
    }

    // En producción estas rutas se ignoran (arriba se avisa y se devuelve null): solo las usan desarrollo y las pruebas.
    if (RutaConfigurada("CargaInicial:Archivo") is { } archivoCarga)
        await aplicacion.Services.AplicarArchivoSiCambioAsync("CargaInicial:Archivo", archivoCarga,
            (servicios, ruta, cancelacion) => servicios.AplicarCargaInicialCentralAsync(ruta, cancelacion));

    // Seguridad, parámetros y maestros para las cajas, con el formato de la caja.
    if (RutaConfigurada("CargaInicialCajas:Archivo") is { } archivoCajas)
        await aplicacion.Services.AplicarArchivoSiCambioAsync("CargaInicialCajas:Archivo", archivoCajas,
            CgPos.Central.Infraestructura.Sincronizacion.ExtensionesPublicacionMaestros.PublicarSeguridadCajasDesdeArchivoAsync);

    if (RutaConfigurada("Maestros:Archivo") is { } archivoMaestros)
        await aplicacion.Services.AplicarArchivoSiCambioAsync("Maestros:Archivo", archivoMaestros,
            CgPos.Central.Infraestructura.Sincronizacion.ExtensionesPublicacionMaestros.PublicarMaestrosDesdeArchivoAsync);

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
