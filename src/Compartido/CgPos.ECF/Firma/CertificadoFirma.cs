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
}
