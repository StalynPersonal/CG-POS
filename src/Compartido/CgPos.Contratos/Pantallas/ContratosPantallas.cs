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

/// <summary>Una pieza de la publicidad: una imagen o un video de la carpeta del equipo.</summary>
/// <param name="Ruta">Dirección con la que la pantalla la pide al Agente.</param>
/// <param name="EsVideo">El video manda su duración: se reproduce entero y al terminar pasa al siguiente.</param>
public sealed record MedioPublicidad(string Ruta, bool EsVideo);

/// <param name="Medios">Imágenes y videos del carrusel, en orden.</param>
/// <param name="SegundosPorImagen">Cuánto dura cada imagen; nulo si el negocio no lo configuró y la publicidad no rota.</param>
/// <param name="MensajeBienvenida">Nulo si no está configurado: no se muestra.</param>
/// <param name="MensajeDespedida">Mensaje al cobrar; nulo si no está configurado.</param>
public sealed record DatosPublicidad(
    IReadOnlyList<MedioPublicidad> Medios,
    int? SegundosPorImagen,
    string? MensajeBienvenida,
    string? EmpresaNombre,
    string? MensajeDespedida = null);
