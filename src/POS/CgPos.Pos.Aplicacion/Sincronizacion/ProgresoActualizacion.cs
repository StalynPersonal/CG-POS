using CgPos.Contratos.Seguridad;

namespace CgPos.Pos.Aplicacion.Sincronizacion;

/// <summary>
/// En qué anda la sincronización ahora mismo, para poder decírselo al cajero.
///
/// Una caja recién configurada tarda en tener sus datos: mientras bajan y se aplican, la pantalla decía que la caja no
/// existe, que es lo que se ve cuando algo está mal de verdad. Con esto la pantalla distingue «todavía está bajando» de
/// «esto no va a funcionar solo», que es lo que el cajero necesita saber para esperar o llamar a soporte.
///
/// Vive en memoria y es de todo el Agente: si el servicio se reinicia, la sincronización vuelve a empezar de todos modos.
/// </summary>
public interface IProgresoActualizacion
{
    /// <summary>Lo que está pasando; nulo significa que ahora mismo no se está actualizando nada.</summary>
    DatosActualizacionCaja? Actual { get; }

    /// <summary>Empieza (o cambia de etapa) una actualización. Devuelve algo que al soltarse la da por terminada.</summary>
    IDisposable Comenzar(string etapa);

    /// <summary>Cambia el texto de lo que se está haciendo, sin abrir otra actualización.</summary>
    void Etapa(string etapa);

    /// <summary>Cuántos elementos lleva la etapa, para que en pantalla se vea un número moviéndose.</summary>
    void Avance(int hechos, int total);

    /// <summary>Lo que dejó la última actualización: nulo si terminó bien.</summary>
    void Terminar(string? error);
}
