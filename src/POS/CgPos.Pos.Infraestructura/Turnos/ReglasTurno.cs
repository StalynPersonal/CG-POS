using CgPos.Dominio.Comun;
using CgPos.Dominio.Turnos;
using CgPos.Pos.Aplicacion.Organizacion;

namespace CgPos.Pos.Infraestructura.Turnos;

/// <summary>Reglas del turno que comparten la venta, la pantalla y el cierre.</summary>
internal static class ReglasTurno
{
    /// <summary>
    /// Con un turno abierto de un día anterior la caja no vende ni cobra hasta cerrarlo, si el parámetro lo pide. Devuelve el
    /// aviso para el cajero, o nulo si el turno es de hoy o el negocio apagó la regla.
    /// </summary>
    public static async Task<string?> BloqueoDiaAnteriorAsync(Turno turno, IParametros parametros, TimeProvider reloj, CancellationToken cancelacion)
    {
        if (!turno.EsDeDiaAnterior(reloj.Ahora().Dia()))
            return null;
        if (!await parametros.ObtenerBooleanoAsync(ClavesParametros.BloquearVentaTurnoDiaAnterior, turno.CajaId, cancelacion))
            return null;

        return $"El turno {turno.Numero} es del {turno.FechaOperacion:dd/MM/yyyy}: la caja no vende ni cobra con un turno de un día anterior. "
               + "Ciérrelo y abra uno nuevo.";
    }
}
