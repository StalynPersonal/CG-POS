using System.Globalization;
using System.Text;

namespace CgPos.Central.Infraestructura.Reportes;

/// <summary>Tamaño de la hoja, en puntos.</summary>
internal sealed record HojaPdf(double Ancho, double Alto, double Margen, double TamanoLetra, double AltoLinea)
{
    /// <summary>A4 apaisado: la de los reportes, que son tablas anchas.</summary>
    public static HojaPdf A4Apaisada { get; } = new(842, 595, 28, 8, 11);

    /// <summary>Carta vertical: la de los documentos que se le entregan a un cliente.</summary>
    public static HojaPdf CartaVertical { get; } = new(612, 792, 40, 9, 13);

    /// <summary>Caracteres que caben a lo ancho; Courier es monoespaciada, así que es una división.</summary>
    public int Columnas => (int)Math.Floor((Ancho - 2 * Margen) / (TamanoLetra * 0.6));

    /// <summary>Líneas que caben en una hoja, dejando sitio al pie.</summary>
    public int Lineas => Math.Max(1, (int)Math.Floor((Alto - 2 * Margen - AltoLinea) / AltoLinea));
}

/// <summary>
/// Escribe el PDF a mano, sin librerías: el Central se instala en la empresa y no descarga nada. Usa Courier, una de las 14
/// fuentes que todo visor trae, así que tampoco hay que incrustar tipografías.
/// </summary>
internal static class EnsambladorPdf
{
    public static byte[] Crear(IReadOnlyList<(string Texto, bool Negrita)[]> paginas, HojaPdf hoja, bool numerarPaginas = true)
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
            objetos.Add($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {hoja.Ancho:F0} {hoja.Alto:F0}] " +
                        $"/Resources << /Font << /F1 3 0 R /F2 4 0 R >> >> /Contents {contenidoId} 0 R >>");
            var flujo = Contenido(paginas[indice], hoja, indice + 1, paginas.Count, numerarPaginas);
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

    private static string Contenido((string Texto, bool Negrita)[] lineas, HojaPdf hoja, int pagina, int total, bool numerar)
    {
        var flujo = new StringBuilder("BT\n");
        var y = hoja.Alto - hoja.Margen;
        foreach (var (texto, negrita) in lineas)
        {
            flujo.Append(CultureInfo.InvariantCulture,
                $"/{(negrita ? "F2" : "F1")} {hoja.TamanoLetra:F0} Tf\n1 0 0 1 {hoja.Margen:F0} {y:F0} Tm\n({Escapar(texto)}) Tj\n");
            y -= hoja.AltoLinea;
        }

        if (numerar)
        {
            flujo.Append(CultureInfo.InvariantCulture,
                $"/F1 {hoja.TamanoLetra:F0} Tf\n1 0 0 1 {hoja.Ancho - hoja.Margen - 80:F0} {hoja.Margen:F0} Tm\n(Página {pagina} de {total}) Tj\n");
        }

        flujo.Append("ET");
        return flujo.ToString();
    }

    /// <summary>Texto para el PDF: se escapan los paréntesis y las tildes se pasan a WinAnsi.</summary>
    private static string Escapar(string texto)
    {
        var limpio = (texto ?? string.Empty).Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
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
