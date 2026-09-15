using System.Security.Cryptography.X509Certificates;

namespace CgPos.ECF.Firma;

/// <summary>Carga del certificado digital de la caja (.p12/.pfx) protegido por su PIN.</summary>
public static class CertificadoFirma
{
    /// <summary>
    /// Carga el certificado con su clave privada. <see cref="X509KeyStorageFlags.EphemeralKeySet"/> mantiene la clave
    /// solo en memoria: no se escribe en el almacén de Windows ni en disco (RNF-17).
    /// </summary>
    /// <exception cref="System.Security.Cryptography.CryptographicException">PIN incorrecto o archivo inválido.</exception>
    public static X509Certificate2 CargarPkcs12(ReadOnlySpan<byte> contenido, string pin)
    {
        var certificado = X509CertificateLoader.LoadPkcs12(contenido, pin, X509KeyStorageFlags.EphemeralKeySet);

        if (!certificado.HasPrivateKey)
        {
            certificado.Dispose();
            throw new InvalidOperationException("El certificado no contiene la clave privada necesaria para firmar.");
        }

        return certificado;
    }

    public static X509Certificate2 CargarPkcs12DesdeArchivo(string ruta, string pin) =>
        CargarPkcs12(File.ReadAllBytes(ruta), pin);

    /// <summary>
    /// Crea un certificado autofirmado RSA-2048 en formato .p12. Solo para desarrollo y pruebas: el certificado real de cada
    /// caja lo emite una entidad certificadora autorizada por la DGII.
    /// </summary>
    public static byte[] CrearAutofirmadoDesarrollo(string sujeto, string pin, DateTimeOffset desde, DateTimeOffset hasta)
    {
        using var rsa = System.Security.Cryptography.RSA.Create(2048);
        var solicitud = new System.Security.Cryptography.X509Certificates.CertificateRequest(
            sujeto, rsa, System.Security.Cryptography.HashAlgorithmName.SHA256, System.Security.Cryptography.RSASignaturePadding.Pkcs1);
        solicitud.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.NonRepudiation, critical: true));

        using var certificado = solicitud.CreateSelfSigned(desde, hasta);
        return certificado.Export(X509ContentType.Pkcs12, pin);
    }
}
