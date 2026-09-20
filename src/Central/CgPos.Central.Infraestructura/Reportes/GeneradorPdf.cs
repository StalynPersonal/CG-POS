using System.Globalization;
using System.Text;
using CgPos.Contratos.Central;

namespace CgPos.Central.Infraestructura.Reportes;

/// <summary>
/// PDF mínimo de una tabla, en A4 apaisado: es la forma de los reportes del Central, que son anchos. El PDF en sí lo escribe
/// EnsambladorPdf, que también sirve para documentos con otra forma, como la cotización que se le entrega a un cliente.
/// </summary>
internal static class GeneradorPdf
{
    private static readonly HojaPdf Hoja = HojaPdf.A4Apaisada;

    public static byte[] Crear(TablaReporte tabla)
    {
        var anchos = Anchos(tabla);
        var lineas = new List<(string Texto, bool Negrita)>
        {
            (tabla.Titulo, true),
            (tabla.Subtitulo, false),
            (string.Empty, false),
            (Linea(tabla.Columnas, anchos, tabla.Alineaciones), true),
            (new string('-', anchos.Sum() + (anchos.Count - 1) * 2), false),
        };

        lineas.AddRange(tabla.Filas.Select(fila => (Linea(fila, anchos, tabla.Alineaciones), false)));
        if (tabla.Totales is { } totales)
        {
            lineas.Add((new string('-', anchos.Sum() + (anchos.Count - 1) * 2), false));
            lineas.Add((Linea(totales, anchos, tabla.Alineaciones), true));
        }

        var paginas = lineas.Chunk(Hoja.Lineas).ToList();
        if (paginas.Count == 0)
            paginas.Add([(tabla.Titulo, true)]);

        return EnsambladorPdf.Crear(paginas, Hoja);
    }

    private static List<int> Anchos(TablaReporte tabla)
    {
        var anchos = tabla.Columnas.Select(c => c.Length).ToList();
        foreach (var fila in tabla.Filas.Concat(tabla.Totales is null ? [] : [tabla.Totales]))
        {
            for (var columna = 0; columna < anchos.Count && columna < fila.Count; columna++)
                anchos[columna] = Math.Max(anchos[columna], (fila[columna] ?? string.Empty).Length);
        }

        // Si la fila no cabe a lo ancho, las columnas de texto se recortan (los números nunca).
        var disponibles = Hoja.Columnas;
        for (var intento = 0; intento < 50 && anchos.Sum() + (anchos.Count - 1) * 2 > disponibles; intento++)
        {
            var mayor = anchos.Select((ancho, indice) => (ancho, indice))
                .Where(c => c.indice >= tabla.Alineaciones.Count || !tabla.Alineaciones[c.indice])
                .OrderByDescending(c => c.ancho)
                .FirstOrDefault();
            if (mayor.ancho <= 6)
                break;

            anchos[mayor.indice] = mayor.ancho - Math.Max(1, mayor.ancho / 10);
        }

        return anchos;
    }

    private static string Linea(IReadOnlyList<string> valores, IReadOnlyList<int> anchos, IReadOnlyList<bool> alineaciones)
    {
        var celdas = new List<string>(anchos.Count);
        for (var columna = 0; columna < anchos.Count; columna++)
        {
            var valor = columna < valores.Count ? valores[columna] ?? string.Empty : string.Empty;
            if (valor.Length > anchos[columna])
                valor = valor[..anchos[columna]];

            var derecha = columna < alineaciones.Count && alineaciones[columna];
            celdas.Add(derecha ? valor.PadLeft(anchos[columna]) : valor.PadRight(anchos[columna]));
        }

        return string.Join("  ", celdas).TrimEnd();
    }
}
