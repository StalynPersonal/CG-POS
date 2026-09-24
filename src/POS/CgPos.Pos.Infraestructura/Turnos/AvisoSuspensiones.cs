using CgPos.Contratos.Sincronizacion;
using CgPos.Dominio.Turnos;
using CgPos.Pos.Aplicacion.Abstracciones;

namespace CgPos.Pos.Infraestructura.Turnos;

/// <summary>
/// Sube al Central el rato que la caja estuvo parada. Se manda una sola vez, cuando ya se cerró: al reanudar o al cerrar el
/// turno si nadie volvió. Mientras sigue abierto no se informa, porque al Central le sirve el tiempo completo.
/// </summary>
internal static class AvisoSuspensiones
{
    public static void Encolar(IBandejaSalida bandejaSalida, SuspensionCaja suspension, DateOnly fechaOperacion)
    {
        if (suspension.ReanudadaEn is not { } reanudada)
            return;

        // La referencia lleva el turno y el número de la suspensión en esa caja: un reenvío actualiza la fila, no la duplica.
        bandejaSalida.Encolar(TiposMensaje.SuspensionCaja, $"{suspension.TurnoNumero}-{suspension.Id}",
            new DocumentoSuspensionCaja(suspension.Id, suspension.TurnoNumero, fechaOperacion, suspension.UsuarioNombre,
                suspension.MotivoCodigo, suspension.MotivoNombre, suspension.Programado, suspension.Nota,
                suspension.SuspendidaEn, reanudada, suspension.CerradaPorCierreDeTurno));
    }
}
