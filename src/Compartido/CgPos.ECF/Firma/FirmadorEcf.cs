using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;

namespace CgPos.ECF.Firma;

/// <summary>
/// Firma un e-CF con XMLDSig "enveloped" según el esquema de la DGII:
/// RSA-SHA256, canonicalización C14N, digest SHA-256, Reference URI="" (documento completo)
/// y el certificado en KeyInfo/X509Data. La firma se agrega como último hijo de la raíz.
/// </summary>
public sealed class FirmadorEcf
{
    /// <returns>El XML firmado.</returns>
    /// <exception cref="ArgumentException">El certificado no tiene clave privada RSA.</exception>
    /// <exception cref="InvalidOperationException">El documento ya está firmado.</exception>
    /// <exception cref="System.Xml.XmlException">XML mal formado o con DTD.</exception>
    public string Firmar(string xml, X509Certificate2 certificado)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xml);
        ArgumentNullException.ThrowIfNull(certificado);

        using var clavePrivada = certificado.GetRSAPrivateKey()
            ?? throw new ArgumentException("El certificado no tiene clave privada RSA.", nameof(certificado));

        var documento = XmlSeguro.Cargar(xml);
        var raiz = documento.DocumentElement
            ?? throw new ArgumentException("El XML no tiene elemento raíz.", nameof(xml));

        if (documento.GetElementsByTagName("Signature", SignedXml.XmlDsigNamespaceUrl).Count > 0)
            throw new InvalidOperationException("El documento ya está firmado.");

        var firma = new SignedXml(documento) { SigningKey = clavePrivada };
        firma.SignedInfo!.SignatureMethod = SignedXml.XmlDsigRSASHA256Url;
        firma.SignedInfo.CanonicalizationMethod = SignedXml.XmlDsigC14NTransformUrl;

        var referencia = new Reference(string.Empty) { DigestMethod = SignedXml.XmlDsigSHA256Url };
        referencia.AddTransform(new XmlDsigEnvelopedSignatureTransform());
        firma.AddReference(referencia);

        var infoClave = new KeyInfo();
        infoClave.AddClause(new KeyInfoX509Data(certificado));
        firma.KeyInfo = infoClave;

        firma.ComputeSignature();
        raiz.AppendChild(documento.ImportNode(firma.GetXml(), deep: true));

        return documento.OuterXml;
    }
}
