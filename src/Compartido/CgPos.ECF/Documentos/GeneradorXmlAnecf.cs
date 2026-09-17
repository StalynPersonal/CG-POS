using System.Globalization;
using System.Text;
using System.Xml;

namespace CgPos.ECF.Documentos;

/// <summary>Rango de e-NCF de un mismo tipo que se anula; <paramref name="Desde"/> y <paramref name="Hasta"/> son e-NCF completos (ej. E320000000101).</summary>
public sealed record RangoAnulacionEcf(int TipoEcf, string Desde, string Hasta)
{
    /// <summary>Cantidad de e-NCF del rango, ambos extremos incluidos.</summary>
    public long Cantidad => long.Parse(Hasta[3..], CultureInfo.InvariantCulture) - long.Parse(Desde[3..], CultureInfo.InvariantCulture) + 1;
}

/// <summary>
/// Anulación de e-NCF no utilizados (ANECF, esquema "ANECF v.1.0" de la DGII): informa rangos de secuencias que el emisor no usará, por ejemplo
/// los que quedaron en una caja dañada o en un rango vencido. Se firma con el certificado del emisor antes de enviarla.
/// </summary>
public static class GeneradorXmlAnecf
{
    /// <summary>Máximo de rangos por anulación que admite el esquema.</summary>
    public const int MaximoRangos = 10;

    public static string Generar(string rncEmisor, DateTimeOffset fechaHora, IReadOnlyList<RangoAnulacionEcf> rangos)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rncEmisor);
        ArgumentNullException.ThrowIfNull(rangos);
        if (rangos.Count is 0 or > MaximoRangos)
            throw new ArgumentException($"Una anulación lleva de 1 a {MaximoRangos} rangos.", nameof(rangos));

        foreach (var rango in rangos)
        {
            if (rango.Desde.Length != 13 || rango.Hasta.Length != 13 || rango.Desde[..3] != rango.Hasta[..3]
                || rango.Desde[1..3] != rango.TipoEcf.ToString("00", CultureInfo.InvariantCulture) || rango.Cantidad < 1)
                throw new ArgumentException($"El rango {rango.Desde} – {rango.Hasta} no es válido para el e-CF {rango.TipoEcf}.", nameof(rangos));
        }

        var texto = new StringBuilder();
        using (var xml = XmlWriter.Create(texto, new XmlWriterSettings { Indent = false, OmitXmlDeclaration = false }))
        {
            xml.WriteStartDocument();
            xml.WriteStartElement("ANECF");

            xml.WriteStartElement("Encabezado");
            xml.WriteElementString("Version", "1.0");
            xml.WriteElementString("RncEmisor", rncEmisor.Trim());
            xml.WriteElementString("CantidadeNCFAnulados", rangos.Sum(r => r.Cantidad).ToString(CultureInfo.InvariantCulture));
            xml.WriteElementString("FechaHoraAnulacioneNCF", GeneradorXmlEcf.FechaHora(fechaHora));
            xml.WriteEndElement();

            xml.WriteStartElement("DetalleAnulacion");
            var linea = 0;
            foreach (var tipo in rangos.GroupBy(r => r.TipoEcf).OrderBy(g => g.Key))
            {
                xml.WriteStartElement("Anulacion");
                xml.WriteElementString("NoLinea", (++linea).ToString(CultureInfo.InvariantCulture));
                xml.WriteElementString("TipoeCF", tipo.Key.ToString("00", CultureInfo.InvariantCulture));
                xml.WriteStartElement("TablaRangoSecuenciasAnuladaseNCF");
                foreach (var rango in tipo.OrderBy(r => r.Desde, StringComparer.Ordinal))
                {
                    xml.WriteStartElement("Secuencias");
                    xml.WriteElementString("SecuenciaeNCFDesde", rango.Desde);
                    xml.WriteElementString("SecuenciaeNCFHasta", rango.Hasta);
                    xml.WriteEndElement();
                }

                xml.WriteEndElement();
                xml.WriteElementString("CantidadeNCFAnulados", tipo.Sum(r => r.Cantidad).ToString(CultureInfo.InvariantCulture));
                xml.WriteEndElement();
            }

            xml.WriteEndElement(); // DetalleAnulacion
            xml.WriteEndElement(); // ANECF
            xml.WriteEndDocument();
        }

        // XmlWriter sobre StringBuilder declara UTF-16: la DGII recibe UTF-8.
        return texto.ToString().Replace("encoding=\"utf-16\"", "encoding=\"utf-8\"", StringComparison.Ordinal);
    }
}
