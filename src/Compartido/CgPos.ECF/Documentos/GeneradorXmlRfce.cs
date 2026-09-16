using System.Globalization;
using System.Text;
using System.Xml;

namespace CgPos.ECF.Documentos;

/// <summary>
/// Resumen de Factura de Consumo Electrónica (RFCE): lo que se le envía a la DGII cuando una factura de consumo (E32) no llega
/// al monto que obliga a identificar al comprador. Lleva los totales y el código de seguridad del e-CF, no sus líneas; el e-CF
/// completo se conserva en la caja y es el que se le entrega al cliente.
/// </summary>
public static class GeneradorXmlRfce
{
    /// <summary>Tipo de e-CF que se resume; la DGII solo admite el resumen para la factura de consumo.</summary>
    public const int TipoResumible = 32;

    /// <param name="codigoSeguridad">Los primeros caracteres de la firma del e-CF (<see cref="CodigoSeguridadEcf"/>).</param>
    public static string Generar(DocumentoEcf documento, string codigoSeguridad)
    {
        ArgumentNullException.ThrowIfNull(documento);
        ArgumentException.ThrowIfNullOrWhiteSpace(codigoSeguridad);
        if (documento.TipoEcf != TipoResumible)
            throw new ArgumentException($"El resumen de consumo solo aplica al e-CF {TipoResumible}.", nameof(documento));

        var texto = new StringBuilder();
        using (var xml = XmlWriter.Create(texto, new XmlWriterSettings { Indent = false, Encoding = Encoding.UTF8, OmitXmlDeclaration = false }))
        {
            void Elemento(string nombre, string? valor)
            {
                if (!string.IsNullOrWhiteSpace(valor))
                    xml.WriteElementString(nombre, valor);
            }

            void Monto(string nombre, decimal valor) => Elemento(nombre, valor.ToString("0.00", CultureInfo.InvariantCulture));

            xml.WriteStartDocument();
            xml.WriteStartElement("RFCE");
            xml.WriteStartElement("Encabezado");
            Elemento("Version", "1.0");

            xml.WriteStartElement("IdDoc");
            Elemento("TipoeCF", documento.TipoEcf.ToString("00", CultureInfo.InvariantCulture));
            Elemento("eNCF", documento.Encf);
            Elemento("TipoIngresos", documento.TipoIngresos.ToString("00", CultureInfo.InvariantCulture));
            Elemento("TipoPago", documento.TipoPago.ToString(CultureInfo.InvariantCulture));

            if (documento.FormasPago is { Count: > 0 } formas)
            {
                xml.WriteStartElement("TablaFormasPago");
                foreach (var forma in formas)
                {
                    xml.WriteStartElement("FormaDePago");
                    Elemento("FormaPago", forma.FormaPago.ToString("00", CultureInfo.InvariantCulture));
                    Monto("MontoPago", forma.Monto);
                    xml.WriteEndElement();
                }

                xml.WriteEndElement();
            }

            xml.WriteEndElement(); // IdDoc

            xml.WriteStartElement("Emisor");
            Elemento("RNCEmisor", documento.Emisor.Rnc);
            Elemento("RazonSocialEmisor", documento.Emisor.RazonSocial);
            Elemento("FechaEmision", GeneradorXmlEcf.Fecha(documento.FechaEmision));
            xml.WriteEndElement();

            // El esquema exige el bloque del comprador aunque el consumo no lo identifique.
            xml.WriteStartElement("Comprador");
            if (documento.Comprador is { } comprador)
            {
                if (comprador.Rnc is { Length: > 0 } rnc)
                    Elemento("RNCComprador", rnc);
                if (!string.IsNullOrWhiteSpace(comprador.RazonSocial))
                    Elemento("RazonSocialComprador", comprador.RazonSocial);
            }

            xml.WriteEndElement();

            var totales = documento.Totales;
            xml.WriteStartElement("Totales");
            if (totales.MontoGravadoTotal > 0)
                Monto("MontoGravadoTotal", totales.MontoGravadoTotal);
            if (totales.MontoGravadoI1 > 0)
                Monto("MontoGravadoI1", totales.MontoGravadoI1);
            if (totales.MontoGravadoI2 > 0)
                Monto("MontoGravadoI2", totales.MontoGravadoI2);
            if (totales.MontoGravadoI3 > 0)
                Monto("MontoGravadoI3", totales.MontoGravadoI3);
            if (totales.MontoExento > 0)
                Monto("MontoExento", totales.MontoExento);
            if (totales.TotalItbis > 0)
                Monto("TotalITBIS", totales.TotalItbis);
            if (totales.TotalItbis1 > 0)
                Monto("TotalITBIS1", totales.TotalItbis1);
            if (totales.TotalItbis2 > 0)
                Monto("TotalITBIS2", totales.TotalItbis2);
            if (totales.TotalItbis3 > 0)
                Monto("TotalITBIS3", totales.TotalItbis3);
            Monto("MontoTotal", totales.MontoTotal);
            xml.WriteEndElement();

            Elemento("CodigoSeguridadeCF", codigoSeguridad);
            xml.WriteEndElement(); // Encabezado
            xml.WriteEndElement(); // RFCE
            xml.WriteEndDocument();
        }

        return texto.ToString();
    }
}
