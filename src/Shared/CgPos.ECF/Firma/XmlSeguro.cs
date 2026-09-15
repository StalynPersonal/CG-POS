using System.Xml;

namespace CgPos.ECF.Firma;

/// <summary>Carga de XML endurecida: sin DTD ni resolución de entidades externas (evita XXE) y conservando espacios.</summary>
internal static class XmlSeguro
{
    public static XmlDocument Cargar(string xml)
    {
        var configuracion = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
        };

        // PreserveWhitespace es obligatorio: la firma se calcula sobre el documento tal cual, con sus espacios.
        var documento = new XmlDocument { PreserveWhitespace = true, XmlResolver = null };

        using var texto = new StringReader(xml);
        using var lector = XmlReader.Create(texto, configuracion);
        documento.Load(lector);
        return documento;
    }
}
