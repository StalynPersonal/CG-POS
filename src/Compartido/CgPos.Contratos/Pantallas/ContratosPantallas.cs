namespace CgPos.Contratos.Pantallas;

/// <summary>Canal en tiempo real entre el Agente y la pantalla del cliente (segundo monitor, RF-115).</summary>
public static class ContratoPantallaCliente
{
    public const string RutaHub = "/hubs/pantalla-cliente";

    /// <summary>El Agente envía la venta actual (o nulo si no hay) cada vez que cambia.</summary>
    public const string MetodoVentaActualizada = "VentaActualizada";

    /// <summary>El Agente avisa de que la publicidad cambió; la pantalla la vuelve a pedir.</summary>
    public const string MetodoPublicidadActualizada = "PublicidadActualizada";

    public const string RutaImagenes = "/publicidad";
}

/// <param name="Imagenes">Rutas relativas de las imágenes del carrusel, en orden.</param>
/// <param name="SegundosPorImagen">Nulo si el negocio no lo configuró: la publicidad no rota.</param>
/// <param name="MensajeBienvenida">Nulo si no está configurado: no se muestra.</param>
/// <param name="MensajeDespedida">Mensaje al cobrar; nulo si no está configurado.</param>
public sealed record DatosPublicidad(
    IReadOnlyList<string> Imagenes,
    int? SegundosPorImagen,
    string? MensajeBienvenida,
    string? EmpresaNombre,
    string? MensajeDespedida = null);
