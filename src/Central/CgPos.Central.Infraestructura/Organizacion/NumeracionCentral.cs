using CgPos.Central.Aplicacion.Organizacion;
using CgPos.Central.Infraestructura.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace CgPos.Central.Infraestructura.Organizacion;

/// <summary>
/// Contador de un documento que numera el propio Central. Cada documento tiene su fila y sin ella no se puede crear: la
/// numeración de un documento es una decisión del negocio, no algo que el sistema invente la primera vez que hace falta.
/// </summary>
public sealed class SecuenciaCentral
{
    public const int LargoMaximoPrefijo = 10;
    public const int LargoMaximoDocumento = 60;

    /// <summary>Lo que se antepone al correlativo y con lo que el código pide su número: COT, LB…</summary>
    public string Prefijo { get; set; } = string.Empty;

    /// <summary>Qué documento numera, tal como se le llama en el negocio. Es lo que se ve al administrarla.</summary>
    public string Documento { get; set; } = string.Empty;

    /// <summary>Último número entregado; el próximo documento se lleva este más uno.</summary>
    public long Ultimo { get; set; }

    /// <summary>Con cuántos dígitos se escribe el correlativo: 6 da COT000001.</summary>
    public int Digitos { get; set; } = 6;

    /// <summary>Desactivarla impide crear ese documento, sin borrar el contador.</summary>
    public bool Activa { get; set; } = true;
}

/// <summary>No hay una secuencia configurada para ese documento, o está desactivada, así que no se puede crear.</summary>
public sealed class SecuenciaCentralNoConfiguradaExcepcion(string mensaje) : Exception(mensaje);

/// <summary>
/// Entrega el siguiente número con un único UPDATE … OUTPUT: dos usuarios que crean un documento a la vez nunca reciben el
/// mismo. Si la operación falla después, ese número se pierde; un hueco en la numeración de un documento interno es
/// preferible a dos documentos con el mismo número.
///
/// Si el documento no tiene su secuencia configurada no se inventa ninguna: se rechaza la operación diciendo cuál falta.
/// </summary>
internal sealed class NumeracionCentral(ContextoDatosCentral contexto) : INumeracionCentral
{
    public async Task<string> SiguienteAsync(string prefijo, CancellationToken cancelacion = default)
    {
        var limpio = (prefijo ?? string.Empty).Trim().ToUpperInvariant();
        ArgumentException.ThrowIfNullOrEmpty(limpio);

        // El UPDATE solo toca la fila si existe y está activa; devuelve el número ya incrementado.
        var valores = await contexto.Database.SqlQuery<long>($"""
            UPDATE SecuenciasCentral WITH (ROWLOCK)
            SET Ultimo = Ultimo + 1
            OUTPUT inserted.Ultimo AS Value
            WHERE Prefijo = {limpio} AND Activa = 1;
            """).ToListAsync(cancelacion);

        if (valores.Count == 0)
            throw new SecuenciaCentralNoConfiguradaExcepcion(await MensajeAsync(limpio, cancelacion));

        var secuencia = await contexto.SecuenciasCentral.AsNoTracking().SingleAsync(s => s.Prefijo == limpio, cancelacion);
        return $"{limpio}{valores.Single().ToString(new string('0', Math.Clamp(secuencia.Digitos, 1, 18)))}";
    }

    /// <summary>Distingue la secuencia que no existe de la que está apagada: se corrigen en sitios distintos.</summary>
    private async Task<string> MensajeAsync(string prefijo, CancellationToken cancelacion)
    {
        var secuencia = await contexto.SecuenciasCentral.AsNoTracking().SingleOrDefaultAsync(s => s.Prefijo == prefijo, cancelacion);
        return secuencia is null
            ? $"No hay una secuencia configurada con el prefijo {prefijo}. Configúrela en Organización → Secuencias antes de crear este documento."
            : $"La secuencia de {secuencia.Documento} ({prefijo}) está desactivada: actívela en Organización → Secuencias para poder crear el documento.";
    }
}
