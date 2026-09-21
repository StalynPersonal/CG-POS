using System.Collections.Concurrent;
using System.Reflection;

namespace CgPos.Dominio.Comun;

/// <summary>
/// Copia todas las propiedades con valor de una entidad a otra del mismo tipo base.
///
/// La venta pasa de una tabla a otra (armándose, en espera, cobrada) y en cada paso tiene que llevarse todo lo que tenía.
/// Copiarlas a mano obliga a acordarse de actualizar la copia cada vez que se agrega un campo, y olvidarlo no da error:
/// el dato simplemente se pierde al guardar en espera. Recorriendo las propiedades, lo nuevo viaja solo.
/// </summary>
internal static class CopiaPropiedades
{
    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> Cache = new();

    /// <summary>
    /// Copia las propiedades de <paramref name="tipo"/> y de sus bases. No toca el Id —cada tabla da el suyo— ni las
    /// colecciones, que cada entidad clona con sus propios tipos. Lo que agregue una variante (la referencia de la venta
    /// en espera, por ejemplo) no se copia: se indica el tipo común, no el de la variante.
    /// </summary>
    /// <param name="excepto">Propiedades que no deben copiarse, por nombre.</param>
    public static void Copiar(object origen, object destino, Type tipo, IReadOnlyCollection<string>? excepto = null)
    {
        ArgumentNullException.ThrowIfNull(origen);
        ArgumentNullException.ThrowIfNull(destino);

        foreach (var propiedad in PropiedadesDe(tipo))
        {
            if (excepto is not null && excepto.Contains(propiedad.Name))
                continue;

            propiedad.SetValue(destino, propiedad.GetValue(origen));
        }
    }

    /// <summary>
    /// Las propiedades con escritura de <paramref name="tipo"/> y de sus bases hasta <see cref="Entidad"/>. Se recorre
    /// cada nivel porque un setter privado de la base no aparece al buscar desde la derivada.
    /// </summary>
    public static PropertyInfo[] PropiedadesDe(Type tipo) =>
        Cache.GetOrAdd(tipo, _ =>
        {
            var propiedades = new List<PropertyInfo>();
            for (var actual = tipo; actual is not null && actual != typeof(Entidad) && actual != typeof(object); actual = actual.BaseType)
            {
                propiedades.AddRange(actual
                    .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                    .Where(p => p.GetSetMethod(nonPublic: true) is not null && p.GetIndexParameters().Length == 0 && !EsColeccion(p.PropertyType)));
            }

            return [.. propiedades];
        });

    private static bool EsColeccion(Type tipo) =>
        tipo != typeof(string) && typeof(System.Collections.IEnumerable).IsAssignableFrom(tipo);
}
