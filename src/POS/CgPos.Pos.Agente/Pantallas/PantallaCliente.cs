using CgPos.Contratos.Pantallas;
using CgPos.Contratos.Ventas;
using CgPos.Pos.Aplicacion.Organizacion;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.FileProviders;

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
    private static readonly string[] ExtensionesImagen = [".png", ".jpg", ".jpeg", ".webp", ".svg"];

    public static WebApplication MapearPantallaCliente(this WebApplication aplicacion)
    {
        aplicacion.MapHub<HubPantallaCliente>(ContratoPantallaCliente.RutaHub);

        var carpeta = CarpetaPublicidad(aplicacion);
        if (Directory.Exists(carpeta))
        {
            aplicacion.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = new PhysicalFileProvider(carpeta),
                RequestPath = ContratoPantallaCliente.RutaImagenes,
            });
        }

        // Público, como el hub: la pantalla del cliente no inicia sesión.
        aplicacion.MapGet("/api/pantallas/publicidad", async (IEstadoCaja estadoCaja, CancellationToken cancelacion) =>
        {
            var imagenes = Directory.Exists(carpeta)
                ? Directory.EnumerateFiles(carpeta)
                    .Where(ruta => ExtensionesImagen.Contains(Path.GetExtension(ruta), StringComparer.OrdinalIgnoreCase))
                    .Select(Path.GetFileName)
                    .Order(StringComparer.OrdinalIgnoreCase)
                    .Select(nombre => $"{ContratoPantallaCliente.RutaImagenes}/{Uri.EscapeDataString(nombre!)}")
                    .ToList()
                : [];

            var estado = await estadoCaja.ObtenerAsync(cancelacion);
            var segundos = int.TryParse(aplicacion.Configuration["Pantallas:SegundosPorImagen"], out var valor) && valor > 0 ? valor : 8;

            return Results.Ok(new DatosPublicidad(imagenes, segundos, aplicacion.Configuration["Pantallas:MensajeBienvenida"] ?? "¡Bienvenido!", estado.EmpresaNombre));
        });

        return aplicacion;
    }

    private static string CarpetaPublicidad(WebApplication aplicacion) =>
        Path.GetFullPath(aplicacion.Configuration["Pantallas:CarpetaPublicidad"] is { Length: > 0 } ruta ? ruta : @"C:\CGPOS\Publicidad",
            aplicacion.Environment.ContentRootPath);
}
