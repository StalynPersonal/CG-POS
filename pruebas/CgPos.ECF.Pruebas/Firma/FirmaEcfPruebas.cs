using System.Diagnostics;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Xml;
using System.Xml.Linq;
using CgPos.ECF.Firma;
using Xunit.Abstractions;

namespace CgPos.ECF.Pruebas.Firma;

public class FirmaEcfPruebas(ITestOutputHelper salida)
{
    private const string Pin = "1234-Prueba";
    private static readonly byte[] Pkcs12 = CertificadoPrueba.CrearPkcs12(Pin);
    private static readonly XNamespace Ds = "http://www.w3.org/2000/09/xmldsig#";

    private readonly FirmadorEcf _firmador = new();

    private static X509Certificate2 CargarCertificado() => CertificadoFirma.CargarPkcs12(Pkcs12, Pin);

    [Fact]
    public void Documento_firmado_se_verifica_como_valido()
    {
        using var certificado = CargarCertificado();

        var firmado = _firmador.Firmar(XmlEjemploEcf.E31, certificado);
        var resultado = VerificadorFirmaEcf.Verificar(firmado);

        Assert.True(resultado.EsValida, resultado.Motivo);
        Assert.Equal(certificado.Thumbprint, resultado.Certificado!.Thumbprint);
    }

    [Fact]
    public void Cambiar_el_monto_despues_de_firmar_invalida_la_firma()
    {
        using var certificado = CargarCertificado();
        var firmado = _firmador.Firmar(XmlEjemploEcf.E31, certificado);

        var alterado = firmado.Replace("<MontoTotal>850.00</MontoTotal>", "<MontoTotal>85.00</MontoTotal>");

        Assert.NotEqual(firmado, alterado);
        var resultado = VerificadorFirmaEcf.Verificar(alterado);
        Assert.False(resultado.EsValida);
        Assert.Contains("modificado", resultado.Motivo);
    }

    [Fact]
    public void Cambiar_un_solo_espacio_tambien_invalida_la_firma()
    {
        using var certificado = CargarCertificado();
        var firmado = _firmador.Firmar(XmlEjemploEcf.E31, certificado);

        var alterado = firmado.Replace("CONTRERAS GROUP SRL", "CONTRERAS  GROUP SRL");

        Assert.False(VerificadorFirmaEcf.Verificar(alterado).EsValida);
    }

    [Fact]
    public void Firma_usa_los_algoritmos_y_la_estructura_esperados()
    {
        using var certificado = CargarCertificado();
        var firmado = XDocument.Parse(_firmador.Firmar(XmlEjemploEcf.E31, certificado));

        var raiz = firmado.Root!;
        var firma = Assert.Single(raiz.Descendants(Ds + "Signature"));
        Assert.Same(raiz.Elements().Last(), firma); // enveloped: último hijo de la raíz

        var infoFirmada = firma.Element(Ds + "SignedInfo")!;
        Assert.Equal("http://www.w3.org/TR/2001/REC-xml-c14n-20010315", infoFirmada.Element(Ds + "CanonicalizationMethod")!.Attribute("Algorithm")!.Value);
        Assert.Equal("http://www.w3.org/2001/04/xmldsig-more#rsa-sha256", infoFirmada.Element(Ds + "SignatureMethod")!.Attribute("Algorithm")!.Value);

        var referencia = Assert.Single(infoFirmada.Elements(Ds + "Reference"));
        Assert.Equal(string.Empty, referencia.Attribute("URI")!.Value);
        Assert.Equal("http://www.w3.org/2000/09/xmldsig#enveloped-signature", referencia.Descendants(Ds + "Transform").Single().Attribute("Algorithm")!.Value);
        Assert.Equal("http://www.w3.org/2001/04/xmlenc#sha256", referencia.Element(Ds + "DigestMethod")!.Attribute("Algorithm")!.Value);

        var certificadoIncluido = firma.Descendants(Ds + "X509Certificate").Single().Value;
        Assert.Equal(Convert.ToBase64String(certificado.RawData), certificadoIncluido);
    }

    [Fact]
    public void Firmar_no_altera_los_datos_del_documento()
    {
        using var certificado = CargarCertificado();
        var original = XDocument.Parse(XmlEjemploEcf.E31);
        var firmado = XDocument.Parse(_firmador.Firmar(XmlEjemploEcf.E31, certificado));

        firmado.Root!.Element(Ds + "Signature")!.Remove();

        Assert.True(XNode.DeepEquals(original.Root, firmado.Root));
    }

    [Fact]
    public void Codigo_de_seguridad_son_los_primeros_6_caracteres_del_SignatureValue()
    {
        using var certificado = CargarCertificado();
        var firmado = _firmador.Firmar(XmlEjemploEcf.E31, certificado);

        var valorFirma = XDocument.Parse(firmado).Descendants(Ds + "SignatureValue").Single().Value;
        var codigo = CodigoSeguridadEcf.Obtener(firmado);

        Assert.Equal(CodigoSeguridadEcf.Largo, codigo.Length);
        Assert.Equal(valorFirma[..6], codigo);
        salida.WriteLine($"Código de seguridad: {codigo}");
    }

    [Fact]
    public void Documento_sin_firma_no_es_valido()
    {
        var resultado = VerificadorFirmaEcf.Verificar(XmlEjemploEcf.E31);

        Assert.False(resultado.EsValida);
        Assert.Equal("El documento no está firmado.", resultado.Motivo);
    }

    [Fact]
    public void Documento_ya_firmado_no_se_vuelve_a_firmar()
    {
        using var certificado = CargarCertificado();
        var firmado = _firmador.Firmar(XmlEjemploEcf.E31, certificado);

        Assert.Throws<InvalidOperationException>(() => _firmador.Firmar(firmado, certificado));
    }

    [Fact]
    public void PIN_incorrecto_no_carga_el_certificado()
    {
        Assert.ThrowsAny<CryptographicException>(() => CertificadoFirma.CargarPkcs12(Pkcs12, "PIN-equivocado"));
    }

    [Fact]
    public void Certificado_sin_clave_privada_no_puede_firmar()
    {
        using var conClave = CargarCertificado();
        using var soloPublico = X509CertificateLoader.LoadCertificate(conClave.RawData);

        Assert.Throws<ArgumentException>(() => _firmador.Firmar(XmlEjemploEcf.E31, soloPublico));
    }

    [Fact]
    public void XML_con_DTD_se_rechaza()
    {
        using var certificado = CargarCertificado();
        const string conDtd = """<?xml version="1.0"?><!DOCTYPE ECF [<!ENTITY x SYSTEM "file:///c:/windows/win.ini">]><ECF>&x;</ECF>""";

        Assert.Throws<XmlException>(() => _firmador.Firmar(conDtd, certificado));
    }

    [Fact]
    public void Firmar_y_verificar_es_rapido()
    {
        using var certificado = CargarCertificado();
        _firmador.Firmar(XmlEjemploEcf.E31, certificado); // calentamiento (JIT)

        const int repeticiones = 50;
        var cronometro = Stopwatch.StartNew();
        for (var i = 0; i < repeticiones; i++)
        {
            var firmado = _firmador.Firmar(XmlEjemploEcf.E31, certificado);
            Assert.True(VerificadorFirmaEcf.Verificar(firmado).EsValida);
        }
        cronometro.Stop();

        var promedioMs = cronometro.Elapsed.TotalMilliseconds / repeticiones;
        salida.WriteLine($"Firmar + verificar: {promedioMs:F2} ms promedio ({repeticiones} repeticiones)");

        // Holgado a propósito: el objetivo real es imprimir en <= 3 s (RNF-19); la firma debe ser una fracción mínima.
        Assert.True(promedioMs < 200, $"Promedio {promedioMs:F2} ms");
    }
}
