using System.Text.Json.Serialization;
using CgPos.Dominio.Organizacion;
using CgPos.Pos.Agente.Api;
using CgPos.Pos.Agente.Pantallas;
using CgPos.Pos.Agente.Salud;
using CgPos.Pos.Agente.Seguridad;
using CgPos.Pos.Aplicacion.Organizacion;
using CgPos.Pos.ECF;
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
    constructor.Services.AgregarEcfPos(constructor.Configuration);
    constructor.Services.AgregarSeguridadAgente();

    // Envío de la bandeja de salida al Central en segundo plano (M14).
    constructor.Services.AddHostedService<CgPos.Pos.Agente.Sincronizacion.TrabajadorSincronizacion>();

    // Respaldo diario, purga controlada y verificación de la hora.
    constructor.Services.AddHostedService<CgPos.Pos.Agente.Sincronizacion.TrabajadorMantenimiento>();

    // Pantalla del cliente en tiempo real (segundo monitor).
    constructor.Services.AddSignalR().AddJsonProtocol(opciones =>
    {
        opciones.PayloadSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        opciones.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });
    constructor.Services.AddSingleton<PublicadorPantallaCliente>();

    constructor.Services.AddHealthChecks()
        .AddDbContextCheck<ContextoDatosPos>("base-datos");

    var aplicacion = constructor.Build();

    if (aplicacion.Environment.IsDevelopment())
        aplicacion.UseWebAssemblyDebugging();

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

    // Pantallas de la caja (Blazor WebAssembly de CgPos.Pos.Web): sus archivos son públicos y se sirven antes de autenticar.
    var servirPantallas = aplicacion.Configuration.GetValue("Agente:ServirPantallas", true);
    if (servirPantallas)
    {
        aplicacion.UseBlazorFrameworkFiles();
        aplicacion.UseStaticFiles();
    }

    aplicacion.UseAuthentication();
    aplicacion.UseAuthorization();

    aplicacion.MapHealthChecks("/salud", new HealthCheckOptions { ResponseWriter = EscritorSalud.EscribirAsync });
    aplicacion.MapearApiSeguridad();
    aplicacion.MapearApiCatalogo();
    aplicacion.MapearApiVentas();
    aplicacion.MapearApiEcf();
    aplicacion.MapearApiCaja();
    aplicacion.MapearApiDevoluciones();
    aplicacion.MapearApiFidelidad();
    aplicacion.MapearApiDespacho();
    aplicacion.MapearPantallaCliente();

    // Cualquier ruta de las pantallas (cajero, cliente, devoluciones, despacho) la resuelve la propia aplicación.
    if (servirPantallas)
        aplicacion.MapFallbackToFile("index.html");

    await aplicacion.Services.InicializarBaseDatosPosAsync();

    // Datos desde archivo (desarrollo o instalación sin Central todavía). Cada archivo se aplica solo si cambió desde la última vez.
    string? RutaConfigurada(string clave) =>
        aplicacion.Configuration[clave] is { Length: > 0 } ruta ? Path.GetFullPath(ruta, aplicacion.Environment.ContentRootPath) : null;

    if (RutaConfigurada("CargaInicial:Archivo") is { } archivoCargaInicial)
        await aplicacion.Services.AplicarArchivoSiCambioAsync("CargaInicial:Archivo", archivoCargaInicial,
            (servicios, ruta, cancelacion) => servicios.AplicarCargaInicialAsync(ruta, cancelacion));

    if (RutaConfigurada("Maestros:Archivo") is { } archivoMaestros)
        await aplicacion.Services.AplicarArchivoSiCambioAsync("Maestros:Archivo", archivoMaestros,
            (servicios, ruta, cancelacion) => CgPos.Pos.Infraestructura.Catalogo.ExtensionesCatalogo.AplicarMaestrosAsync(servicios, ruta, cancelacion));

    if (RutaConfigurada("Maestros:PadronDgii") is { } archivoPadron)
        await aplicacion.Services.AplicarArchivoSiCambioAsync("Maestros:PadronDgii", archivoPadron, async (servicios, ruta, cancelacion) =>
        {
            var padron = await CgPos.Pos.Infraestructura.Catalogo.ExtensionesCatalogo.ImportarPadronDgiiAsync(servicios, ruta, cancelacion);
            Log.Information("Padrón DGII importado: {Validos} registros válidos de {Leidas} líneas", padron.RegistrosValidos, padron.LineasLeidas);
        });

    // Solo desarrollo: certificado autofirmado cargado con un PIN de configuración. En producción el PIN lo digita un usuario.
    if (aplicacion.Configuration[CgPos.Pos.Aplicacion.Ecf.ClavesEcf.PinDesarrollo] is { Length: > 0 } pinDesarrollo)
        CgPos.Pos.ECF.ExtensionesEcf.PrepararCertificadoDesarrollo(aplicacion.Services, aplicacion.Configuration, pinDesarrollo);

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
