using CgPos.Contratos.Pantallas;
using CgPos.Contratos.Ventas;
using CgPos.Pos.Aplicacion.Organizacion;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;

namespace CgPos.Pos.Agente.Pantallas;

/// <summary>
/// Hub de la pantalla del cliente. No exige sesión: el Agente solo escucha en localhost y la pantalla del
/// segundo monitor no tiene usuario. Solo difunde lo que el cliente ya ve en la caja.
/// </summary>
public sealed class HubPantallaCliente(PublicadorPantallaCliente publicador) : Hub
{
    public override async Task OnConnectedAsync()
    {
        // Una pantalla recién abierta (o reconectada) recibe de inmediato el estado actual.
        await Clients.Caller.SendAsync(ContratoPantallaCliente.MetodoVentaActualizada, publicador.Ultima);
        await base.OnConnectedAsync();
    }
}

/// <summary>Guarda la última venta publicada y la envía a todas las pantallas del cliente conectadas.</summary>
public sealed class PublicadorPantallaCliente(IHubContext<HubPantallaCliente> hub)
{
    private DatosVenta? _ultima;

    public DatosVenta? Ultima => Volatile.Read(ref _ultima);

    public Task PublicarAsync(DatosVenta? venta, CancellationToken cancelacion = default)
    {
        Volatile.Write(ref _ultima, venta);
        return hub.Clients.All.SendAsync(ContratoPantallaCliente.MetodoVentaActualizada, venta, cancelacion);
    }
}

/// <summary>Después de cada operación de venta publica la venta resultante en la pantalla del cliente.</summary>
public sealed class FiltroPublicarVenta(PublicadorPantallaCliente publicador, ILogger<FiltroPublicarVenta> registro) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext contexto, EndpointFilterDelegate siguiente)
    {
        var resultado = await siguiente(contexto);

        if (resultado is IValueHttpResult { Value: RespuestaVenta { Venta: { } venta } })
        {
            try
            {
                await publicador.PublicarAsync(venta, contexto.HttpContext.RequestAborted);
            }
            catch (Exception excepcion) when (excepcion is not OperationCanceledException)
            {
                // La pantalla del cliente es informativa: un fallo al publicar nunca afecta la venta.
                registro.LogWarning(excepcion, "No se pudo actualizar la pantalla del cliente");
            }
        }

        return resultado;
    }
}

public static class RutasPantallaCliente
{
    /// <summary>Lo que la pantalla del cliente sabe mostrar; lo demás que haya en la carpeta se ignora.</summary>
    internal static readonly string[] ExtensionesImagen = [".png", ".jpg", ".jpeg", ".webp", ".svg"];

    /// <summary>Dónde se dejan las imágenes si no se configura otra carpeta.</summary>
    internal const string CarpetaPublicidadPredeterminada = @"C:\CGPOS\Publicidad";

    /// <summary>Clave de appsettings con la carpeta de las imágenes.</summary>
    internal const string ClaveCarpetaPublicidad = "Pantallas:CarpetaPublicidad";

    /// <summary>Carpeta de las imágenes de publicidad, ya resuelta a ruta completa. La usan la pantalla y el diagnóstico.</summary>
    internal static string CarpetaPublicidad(IConfiguration configuracion, string raizContenido) =>
        Path.GetFullPath(configuracion[ClaveCarpetaPublicidad] is { Length: > 0 } ruta ? ruta : CarpetaPublicidadPredeterminada, raizContenido);

    /// <summary>Las imágenes que hay ahora mismo en la carpeta, en el orden en que se muestran.</summary>
    internal static IReadOnlyList<string> ImagenesDe(string carpeta) =>
        Directory.Exists(carpeta)
            ? [.. Directory.EnumerateFiles(carpeta)
                .Where(ruta => ExtensionesImagen.Contains(Path.GetExtension(ruta), StringComparer.OrdinalIgnoreCase))
                .Select(Path.GetFileName)
                .Order(StringComparer.OrdinalIgnoreCase)
                .Select(nombre => nombre!)]
            : [];

    public static WebApplication MapearPantallaCliente(this WebApplication aplicacion)
    {
        aplicacion.MapHub<HubPantallaCliente>(ContratoPantallaCliente.RutaHub);

        // La carpeta se crea si no existe: instalar una caja no debe depender de que alguien se acuerde de crearla, ni del
        // orden en que lo haga. Si no se puede crear (ruta de red caída, sin permisos), la caja arranca igual y solo se
        // queda sin publicidad: la pantalla del cliente sigue mostrando la venta, que es lo que no puede faltar.
        var carpeta = CarpetaPublicidad(aplicacion.Configuration, aplicacion.Environment.ContentRootPath);
        try
        {
            Directory.CreateDirectory(carpeta);
        }
        catch (Exception excepcion) when (excepcion is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            aplicacion.Logger.LogWarning(excepcion, "No se pudo preparar la carpeta de publicidad {Carpeta}", carpeta);
        }

        if (Directory.Exists(carpeta))
        {
            aplicacion.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = new PhysicalFileProvider(carpeta),
                RequestPath = ContratoPantallaCliente.RutaImagenes,
            });
        }

        // Público, como el hub: la pantalla del cliente no inicia sesión.
        aplicacion.MapGet("/api/pantallas/publicidad", async (IEstadoCaja estadoCaja, IParametros parametros, IContextoCaja contextoCaja,
            CancellationToken cancelacion) =>
        {
            var imagenes = ImagenesDe(carpeta)
                .Select(nombre => $"{ContratoPantallaCliente.RutaImagenes}/{Uri.EscapeDataString(nombre)}")
                .ToList();

            // Los textos y el tiempo de la publicidad los configura el negocio; si no están, la pantalla no los muestra.
            var estado = await estadoCaja.ObtenerAsync(cancelacion);
            var cajaId = contextoCaja.CajaId;
            var segundos = (int?)await parametros.ObtenerDecimalOpcionalAsync(ClavesParametros.SegundosPorImagenPantalla, cajaId, cancelacion);

            return Results.Ok(new DatosPublicidad(
                imagenes,
                segundos is > 0 ? segundos : null,
                await parametros.ObtenerAsync(ClavesParametros.MensajeBienvenidaPantalla, cajaId, cancelacion),
                estado.EmpresaNombre,
                await parametros.ObtenerAsync(ClavesParametros.MensajeDespedidaPantalla, cajaId, cancelacion)));
        });

        return aplicacion;
    }


}
