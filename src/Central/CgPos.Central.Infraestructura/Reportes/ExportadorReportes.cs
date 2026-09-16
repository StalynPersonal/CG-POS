using System.Globalization;
using System.IO.Compression;
using System.Text;
using CgPos.Central.Aplicacion.Reportes;
using CgPos.Contratos.Central;

namespace CgPos.Central.Infraestructura.Reportes;

/// <summary>
/// Genera los archivos de los reportes sin depender de librerías externas: el Central se instala en la empresa y no descarga nada.
/// El Excel es un .xlsx (OpenXML mínimo con textos en línea) y el PDF se arma con la fuente Courier incrustada en el visor.
/// </summary>
internal sealed class ExportadorReportes : IExportadorReportes
{
    private const string TipoExcel = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    public ArchivoReporte AExcel(TablaReporte tabla)
    {
        ArgumentNullException.ThrowIfNull(tabla);

        using var memoria = new MemoryStream();
        using (var paquete = new ZipArchive(memoria, ZipArchiveMode.Create, leaveOpen: true))
        {
            Escribir(paquete, "[Content_Types].xml", ContenidoTipos);
            Escribir(paquete, "_rels/.rels", RelacionesRaiz);
            Escribir(paquete, "xl/workbook.xml", Libro);
            Escribir(paquete, "xl/_rels/workbook.xml.rels", RelacionesLibro);
            Escribir(paquete, "xl/worksheets/sheet1.xml", Hoja(tabla));
        }

        return new ArchivoReporte($"{Nombre(tabla)}.xlsx", TipoExcel, memoria.ToArray());
    }

    public ArchivoReporte APdf(TablaReporte tabla)
    {
        ArgumentNullException.ThrowIfNull(tabla);
        return new ArchivoReporte($"{Nombre(tabla)}.pdf", "application/pdf", GeneradorPdf.Crear(tabla));
    }

    public ArchivoReporte A607(string rncEmisor, DateOnly periodo, IReadOnlyList<DatosFormato607> filas)
    {
        ArgumentNullException.ThrowIfNull(filas);
        var cultura = CultureInfo.InvariantCulture;
        var texto = new StringBuilder();

        // Encabezado del archivo de envío: RNC, periodo (AAAAMM) y cantidad de registros.
        texto.Append(CultureInfo.InvariantCulture, $"607|{rncEmisor}|{periodo:yyyyMM}|{filas.Count}\r\n");
        foreach (var fila in filas)
        {
            texto.Append(CultureInfo.InvariantCulture,
                $"{fila.Rnc}|{fila.TipoIdentificacion}|{fila.Encf}|{fila.EncfModificado}|{fila.Fecha:yyyyMMdd}|" +
                $"{fila.MontoFacturado.ToString("F2", cultura)}|{fila.ItbisFacturado.ToString("F2", cultura)}|{fila.ItbisRetenido.ToString("F2", cultura)}\r\n");
        }

        return new ArchivoReporte($"607-{periodo:yyyyMM}.txt", "text/plain; charset=utf-8", Encoding.UTF8.GetBytes(texto.ToString()));
    }

    private static string Nombre(TablaReporte tabla)
    {
        var limpio = new string(tabla.Titulo.Select(c => char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : '-').ToArray());
        return $"{limpio.Trim('-')}-{DateTime.Now:yyyyMMdd-HHmm}";
    }

    private static void Escribir(ZipArchive paquete, string ruta, string contenido)
    {
        using var flujo = paquete.CreateEntry(ruta, CompressionLevel.Optimal).Open();
        using var escritor = new StreamWriter(flujo, new UTF8Encoding(false));
        escritor.Write(contenido);
    }

    private const string ContenidoTipos = """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
          <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
          <Default Extension="xml" ContentType="application/xml"/>
          <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>
          <Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
        </Types>
        """;

    private const string RelacionesRaiz = """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
          <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>
        </Relationships>
        """;

    private const string Libro = """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"
                  xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
          <sheets><sheet name="Reporte" sheetId="1" r:id="rId1"/></sheets>
        </workbook>
        """;

    private const string RelacionesLibro = """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
          <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/>
        </Relationships>
        """;

    private static string Hoja(TablaReporte tabla)
    {
        var xml = new StringBuilder();
        xml.Append("""<?xml version="1.0" encoding="UTF-8" standalone="yes"?>""");
        xml.Append("""<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><sheetData>""");

        var numeroFila = 1;
        Fila(xml, numeroFila++, [tabla.Titulo]);
        Fila(xml, numeroFila++, [tabla.Subtitulo]);
        numeroFila++;
        Fila(xml, numeroFila++, tabla.Columnas);
        foreach (var fila in tabla.Filas)
            Fila(xml, numeroFila++, fila);
        if (tabla.Totales is { } totales)
            Fila(xml, numeroFila, totales);

        xml.Append("</sheetData></worksheet>");
        return xml.ToString();
    }

    private static void Fila(StringBuilder xml, int numero, IReadOnlyList<string> valores)
    {
        xml.Append(CultureInfo.InvariantCulture, $"<row r=\"{numero}\">");
        for (var columna = 0; columna < valores.Count; columna++)
        {
            var referencia = $"{Columna(columna)}{numero}";
            xml.Append(CultureInfo.InvariantCulture, $"<c r=\"{referencia}\" t=\"inlineStr\"><is><t xml:space=\"preserve\">{Escapar(valores[columna])}</t></is></c>");
        }

        xml.Append("</row>");
    }

    /// <summary>A, B… Z, AA, AB…</summary>
    private static string Columna(int indice)
    {
        var nombre = string.Empty;
        for (var resto = indice; resto >= 0; resto = resto / 26 - 1)
            nombre = (char)('A' + resto % 26) + nombre;

        return nombre;
    }

    private static string Escapar(string? texto) => (texto ?? string.Empty)
        .Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
}
