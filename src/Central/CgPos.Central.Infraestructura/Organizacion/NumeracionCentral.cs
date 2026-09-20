using CgPos.Central.Aplicacion.Organizacion;
using CgPos.Central.Infraestructura.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace CgPos.Central.Infraestructura.Organizacion;

/// <summary>Contador de los documentos que numera el propio Central (cotizaciones, listas de boda…).</summary>
internal sealed class SecuenciaCentral
{
    public const int LargoMaximoPrefijo = 10;

    public string Prefijo { get; set; } = string.Empty;
    public long Ultimo { get; set; }
}

/// <summary>
/// Entrega el siguiente número con un único MERGE … OUTPUT: dos usuarios que crean un documento a la vez nunca reciben el
/// mismo. Si la operación falla después, ese número se pierde; un hueco en la numeración de un documento interno es
/// preferible a dos documentos con el mismo número.
/// </summary>
internal sealed class NumeracionCentral(ContextoDatosCentral contexto) : INumeracionCentral
{
    public async Task<string> SiguienteAsync(string prefijo, CancellationToken cancelacion = default)
    {
        var limpio = (prefijo ?? string.Empty).Trim().ToUpperInvariant();
        ArgumentException.ThrowIfNullOrEmpty(limpio);

        var valores = await contexto.Database.SqlQuery<long>($"""
            MERGE SecuenciasCentral WITH (HOLDLOCK) AS destino
            USING (SELECT {limpio} AS Prefijo) AS origen
                ON destino.Prefijo = origen.Prefijo
            WHEN MATCHED THEN UPDATE SET Ultimo = destino.Ultimo + 1
            WHEN NOT MATCHED THEN INSERT (Prefijo, Ultimo) VALUES (origen.Prefijo, 1)
            OUTPUT inserted.Ultimo AS Value;
            """).ToListAsync(cancelacion);

        return $"{limpio}{valores.Single():000000}";
    }
}
