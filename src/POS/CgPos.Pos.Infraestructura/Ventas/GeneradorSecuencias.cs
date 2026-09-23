using System.Globalization;
using CgPos.Dominio.Organizacion;
using CgPos.Pos.Aplicacion.Organizacion;
using CgPos.Pos.Infraestructura.Persistencia;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CgPos.Pos.Infraestructura.Ventas;

/// <summary>Contador por caja y tipo (turnos, transacciones…).</summary>
internal sealed class SecuenciaCaja
{
    public const int LargoMaximoTipo = 30;

    public int CajaId { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public long Ultimo { get; set; }

    // Los Id son de esta base: si se recrea o se restaura otra, el código dice de qué caja es la numeración.
    public string SucursalCodigo { get; set; } = string.Empty;
    public string CajaCodigo { get; set; } = string.Empty;
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
internal sealed class GeneradorSecuencias(ContextoDatosPos contexto, ILogger<GeneradorSecuencias> registro)
{
    /// <param name="minimo">
    /// Valor mínimo que puede entregar (el que se configuró para continuar una numeración, ej. tras reinstalar la caja). Solo empuja la secuencia
    /// hacia adelante: si ya va más alta, sigue desde donde iba y nunca repite un número.
    /// </param>
    /// <summary>
    /// El número que entregaría <see cref="SiguienteAsync"/> ahora, sin consumirlo: es lo que se muestra mientras la venta
    /// se arma. Puede cambiar si otra venta se cobra antes; el número de verdad se toma al cobrar.
    /// </summary>
    public async Task<long> VerSiguienteAsync(int cajaId, string tipo, CancellationToken cancelacion, long minimo = 1)
    {
        var ultimo = await contexto.Set<SecuenciaCaja>().AsNoTracking()
            .Where(s => s.CajaId == cajaId && s.Tipo == tipo)
            .Select(s => (long?)s.Ultimo)
            .SingleOrDefaultAsync(cancelacion);

        return ultimo is { } valor ? Math.Max(valor + 1, minimo) : minimo;
    }

    public async Task<long> SiguienteAsync(int cajaId, string tipo, CancellationToken cancelacion, long minimo = 1)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(minimo, 1);

        var valores = await contexto.Database.SqlQuery<long>($"""
            MERGE SecuenciasCaja WITH (HOLDLOCK) AS destino
            USING (SELECT Id AS CajaId, SucursalCodigo, Codigo AS CajaCodigo, {tipo} AS Tipo, {minimo} AS Minimo FROM Cajas WHERE Id = {cajaId}) AS origen
                ON destino.CajaId = origen.CajaId AND destino.Tipo = origen.Tipo
            WHEN MATCHED THEN UPDATE SET
                Ultimo = CASE WHEN destino.Ultimo + 1 < origen.Minimo THEN origen.Minimo ELSE destino.Ultimo + 1 END,
                SucursalCodigo = origen.SucursalCodigo,
                CajaCodigo = origen.CajaCodigo
            WHEN NOT MATCHED THEN INSERT (CajaId, Tipo, Ultimo, SucursalCodigo, CajaCodigo)
                VALUES (origen.CajaId, origen.Tipo, origen.Minimo, origen.SucursalCodigo, origen.CajaCodigo)
            OUTPUT inserted.Ultimo AS Value;
            """).ToListAsync(cancelacion);

        return valores.Single();
    }

    /// <summary>
    /// Las tablas de trabajo de la venta (la que se arma y la que está en espera, con sus líneas y entregas) no guardan
    /// documentos. Al cerrar el turno quedan vacías, y sus Id vuelven a empezar en 1 para el turno siguiente. Si alguna
    /// todavía tiene filas no se toca nada, así un Id nunca se repite mientras exista. Devuelve si se reiniciaron.
    /// </summary>
    /// <remarks>
    /// Si la base no deja reiniciar (el usuario no tiene permiso sobre las secuencias), el Id sigue desde donde iba: es
    /// orden, no una regla del negocio, así que el cierre no falla por esto.
    /// </remarks>
    public async Task<bool> ReiniciarIdsDeTrabajoAsync(CancellationToken cancelacion)
    {
        try
        {
            var reiniciadas = await contexto.Database.SqlQuery<int>($"""
                IF NOT EXISTS (SELECT 1 FROM [VentasEnProceso]) AND NOT EXISTS (SELECT 1 FROM [LineasVentaEnProceso])
                   AND NOT EXISTS (SELECT 1 FROM [DestinosEntregaVentaEnProceso]) AND NOT EXISTS (SELECT 1 FROM [LineasDestinoEntregaEnProceso])
                   AND NOT EXISTS (SELECT 1 FROM [VentasGuardadas]) AND NOT EXISTS (SELECT 1 FROM [LineasVentaGuardadas])
                   AND NOT EXISTS (SELECT 1 FROM [DestinosEntregaVentaGuardadas]) AND NOT EXISTS (SELECT 1 FROM [LineasDestinoEntregaGuardadas])
                BEGIN
                    ALTER SEQUENCE [SecuenciaVentasEnProceso] RESTART WITH 1;
                    ALTER SEQUENCE [SecuenciaLineasVentaEnProceso] RESTART WITH 1;
                    ALTER SEQUENCE [SecuenciaDestinosEntregaVentaEnProceso] RESTART WITH 1;
                    ALTER SEQUENCE [SecuenciaLineasDestinoEntregaEnProceso] RESTART WITH 1;
                    ALTER SEQUENCE [SecuenciaVentasGuardadas] RESTART WITH 1;
                    ALTER SEQUENCE [SecuenciaLineasVentaGuardadas] RESTART WITH 1;
                    ALTER SEQUENCE [SecuenciaDestinosEntregaVentaGuardadas] RESTART WITH 1;
                    ALTER SEQUENCE [SecuenciaLineasDestinoEntregaGuardadas] RESTART WITH 1;
                    SELECT 1 AS Value;
                END
                ELSE
                    SELECT 0 AS Value;
                """).ToListAsync(cancelacion);
            return reiniciadas.Single() == 1;
        }
        catch (SqlException excepcion)
        {
            registro.LogWarning(excepcion, "No se pudieron reiniciar los Id de las ventas en curso y en espera; siguen desde donde iban.");
            return false;
        }
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
