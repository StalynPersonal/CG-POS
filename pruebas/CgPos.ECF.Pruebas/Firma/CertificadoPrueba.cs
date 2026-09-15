using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace CgPos.ECF.Pruebas.Firma;

/// <summary>Genera un certificado autofirmado RSA-2048 exportado como .p12 con PIN (simula el certificado de una caja).</summary>
internal static class CertificadoPrueba
{
    public static byte[] CrearPkcs12(string pin)
    {
        using var rsa = RSA.Create(2048);
        var solicitud = new CertificateRequest(
            "CN=CAJA 01 PRUEBA, O=Contreras Group, C=DO", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        solicitud.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.NonRepudiation, critical: true));

        using var certificado = solicitud.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
        return certificado.Export(X509ContentType.Pkcs12, pin);
    }
}
