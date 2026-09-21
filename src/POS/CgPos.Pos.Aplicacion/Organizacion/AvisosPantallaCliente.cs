namespace CgPos.Pos.Aplicacion.Organizacion;

/// <summary>
/// Avisa a la pantalla del cliente (el segundo monitor) de que algo suyo cambió.
///
/// Quien aplica los maestros no sabe nada de pantallas ni de SignalR; solo dice que la publicidad cambió y el Agente
/// se encarga de avisarle a las pantallas conectadas. En las pruebas no hay pantallas y el aviso no hace nada.
/// </summary>
public interface IAvisosPantallaCliente
{
    /// <summary>Cambió algo de la publicidad: los segundos por imagen, los mensajes o las imágenes.</summary>
    Task PublicidadCambiadaAsync(CancellationToken cancelacion = default);
}

/// <summary>Sin pantallas conectadas no hay a quién avisar: es lo que se usa fuera del Agente.</summary>
public sealed class AvisosPantallaClienteInactivos : IAvisosPantallaCliente
{
    public Task PublicidadCambiadaAsync(CancellationToken cancelacion = default) => Task.CompletedTask;
}
