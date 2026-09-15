namespace CgPos.Contratos.Pantallas;

/// <summary>Canal en tiempo real entre el Agente y la pantalla del cliente (segundo monitor, RF-115).</summary>
public static class ContratoPantallaCliente
{
    public const string RutaHub = "/hubs/pantalla-cliente";

    /// <summary>El Agente envía la venta actual (o nulo si no hay) cada vez que cambia.</summary>
    public const string MetodoVentaActualizada = "VentaActualizada";

    public const string RutaImagenes = "/publicidad";
}

/// <param name="Imagenes">Rutas relativas de las imágenes del carrusel, en orden.</param>
public sealed record DatosPublicidad(
    IReadOnlyList<string> Imagenes,
    int SegundosPorImagen,
    string? MensajeBienvenida,
    string? EmpresaNombre);
