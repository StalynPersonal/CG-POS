using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Xml;

namespace CgPos.ECF.Firma;

public sealed record ResultadoVerificacionFirma(bool EsValida, string? Motivo, X509Certificate2? Certificado);

/// <summary>
/// Verifica la integridad de la firma de un e-CF. Exige una única firma, ubicada en la raíz, que cubra el documento
/// completo con RSA-SHA256 (evita ataques de "firma envuelta").
/// No valida la cadena de confianza del certificado ni su vigencia: eso corresponde a otra verificación.
/// </summary>
public static class VerificadorFirmaEcf
{
    public static ResultadoVerificacionFirma Verificar(string xmlFirmado)
    {
        XmlDocument documento;
        try
        {
            documento = XmlSeguro.Cargar(xmlFirmado);
        }
        catch (XmlException ex)
        {
            return Invalida($"XML mal formado: {ex.Message}");
        }

        var firmas = documento.GetElementsByTagName("Signature", SignedXml.XmlDsigNamespaceUrl);
        if (firmas.Count == 0)
            return Invalida("El documento no está firmado.");
        if (firmas.Count > 1)
            return Invalida("El documento tiene más de una firma.");

        var elementoFirma = (XmlElement)firmas[0]!;
        if (elementoFirma.ParentNode != documento.DocumentElement)
            return Invalida("La firma debe estar en la raíz del documento.");

        var firma = new SignedXml(documento);
        firma.LoadXml(elementoFirma);

        if (firma.SignatureMethod != SignedXml.XmlDsigRSASHA256Url)
            return Invalida($"Algoritmo de firma no permitido: {firma.SignatureMethod}.");

        var referencias = firma.SignedInfo!.References;
        if (referencias.Count != 1 || ((Reference)referencias[0]!).Uri != string.Empty)
            return Invalida("La firma debe cubrir el documento completo (Reference URI=\"\").");

        var certificado = firma.KeyInfo
            .OfType<KeyInfoX509Data>()
            .SelectMany(datos => datos.Certificates?.OfType<X509Certificate2>() ?? [])
            .FirstOrDefault();

        if (certificado is null)
            return Invalida("La firma no incluye el certificado (KeyInfo/X509Data).");

        return firma.CheckSignature(certificado, verifySignatureOnly: true)
            ? new ResultadoVerificacionFirma(true, null, certificado)
            : new ResultadoVerificacionFirma(false, "La firma no corresponde al contenido: el documento fue modificado o la firma es inválida.", certificado);
    }

    private static ResultadoVerificacionFirma Invalida(string motivo) => new(false, motivo, null);
}
