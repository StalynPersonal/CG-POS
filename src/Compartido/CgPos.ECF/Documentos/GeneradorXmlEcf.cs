using System.Globalization;
using System.Text;
using System.Xml;

namespace CgPos.ECF.Documentos;

/// <summary>
/// Genera el XML sin firmar del e-CF con formatos invariantes: montos a 2 decimales, fechas dd-MM-yyyy y fecha de firma
/// dd-MM-yyyy HH:mm:ss en hora de República Dominicana. La firma se agrega después con <see cref="Firma.FirmadorEcf"/>.
/// </summary>
public static class GeneradorXmlEcf
{
    public const string Version = "1.0";
    public const int LargoMaximoNombreItem = 80;

    private static readonly TimeSpan HoraDominicana = TimeSpan.FromHours(-4);

    /// <summary>Tipos en los que la DGII no exige fecha de vencimiento de la secuencia.</summary>
    private static readonly int[] SinVencimiento = [32, 34];

    public static string Generar(DocumentoEcf documento)
    {
        ArgumentNullException.ThrowIfNull(documento);

        var ajustes = new XmlWriterSettings { Encoding = new UTF8Encoding(false), Indent = false };
        using var texto = new EscritorUtf8();
        using (var xml = XmlWriter.Create(texto, ajustes))
        {
            void Elemento(string nombre, string valor) => xml.WriteElementString(nombre, valor);
            void Monto(string nombre, decimal valor) => Elemento(nombre, FormatoMonto(valor));

            xml.WriteStartDocument();
            xml.WriteStartElement("ECF");
            xml.WriteStartElement("Encabezado");
            Elemento("Version", Version);

            xml.WriteStartElement("IdDoc");
            Elemento("TipoeCF", documento.TipoEcf.ToString(CultureInfo.InvariantCulture));
            Elemento("eNCF", documento.Encf);
            if (documento.FechaVencimientoSecuencia is { } vencimiento && !SinVencimiento.Contains(documento.TipoEcf))
                Elemento("FechaVencimientoSecuencia", vencimiento.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture));
            Elemento("IndicadorMontoGravado", "0");
            Elemento("TipoIngresos", documento.TipoIngresos.ToString("00", CultureInfo.InvariantCulture));
            Elemento("TipoPago", documento.TipoPago.ToString(CultureInfo.InvariantCulture));
            if (documento.FormasPago is { Count: > 0 } formas)
            {
                xml.WriteStartElement("TablaFormasPago");
                foreach (var forma in formas)
                {
                    xml.WriteStartElement("FormaDePago");
                    Elemento("FormaPago", forma.FormaPago.ToString(CultureInfo.InvariantCulture));
                    Monto("MontoPago", forma.Monto);
                    xml.WriteEndElement();
                }
                xml.WriteEndElement();
            }
            xml.WriteEndElement(); // IdDoc

            xml.WriteStartElement("Emisor");
            Elemento("RNCEmisor", documento.Emisor.Rnc);
            Elemento("RazonSocialEmisor", documento.Emisor.RazonSocial);
            if (!string.IsNullOrWhiteSpace(documento.Emisor.NombreComercial))
                Elemento("NombreComercial", documento.Emisor.NombreComercial);
            if (!string.IsNullOrWhiteSpace(documento.Emisor.Sucursal))
                Elemento("Sucursal", documento.Emisor.Sucursal);
            Elemento("DireccionEmisor", documento.Emisor.Direccion);
            Elemento("FechaEmision", Fecha(documento.FechaEmision));
            xml.WriteEndElement(); // Emisor

            if (documento.Comprador is { Rnc: { Length: > 0 } rncComprador } comprador)
            {
                xml.WriteStartElement("Comprador");
                Elemento("RNCComprador", rncComprador);
                if (!string.IsNullOrWhiteSpace(comprador.RazonSocial))
                    Elemento("RazonSocialComprador", comprador.RazonSocial);
                xml.WriteEndElement();
            }

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
            if (totales.MontoGravadoI1 > 0)
                Elemento("ITBIS1", TotalesEcf.TasaItbis1.ToString("0", CultureInfo.InvariantCulture));
            if (totales.MontoGravadoI2 > 0)
                Elemento("ITBIS2", TotalesEcf.TasaItbis2.ToString("0", CultureInfo.InvariantCulture));
            if (totales.MontoGravadoI3 > 0)
                Elemento("ITBIS3", TotalesEcf.TasaItbis3.ToString("0", CultureInfo.InvariantCulture));
            if (totales.MontoGravadoTotal > 0)
                Monto("TotalITBIS", totales.TotalItbis);
            if (totales.MontoGravadoI1 > 0)
                Monto("TotalITBIS1", totales.TotalItbis1);
            if (totales.MontoGravadoI2 > 0)
                Monto("TotalITBIS2", totales.TotalItbis2);
            if (totales.MontoGravadoI3 > 0)
                Monto("TotalITBIS3", totales.TotalItbis3);
            Monto("MontoTotal", totales.MontoTotal);
            xml.WriteEndElement(); // Totales

            xml.WriteEndElement(); // Encabezado

            xml.WriteStartElement("DetallesItems");
            foreach (var item in documento.Items)
            {
                xml.WriteStartElement("Item");
                Elemento("NumeroLinea", item.NumeroLinea.ToString(CultureInfo.InvariantCulture));
                Elemento("IndicadorFacturacion", item.IndicadorFacturacion.ToString(CultureInfo.InvariantCulture));
                Elemento("NombreItem", item.Nombre.Length > LargoMaximoNombreItem ? item.Nombre[..LargoMaximoNombreItem] : item.Nombre);
                Elemento("IndicadorBienoServicio", item.IndicadorBienServicio.ToString(CultureInfo.InvariantCulture));
                Elemento("CantidadItem", item.Cantidad.ToString("0.00##", CultureInfo.InvariantCulture));
                if (!string.IsNullOrWhiteSpace(item.UnidadMedida))
                    Elemento("UnidadMedida", item.UnidadMedida);
                Elemento("PrecioUnitarioItem", item.PrecioUnitario.ToString("0.00##", CultureInfo.InvariantCulture));
                if (item.Descuento > 0)
                    Monto("DescuentoMonto", item.Descuento);
                Monto("MontoItem", item.Monto);
                xml.WriteEndElement();
            }
            xml.WriteEndElement(); // DetallesItems

            if (documento.Referencia is { } referencia)
            {
                xml.WriteStartElement("InformacionReferencia");
                Elemento("NCFModificado", referencia.NcfModificado);
                Elemento("FechaNCFModificado", referencia.FechaNcfModificado.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture));
                Elemento("CodigoModificacion", referencia.CodigoModificacion.ToString(CultureInfo.InvariantCulture));
                xml.WriteEndElement();
            }

            Elemento("FechaHoraFirma", FechaHora(documento.FechaHoraFirma));
            xml.WriteEndElement(); // ECF
            xml.WriteEndDocument();
        }

        return texto.ToString();
    }

    public static string FormatoMonto(decimal valor) => decimal.Round(valor, 2, MidpointRounding.AwayFromZero).ToString("0.00", CultureInfo.InvariantCulture);

    public static string Fecha(DateTimeOffset fecha) => fecha.ToOffset(HoraDominicana).ToString("dd-MM-yyyy", CultureInfo.InvariantCulture);

    public static string FechaHora(DateTimeOffset fecha) => fecha.ToOffset(HoraDominicana).ToString("dd-MM-yyyy HH:mm:ss", CultureInfo.InvariantCulture);

    /// <summary>StringWriter que declara UTF-8 en el encabezado del XML.</summary>
    private sealed class EscritorUtf8 : StringWriter
    {
        public EscritorUtf8() : base(CultureInfo.InvariantCulture)
        {
        }

        public override Encoding Encoding => new UTF8Encoding(false);
    }
}
