using System.Globalization;
using System.Text;
using CgPos.Contratos.Central;

namespace CgPos.Central.Infraestructura.Reportes;

/// <summary>
/// PDF mínimo de una tabla, en apaisado y con Courier (una de las 14 fuentes que todo visor trae, así no hay que incrustar nada).
/// Suficiente para los reportes del Central y sin dependencias que instalar en el servidor de la empresa.
/// </summary>
internal static class GeneradorPdf
{
    private const double AnchoPagina = 842;  // A4 apaisado, en puntos
    private const double AltoPagina = 595;
    private const double Margen = 28;
    private const double TamanoLetra = 8;
    private const double AnchoCaracter = TamanoLetra * 0.6;  // Courier es monoespaciada
    private const double AltoLinea = 11;

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

        var porPagina = (int)Math.Floor((AltoPagina - 2 * Margen - AltoLinea) / AltoLinea);
        var paginas = lineas.Chunk(Math.Max(porPagina, 1)).ToList();
        if (paginas.Count == 0)
            paginas.Add([(tabla.Titulo, true)]);

        return Ensamblar(paginas);
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
        var disponibles = (int)Math.Floor((AnchoPagina - 2 * Margen) / AnchoCaracter);
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

    private static byte[] Ensamblar(IReadOnlyList<(string Texto, bool Negrita)[]> paginas)
    {
        // Objetos: 1 catálogo, 2 páginas, 3 y 4 fuentes, 5.. una página y su contenido.
        var objetos = new List<string>();
        var idsPaginas = Enumerable.Range(0, paginas.Count).Select(i => 5 + i * 2).ToList();

        objetos.Add("<< /Type /Catalog /Pages 2 0 R >>");
        objetos.Add($"<< /Type /Pages /Kids [{string.Join(' ', idsPaginas.Select(id => $"{id} 0 R"))}] /Count {paginas.Count} >>");
        objetos.Add("<< /Type /Font /Subtype /Type1 /BaseFont /Courier /Encoding /WinAnsiEncoding >>");
        objetos.Add("<< /Type /Font /Subtype /Type1 /BaseFont /Courier-Bold /Encoding /WinAnsiEncoding >>");

        for (var indice = 0; indice < paginas.Count; indice++)
        {
            var contenidoId = idsPaginas[indice] + 1;
            objetos.Add($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {AnchoPagina:F0} {AltoPagina:F0}] " +
                        $"/Resources << /Font << /F1 3 0 R /F2 4 0 R >> >> /Contents {contenidoId} 0 R >>");
            var flujo = Contenido(paginas[indice], indice + 1, paginas.Count);
            objetos.Add($"<< /Length {Encoding.ASCII.GetByteCount(flujo)} >>\nstream\n{flujo}\nendstream");
        }

        var pdf = new StringBuilder("%PDF-1.4\n");
        var posiciones = new List<int>();
        for (var indice = 0; indice < objetos.Count; indice++)
        {
            posiciones.Add(Encoding.ASCII.GetByteCount(pdf.ToString()));
            pdf.Append(CultureInfo.InvariantCulture, $"{indice + 1} 0 obj\n{objetos[indice]}\nendobj\n");
        }

        var inicioXref = Encoding.ASCII.GetByteCount(pdf.ToString());
        pdf.Append(CultureInfo.InvariantCulture, $"xref\n0 {objetos.Count + 1}\n0000000000 65535 f \n");
        foreach (var posicion in posiciones)
            pdf.Append(CultureInfo.InvariantCulture, $"{posicion:D10} 00000 n \n");

        pdf.Append(CultureInfo.InvariantCulture,
            $"trailer\n<< /Size {objetos.Count + 1} /Root 1 0 R >>\nstartxref\n{inicioXref}\n%%EOF");

        return Encoding.ASCII.GetBytes(pdf.ToString());
    }

    private static string Contenido((string Texto, bool Negrita)[] lineas, int pagina, int total)
    {
        var flujo = new StringBuilder("BT\n");
        var y = AltoPagina - Margen;
        foreach (var (texto, negrita) in lineas)
        {
            flujo.Append(CultureInfo.InvariantCulture, $"/{(negrita ? "F2" : "F1")} {TamanoLetra:F0} Tf\n1 0 0 1 {Margen:F0} {y:F0} Tm\n({Escapar(texto)}) Tj\n");
            y -= AltoLinea;
        }

        flujo.Append(CultureInfo.InvariantCulture,
            $"/F1 {TamanoLetra:F0} Tf\n1 0 0 1 {AnchoPagina - Margen - 80:F0} {Margen:F0} Tm\n(Página {pagina} de {total}) Tj\n");
        flujo.Append("ET");
        return flujo.ToString();
    }

    /// <summary>Texto para el PDF: se escapan los paréntesis y las tildes se pasan a WinAnsi.</summary>
    private static string Escapar(string texto)
    {
        var limpio = texto.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
        var bytes = Encoding.Latin1.GetBytes(limpio);
        var resultado = new StringBuilder(bytes.Length);
        foreach (var caracter in bytes)
        {
            if (caracter < 32 || caracter > 126)
                resultado.Append(CultureInfo.InvariantCulture, $"\\{Convert.ToString(caracter, 8).PadLeft(3, '0')}");
            else
                resultado.Append((char)caracter);
        }

        return resultado.ToString();
    }
}
