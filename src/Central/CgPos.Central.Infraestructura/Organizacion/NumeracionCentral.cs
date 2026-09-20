using CgPos.Central.Aplicacion.Organizacion;
using CgPos.Central.Infraestructura.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace CgPos.Central.Infraestructura.Organizacion;

/// <summary>
/// Contador de un documento que numera el propio Central. Cada documento tiene su fila y sin ella no se puede crear: la
/// numeración es una decisión del negocio, no algo que el sistema invente la primera vez que hace falta.
///
/// El <see cref="Codigo"/> es con lo que el sistema la busca y no cambia nunca; el <see cref="Prefijo"/> es solo cómo se ve
/// el número y se puede cambiar cuando el negocio quiera, sin que deje de funcionar nada.
/// </summary>
public sealed class SecuenciaCentral
{
    public const int LargoMaximoCodigo = 30;
    public const int LargoMaximoPrefijo = 10;
    public const int LargoMaximoDocumento = 60;

    /// <summary>Con lo que el sistema pide su número: Cotizacion, ListaBoda… No se cambia.</summary>
    public string Codigo { get; set; } = string.Empty;

    /// <summary>Lo que se antepone al correlativo: COT, LB… Esto sí se puede cambiar.</summary>
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

/// <summary>Códigos de los documentos que numera el Central. Son los que el sistema busca en la tabla.</summary>
public static class DocumentosNumerados
{
    public const string Cotizacion = "Cotizacion";
    public const string ListaBoda = "ListaBoda";

    /// <summary>Numeración propia del Central para las facturas que le suben las cajas.</summary>
    public const string Factura = "Factura";

    /// <summary>Numeración propia del Central para las notas de crédito que le suben las cajas.</summary>
    public const string NotaCredito = "NotaCredito";

    /// <summary>Pendientes de entrega y envíos que le suben las cajas y que el Central despacha.</summary>
    public const string Despacho = "Despacho";

    /// <summary>Cierre consolidado del día de una sucursal, que se hace en el Central.</summary>
    public const string CierreSucursal = "CierreSucursal";
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
    public async Task<string> SiguienteAsync(string codigoDocumento, CancellationToken cancelacion = default)
    {
        var codigo = (codigoDocumento ?? string.Empty).Trim();
        ArgumentException.ThrowIfNullOrEmpty(codigo);

        // El UPDATE solo toca la fila si existe y está activa; devuelve el número ya incrementado.
        var valores = await contexto.Database.SqlQuery<long>($"""
            UPDATE SecuenciasCentral WITH (ROWLOCK)
            SET Ultimo = Ultimo + 1
            OUTPUT inserted.Ultimo AS Value
            WHERE Codigo = {codigo} AND Activa = 1;
            """).ToListAsync(cancelacion);

        var secuencia = await contexto.SecuenciasCentral.AsNoTracking().SingleOrDefaultAsync(s => s.Codigo == codigo, cancelacion);
        if (valores.Count == 0 || secuencia is null)
            throw new SecuenciaCentralNoConfiguradaExcepcion(Mensaje(codigo, secuencia));

        return $"{secuencia.Prefijo}{valores.Single().ToString(new string('0', Math.Clamp(secuencia.Digitos, 1, 18)))}";
    }

    /// <summary>Distingue la secuencia que no existe de la que está apagada: se corrigen en sitios distintos.</summary>
    private static string Mensaje(string codigo, SecuenciaCentral? secuencia) =>
        secuencia is null
            ? $"No hay una secuencia configurada para el documento {codigo}. Créela en Organización → Secuencias de documentos antes de emitirlo."
            : $"La secuencia de {secuencia.Documento} está desactivada: actívela en Organización → Secuencias de documentos para poder crear el documento.";
}
