using System.Security.Cryptography.Xml;

namespace CgPos.ECF.Firma;

/// <summary>
/// Código de seguridad del e-CF que se imprime en la representación impresa y en el QR:
/// los primeros 6 caracteres del SignatureValue.
/// Pendiente confirmar contra la documentación oficial de la DGII en la Fase C7.
/// </summary>
public static class CodigoSeguridadEcf
{
    public const int Largo = 6;

    public static string Obtener(string xmlFirmado)
    {
        var documento = XmlSeguro.Cargar(xmlFirmado);

        var valorFirma = documento.GetElementsByTagName("SignatureValue", SignedXml.XmlDsigNamespaceUrl)
            .Cast<System.Xml.XmlNode>()
            .SingleOrDefault()?.InnerText
            ?? throw new InvalidOperationException("El documento no está firmado.");

        var limpio = string.Concat(valorFirma.Where(c => !char.IsWhiteSpace(c)));
        return limpio[..Largo];
    }
}
