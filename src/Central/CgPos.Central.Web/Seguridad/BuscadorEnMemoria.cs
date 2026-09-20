using CgPos.Dominio.Comun;

namespace CgPos.Central.Web.Seguridad;

/// <summary>
/// Busca dentro de una lista que ya está en el navegador.
///
/// Arma una sola vez el texto por el que se busca cada elemento y luego compara contra eso. Recalcularlo en cada tecla se
/// siente en cuanto la lista pasa de unos cientos de filas, que es lo que ocurre con las marcas o los artículos. El texto
/// va sin acentos y en minúsculas, así que escribir «cafe» encuentra «Café».
/// </summary>
public sealed class BuscadorEnMemoria<T>
{
    private List<(T Elemento, string Texto)> _indice = [];

    /// <summary>Lo que hay que buscar de cada elemento. Se llama una vez por elemento, al indexar.</summary>
    public void Indexar(IEnumerable<T> elementos, Func<T, string> texto)
    {
        ArgumentNullException.ThrowIfNull(elementos);
        ArgumentNullException.ThrowIfNull(texto);

        _indice = elementos.Select(e => (e, TextoBusqueda.Normalizar(texto(e)) ?? string.Empty)).ToList();
    }

    /// <summary>Los elementos que contienen lo buscado; todos si no se busca nada.</summary>
    public List<T> Filtrar(string? buscado)
    {
        var texto = TextoBusqueda.Normalizar(buscado);
        return texto is null
            ? _indice.Select(i => i.Elemento).ToList()
            : _indice.Where(i => i.Texto.Contains(texto, StringComparison.Ordinal)).Select(i => i.Elemento).ToList();
    }
}
