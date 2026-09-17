using CgPos.Central.Infraestructura.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CgPos.Central.Infraestructura.Maestros;

/// <summary>
/// Pasa a su tabla los maestros que todavía están en la tabla JSON (MaestrosCentral), un tipo a la vez y en una transacción por tipo.
/// SQL Server 2014 no lee JSON, por eso se hace aquí y no en la migración de EF. Es idempotente: si ya no queda nada en JSON, no hace nada.
/// Los registros conservan su Id, cuándo y quién los cambió; reciben una versión nueva, así las cajas los vuelven a bajar sin cambios de fondo.
/// </summary>
internal static class MigracionMaestrosATablas
{
    public static async Task EjecutarAsync(ContextoDatosCentral contexto, ILogger registro, CancellationToken cancelacion)
    {
        foreach (var tabla in TablasMaestros.Todas)
        {
            var filas = await contexto.MaestrosCentral.Where(m => m.Tipo == tabla.Tipo).ToListAsync(cancelacion);
            if (filas.Count == 0)
                continue;

            await using var transaccion = await contexto.Database.BeginTransactionAsync(cancelacion);
            foreach (var fila in filas)
            {
                await tabla.MigrarAsync(contexto, fila, cancelacion);
                contexto.MaestrosCentral.Remove(fila);
            }

            await contexto.SaveChangesAsync(cancelacion);
            await transaccion.CommitAsync(cancelacion);
            contexto.ChangeTracker.Clear();
            registro.LogInformation("Maestros {Tipo} pasados a su tabla: {Cantidad}", tabla.Tipo, filas.Count);
        }
    }
}
