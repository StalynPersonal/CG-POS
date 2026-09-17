using CgPos.Central.Infraestructura.Persistencia;
using CgPos.Central.Infraestructura.Persistencia.Configuraciones;
using CgPos.Dominio.Fidelidad;
using Microsoft.EntityFrameworkCore;

namespace CgPos.Central.Infraestructura.Fidelidad;

/// <summary>
/// Recalcula el saldo de puntos de una cédula con todos sus movimientos y lo publica en el maestro del miembro, que es como llega a las cajas (RF-240).
/// Si la inscripción todavía no llegó, el saldo se calcula al registrarla.
/// No guarda: quien lo llama decide la transacción, para que el saldo y el movimiento que lo causó se guarden juntos.
/// </summary>
internal sealed class RecalculadorPuntos(ContextoDatosCentral contexto, TimeProvider reloj)
{
    /// <param name="forzarPublicacion">Publica el maestro aunque el saldo no cambie: lo usa la inscripción, que se publica con saldo cero.</param>
    public async Task<SaldoPuntos> RecalcularAsync(string cedula, string usuario, bool forzarPublicacion = false, CancellationToken cancelacion = default)
    {
        var ahora = reloj.GetUtcNow();
        var hoy = DateOnly.FromDateTime(reloj.GetLocalNow().DateTime);
        var movimientos = await contexto.MovimientosPuntos.Where(m => m.Cedula == cedula).ToListAsync(cancelacion);

        // El movimiento que provocó el recálculo todavía no está en la base: se guarda junto con el saldo que produce.
        movimientos.AddRange(contexto.ChangeTracker.Entries<MovimientoPuntosCentral>()
            .Where(e => e.State == EntityState.Added && e.Entity.Cedula == cedula)
            .Select(e => e.Entity)
            .Where(m => movimientos.TrueForAll(existente => existente.Id != m.Id)));

        var calculado = SaldoPuntosCalculo.Calcular(movimientos, hoy);

        // El maestro puede acabar de publicarse en esta misma operación (inscripción hecha en caja), así que primero se mira lo pendiente de guardar.
        var miembro = contexto.MiembrosFidelidad.Local.FirstOrDefault(m => m.Cedula == cedula)
            ?? await contexto.MiembrosFidelidad.SingleOrDefaultAsync(m => m.Cedula == cedula, cancelacion);
        if (miembro is null)
            return calculado;

        var saldo = await contexto.SaldosPuntos.SingleOrDefaultAsync(s => s.MiembroId == miembro.Id, cancelacion);
        if (saldo is null)
        {
            saldo = SaldoPuntosCentral.Crear(miembro.Id, cedula);
            contexto.SaldosPuntos.Add(saldo);
        }

        if (saldo.Aplicar(calculado, ahora) | forzarPublicacion)
        {
            miembro.SincronizarSaldo(calculado.Puntos, ahora, calculado.PuntosPorVencer, calculado.ProximoVencimiento);
            ColumnasMaestro.Marcar(contexto, miembro, ahora, usuario);
        }

        return calculado;
    }
}
