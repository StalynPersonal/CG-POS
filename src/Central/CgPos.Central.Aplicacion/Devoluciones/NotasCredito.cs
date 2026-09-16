using CgPos.Central.Aplicacion.Abstracciones;
using CgPos.Central.Aplicacion.Seguridad;
using CgPos.Contratos.Central;
using CgPos.Dominio.Devoluciones;

namespace CgPos.Central.Aplicacion.Devoluciones;

/// <summary>
/// Notas de crédito de todas las sucursales (RF-38, RF-43). El Central conoce su saldo, retiene el monto mientras una caja cobra
/// y deja consumirlas donde el cliente vuelva, no solo donde se emitieron.
/// </summary>
public interface IServicioNotasCreditoCentral
{
    public const int TamanoMaximoPagina = 100;

    /// <param name="codigo">e-NCF o número de la nota de crédito.</param>
    Task<DatosNotaCreditoCentral?> BuscarAsync(string codigo, CancellationToken cancelacion = default);

    /// <summary>Retiene saldo para esa caja hasta que confirme el consumo o venza la reserva.</summary>
    Task<RespuestaReservaNotaCredito> ReservarAsync(Guid notaCreditoId, Guid cajaId, decimal monto, CancellationToken cancelacion = default);

    /// <returns><c>false</c> si la reserva no existe, ya se cerró o es de otra caja.</returns>
    Task<bool> LiberarReservaAsync(Guid reservaId, Guid cajaId, CancellationToken cancelacion = default);

    Task<PaginaNotasCreditoCentral> ListarAsync(string? buscar, EstadoNotaCreditoCentral? estado, bool soloSobregiradas, int pagina, int tamano,
        CancellationToken cancelacion = default);

    Task<IReadOnlyList<DatosMovimientoNotaCredito>> ListarMovimientosAsync(Guid notaCreditoId, CancellationToken cancelacion = default);

    /// <summary>Habilita una nota vencida hasta una fecha nueva, dentro del máximo configurado (RF-40).</summary>
    Task<ResultadoAdministracion> ProrrogarAsync(Guid notaCreditoId, DateOnly venceEn, string motivo, UsuarioAuditoria actor, CancellationToken cancelacion = default);
}
