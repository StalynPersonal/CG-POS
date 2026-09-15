using CgPos.Pos.Infraestructura.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace CgPos.Pos.Infraestructura.Ventas;

/// <summary>Contador por caja y tipo (turnos, transacciones…).</summary>
internal sealed class SecuenciaCaja
{
    public const int LargoMaximoTipo = 30;

    public Guid CajaId { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public long Ultimo { get; set; }
}

internal static class TiposSecuencia
{
    public const string Turno = "Turno";
    public const string Transaccion = "Transaccion";
    public const string NotaCredito = "NotaCredito";
}

/// <summary>
/// Entrega el siguiente número de forma atómica con un único MERGE … OUTPUT: dos pedidos simultáneos nunca
/// reciben el mismo valor. Si la operación que lo pidió falla, el número se pierde (hueco), lo que es aceptable
/// para el número interno de transacción; los NCF usan su propio control.
/// </summary>
internal sealed class GeneradorSecuencias(ContextoDatosPos contexto)
{
    public async Task<long> SiguienteAsync(Guid cajaId, string tipo, CancellationToken cancelacion)
    {
        var valores = await contexto.Database.SqlQuery<long>($"""
            MERGE SecuenciasCaja WITH (HOLDLOCK) AS destino
            USING (SELECT {cajaId} AS CajaId, {tipo} AS Tipo) AS origen
                ON destino.CajaId = origen.CajaId AND destino.Tipo = origen.Tipo
            WHEN MATCHED THEN UPDATE SET Ultimo = destino.Ultimo + 1
            WHEN NOT MATCHED THEN INSERT (CajaId, Tipo, Ultimo) VALUES (origen.CajaId, origen.Tipo, 1)
            OUTPUT inserted.Ultimo AS Value;
            """).ToListAsync(cancelacion);

        return valores.Single();
    }
}
