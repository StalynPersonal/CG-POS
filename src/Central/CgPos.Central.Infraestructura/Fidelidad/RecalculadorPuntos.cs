using CgPos.Central.Infraestructura.Persistencia;
using CgPos.Central.Infraestructura.Persistencia.Configuraciones;
using CgPos.Dominio.Fidelidad;
using Microsoft.EntityFrameworkCore;

namespace CgPos.Central.Infraestructura.Fidelidad;

/// <summary>
/// Recalcula el saldo de puntos de un miembro con todos sus movimientos y lo publica en su maestro, que es como llega a las cajas (RF-240).
/// No guarda: quien lo llama decide la transacción, para que el saldo y el movimiento que lo causó se guarden juntos.
/// </summary>
internal sealed class RecalculadorPuntos(ContextoDatosCentral contexto, TimeProvider reloj)
{
    /// <param name="forzarPublicacion">Publica el maestro aunque el saldo no cambie: lo usa la inscripción, que se publica con saldo cero.</param>
    public async Task<SaldoPuntos> RecalcularAsync(Guid miembroId, string cedula, string usuario, bool forzarPublicacion = false,
        CancellationToken cancelacion = default)
    {
        var ahora = reloj.GetUtcNow();
        var hoy = DateOnly.FromDateTime(reloj.GetLocalNow().DateTime);
        var movimientos = await contexto.MovimientosPuntos.Where(m => m.MiembroId == miembroId).ToListAsync(cancelacion);

        // El movimiento que provocó el recálculo todavía no está en la base: se guarda junto con el saldo que produce.
        movimientos.AddRange(contexto.ChangeTracker.Entries<MovimientoPuntosCentral>()
            .Where(e => e.State == EntityState.Added && e.Entity.MiembroId == miembroId)
            .Select(e => e.Entity)
            .Where(m => movimientos.TrueForAll(existente => existente.Id != m.Id)));

        var calculado = SaldoPuntosCalculo.Calcular(movimientos, hoy);

        var saldo = await contexto.SaldosPuntos.SingleOrDefaultAsync(s => s.MiembroId == miembroId, cancelacion);
        if (saldo is null)
        {
            saldo = SaldoPuntosCentral.Crear(miembroId, cedula);
            contexto.SaldosPuntos.Add(saldo);
        }

        if (saldo.Aplicar(calculado, ahora) | forzarPublicacion)
            await PublicarEnMaestroAsync(miembroId, calculado, ahora, usuario, cancelacion);

        return calculado;
    }

    /// <summary>Lleva el saldo al maestro del miembro; si la inscripción del Central todavía no llegó, se publicará con el saldo al registrarla.</summary>
    private async Task PublicarEnMaestroAsync(Guid miembroId, SaldoPuntos saldo, DateTimeOffset ahora, string usuario, CancellationToken cancelacion)
    {
        // El maestro puede acabar de publicarse en esta misma operación (inscripción hecha en caja), así que primero se mira lo pendiente de guardar.
        var miembro = contexto.MiembrosFidelidad.Local.FirstOrDefault(m => m.Id == miembroId)
            ?? await contexto.MiembrosFidelidad.SingleOrDefaultAsync(m => m.Id == miembroId, cancelacion);
        if (miembro is null)
            return;

        miembro.SincronizarSaldo(saldo.Puntos, ahora, saldo.PuntosPorVencer, saldo.ProximoVencimiento);
        ColumnasMaestro.Marcar(contexto, miembro, ahora, usuario);
    }
}
