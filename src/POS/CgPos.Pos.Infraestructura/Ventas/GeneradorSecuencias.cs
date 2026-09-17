using System.Globalization;
using CgPos.Dominio.Organizacion;
using CgPos.Pos.Aplicacion.Organizacion;
using CgPos.Pos.Infraestructura.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace CgPos.Pos.Infraestructura.Ventas;

/// <summary>Contador por caja y tipo (turnos, transacciones…).</summary>
internal sealed class SecuenciaCaja
{
    public const int LargoMaximoTipo = 30;

    public int CajaId { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public long Ultimo { get; set; }
}

internal static class TiposSecuencia
{
    public const string Turno = "Turno";
    public const string Transaccion = "Transaccion";
    public const string NotaCredito = "NotaCredito";
    public const string PendienteEntrega = "PendienteEntrega";
}

/// <summary>
/// Entrega el siguiente número de forma atómica con un único MERGE … OUTPUT: dos pedidos simultáneos nunca
/// reciben el mismo valor. Si la operación que lo pidió falla, el número se pierde (hueco), lo que es aceptable
/// para el número interno de transacción; los NCF usan su propio control.
/// </summary>
internal sealed class GeneradorSecuencias(ContextoDatosPos contexto)
{
    /// <param name="minimo">
    /// Valor mínimo que puede entregar (el que se configuró para continuar una numeración, ej. tras reinstalar la caja). Solo empuja la secuencia
    /// hacia adelante: si ya va más alta, sigue desde donde iba y nunca repite un número.
    /// </param>
    public async Task<long> SiguienteAsync(int cajaId, string tipo, CancellationToken cancelacion, long minimo = 1)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(minimo, 1);

        var valores = await contexto.Database.SqlQuery<long>($"""
            MERGE SecuenciasCaja WITH (HOLDLOCK) AS destino
            USING (SELECT {cajaId} AS CajaId, {tipo} AS Tipo, {minimo} AS Minimo) AS origen
                ON destino.CajaId = origen.CajaId AND destino.Tipo = origen.Tipo
            WHEN MATCHED THEN UPDATE SET Ultimo = CASE WHEN destino.Ultimo + 1 < origen.Minimo THEN origen.Minimo ELSE destino.Ultimo + 1 END
            WHEN NOT MATCHED THEN INSERT (CajaId, Tipo, Ultimo) VALUES (origen.CajaId, origen.Tipo, origen.Minimo)
            OUTPUT inserted.Ultimo AS Value;
            """).ToListAsync(cancelacion);

        return valores.Single();
    }
}

/// <summary>Numeración de los documentos de la caja según los parámetros del Central.</summary>
internal static class NumeracionDocumentos
{
    public static Task<int> DigitosAsync(IParametros parametros, int cajaId, CancellationToken cancelacion) =>
        parametros.ObtenerEnteroAsync(CatalogoParametros.DigitosSecuenciaDocumentos, cajaId, cancelacion);

    /// <summary>Mínimo configurado para la próxima secuencia; 1 si no se configuró.</summary>
    public static async Task<long> MinimoAsync(IParametros parametros, string clave, int cajaId, CancellationToken cancelacion) =>
        await parametros.ObtenerAsync(clave, cajaId, cancelacion) is { Length: > 0 } texto
            ? long.TryParse(texto, NumberStyles.Integer, CultureInfo.InvariantCulture, out var minimo) && minimo >= 1
                ? minimo
                : throw new ParametroNoConfiguradoExcepcion(clave, "debe ser un número entero mayor que cero")
            : 1;
}
