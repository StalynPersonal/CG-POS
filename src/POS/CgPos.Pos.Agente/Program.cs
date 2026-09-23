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
    // El mismo publicador es quien avisa a las pantallas de que su publicidad cambió.
    constructor.Services.AddSingleton<IAvisosPantallaCliente>(s => s.GetRequiredService<PublicadorPantallaCliente>());

    // /salud es el diagnóstico de la caja: dice qué le falta y qué hacer, para revisarla antes de ponerla a vender.
    constructor.Services.AddHealthChecks()
        .AddDbContextCheck<ContextoDatosPos>("Base de datos")
                .AddCheck<VerificacionesCaja.Configuracion>("Configuración")
        .AddCheck<VerificacionesCaja.Central>("Central")
        .AddCheck<VerificacionesCaja.Certificado>("Certificado e-CF")
        .AddCheck<VerificacionesCaja.Perifericos>("Periféricos")
        .AddCheck<VerificacionesCaja.Publicidad>("Publicidad");

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

    // El enrutamiento va después de servir los archivos y antes de autenticar, que es su sitio en una aplicación
    // WebAssembly hospedada. Si se pone antes, el fallback que entrega el index.html queda elegido desde el principio
    // y una petición a un archivo inexistente de /_framework muere dentro de esa rama, sin que nadie ejecute el
    // endpoint ya elegido: ahí sale «The request reached the end of the pipeline». Después de los archivos, esa
    // petición termina en un 404 normal, que es lo que corresponde.
    aplicacion.UseRouting();

    aplicacion.UseAuthentication();
    aplicacion.UseAuthorization();

    aplicacion.MapHealthChecks("/salud", new HealthCheckOptions { ResponseWriter = EscritorSalud.EscribirAsync });
    aplicacion.MapearApiConfiguracion();
    aplicacion.MapearApiSeguridad();
    aplicacion.MapearApiCatalogo();
    aplicacion.MapearApiVentas();
    aplicacion.MapearApiEcf();
    aplicacion.MapearApiCaja();
    aplicacion.MapearApiDevoluciones();
    aplicacion.MapearApiFidelidad();
    aplicacion.MapearPantallaCliente();

    // Cualquier ruta de las pantallas (cajero, cliente, devoluciones) la resuelve la propia aplicación;
    // las rutas /api desconocidas dan 404 y no el index.html, que confundiría a quien consume la API.
    if (servirPantallas)
    {
        aplicacion.MapFallbackToFile("/", "index.html");
        aplicacion.MapFallbackToFile("{*ruta:nonfile:regex(^(?!api/).*$)}", "index.html");
    }

    await aplicacion.Services.InicializarBaseDatosPosAsync();

    // La caja toma sus datos del Central; su base se crea con el script de estructura y arranca vacía.
    // Los archivos de carga solo sirven para desarrollo o pruebas: fuera de ahí se ignoran.
    var datosDesdeArchivo = aplicacion.Environment.IsDevelopment() || aplicacion.Environment.IsEnvironment("Pruebas");

    string? RutaConfigurada(string clave)
    {
        if (aplicacion.Configuration[clave] is not { Length: > 0 } ruta)
            return null;

        if (!datosDesdeArchivo)
        {
            Log.Warning("Se ignora {Clave}: la caja toma sus datos del Central, no de archivos", clave);
            return null;
        }

        return Path.GetFullPath(ruta, aplicacion.Environment.ContentRootPath);
    }

    // Conectada al Central, la organización y los maestros vienen de él: los archivos no se aplican para no pisar lo que publicó.
    var conCentral = aplicacion.Configuration["Central:Url"] is { Length: > 0 };
    if (conCentral && (RutaConfigurada("CargaInicial:Archivo") ?? RutaConfigurada("Maestros:Archivo")) is not null)
        Log.Information("La caja está conectada al Central: se omiten los archivos de carga inicial y de maestros");

    if (!conCentral && RutaConfigurada("CargaInicial:Archivo") is { } archivoCargaInicial)
        await aplicacion.Services.AplicarArchivoSiCambioAsync("CargaInicial:Archivo", archivoCargaInicial,
            (servicios, ruta, cancelacion) => servicios.AplicarCargaInicialAsync(ruta, cancelacion));

    if (!conCentral && RutaConfigurada("Maestros:Archivo") is { } archivoMaestros)
        await aplicacion.Services.AplicarArchivoSiCambioAsync("Maestros:Archivo", archivoMaestros,
            (servicios, ruta, cancelacion) => CgPos.Pos.Infraestructura.Catalogo.ExtensionesCatalogo.AplicarMaestrosAsync(servicios, ruta, cancelacion));

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
