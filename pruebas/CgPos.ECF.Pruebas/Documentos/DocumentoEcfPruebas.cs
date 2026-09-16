using System.Xml.Linq;
using CgPos.ECF.Documentos;
using CgPos.ECF.Firma;
using Validador = CgPos.ECF.Documentos.ValidadorEcf;

namespace CgPos.ECF.Pruebas.Documentos;

public class DocumentoEcfPruebas
{
    private static readonly DateTimeOffset Emision = new(2026, 9, 15, 14, 30, 5, TimeSpan.FromHours(-4));

    /// <summary>Monto de identificación de la factura de consumo que configura el negocio (dato de la prueba).</summary>
    private const decimal MontoIdentificacion = 250_000m;

    private static IReadOnlyList<string> Validar(DocumentoEcf documento) => Validador.Validar(documento, MontoIdentificacion);

    /// <summary>Consumo: cincel con ITBIS 18 % (850 con ITBIS, 50 de descuento) y tomate exento.</summary>
    private static DocumentoEcf Consumo(decimal? montoCincel = null) =>
        new(
            TipoEcf: 32,
            Encf: "E320000000123",
            FechaVencimientoSecuencia: new DateOnly(2027, 12, 31),
            Emisor: new EmisorEcf("999000001", "CONTRERAS GROUP SRL", "Ferretería Contreras", "Sucursal Principal", "Av. Ficticia 100, Santo Domingo"),
            Comprador: null,
            Items:
            [
                new ItemEcf(1, 1, "Cincel de Punta SDS MAX", 1m, "UND", 720.34m, 42.37m, montoCincel ?? 677.97m),
                new ItemEcf(2, 4, "Tomate de ensalada", 2.325m, "LB", 45m, 0m, 104.63m),
            ],
            Totales: new TotalesEcf(677.97m, 0m, 0m, 104.63m, 122.03m, 0m, 0m, 904.63m, TasaItbis1: 18m),
            FechaEmision: Emision,
            FechaHoraFirma: Emision.AddSeconds(2),
            TipoIngresos: 1,
            TipoPago: 1,
            FormasPago: [new FormaPagoEcf(1, 904.63m)]);

    [Fact]
    public void El_resumen_de_consumo_lleva_los_totales_y_el_codigo_de_seguridad_y_cumple_su_esquema()
    {
        var carpeta = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "datos", "xsd"));
        Assert.True(Directory.Exists(carpeta), $"No están los esquemas de la DGII en {carpeta}.");

        using var certificado = CertificadoFirma.CargarPkcs12(CertificadoFirma.CrearAutofirmadoDesarrollo("CN=CAJA PRUEBA", "1234",
            DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1)), "1234");
        var firmadoEcf = new FirmadorEcf().Firmar(GeneradorXmlEcf.Generar(Consumo()), certificado);
        var codigoSeguridad = CodigoSeguridadEcf.Obtener(firmadoEcf);

        var resumen = GeneradorXmlRfce.Generar(Consumo(), codigoSeguridad);
        var xml = XDocument.Parse(resumen);
        var encabezado = xml.Root!.Element("Encabezado")!;

        Assert.Equal("RFCE", xml.Root!.Name.LocalName);
        Assert.Equal("E320000000123", encabezado.Element("IdDoc")!.Element("eNCF")!.Value);
        Assert.Equal("904.63", encabezado.Element("Totales")!.Element("MontoTotal")!.Value);
        Assert.Equal(codigoSeguridad, encabezado.Element("CodigoSeguridadeCF")!.Value);

        // El resumen no lleva las líneas: solo los totales y el código de seguridad del e-CF.
        Assert.Null(encabezado.Element("DetallesItems"));

        var firmado = new FirmadorEcf().Firmar(resumen, certificado);
        Assert.Empty(ValidadorEcf.ValidarContraXsd(firmado, carpeta, tipoEcf: null, nombreEsquema: "RFCE"));
    }

    [Fact]
    public void El_xml_firmado_cumple_el_esquema_oficial_de_la_dgii()
    {
        // Los XSD oficiales viven en datos/xsd; el esquema exige la firma, así que se valida el documento ya firmado.
        var carpeta = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "datos", "xsd"));
        Assert.True(Directory.Exists(carpeta), $"No están los esquemas de la DGII en {carpeta}.");

        using var certificado = CertificadoFirma.CargarPkcs12(CertificadoFirma.CrearAutofirmadoDesarrollo("CN=CAJA PRUEBA", "1234",
            DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1)), "1234");
        var firmado = new FirmadorEcf().Firmar(GeneradorXmlEcf.Generar(Consumo()), certificado);

        Assert.Empty(ValidadorEcf.ValidarContraXsd(firmado, carpeta, 32));
    }

    [Fact]
    public void Genera_el_xml_del_e_cf_con_estructura_y_formatos_de_la_dgii()
    {
        var xml = XDocument.Parse(GeneradorXmlEcf.Generar(Consumo()));
        var encabezado = xml.Root!.Element("Encabezado")!;

        Assert.Equal("ECF", xml.Root.Name.LocalName);
        Assert.Equal("1.0", encabezado.Element("Version")!.Value);
        Assert.Equal("32", encabezado.Element("IdDoc")!.Element("TipoeCF")!.Value);
        Assert.Equal("E320000000123", encabezado.Element("IdDoc")!.Element("eNCF")!.Value);
        Assert.Null(encabezado.Element("IdDoc")!.Element("FechaVencimientoSecuencia")); // no se exige en consumo
        Assert.Equal("15-09-2026", encabezado.Element("Emisor")!.Element("FechaEmision")!.Value);
        // El esquema de la DGII exige el bloque del comprador: en consumo sin identificar va vacío.
        Assert.NotNull(encabezado.Element("Comprador"));
        Assert.False(encabezado.Element("Comprador")!.HasElements);

        var totales = encabezado.Element("Totales")!;
        Assert.Equal("677.97", totales.Element("MontoGravadoI1")!.Value);
        Assert.Equal("104.63", totales.Element("MontoExento")!.Value);
        Assert.Equal("18", totales.Element("ITBIS1")!.Value);
        Assert.Equal("122.03", totales.Element("TotalITBIS")!.Value);
        Assert.Equal("904.63", totales.Element("MontoTotal")!.Value);

        var items = xml.Root.Element("DetallesItems")!.Elements("Item").ToList();
        Assert.Equal(2, items.Count);
        Assert.Equal("42.37", items[0].Element("DescuentoMonto")!.Value);
        // El esquema admite dos decimales en la cantidad: un pesado de 2.325 lb se informa redondeado.
        Assert.Equal("2.33", items[1].Element("CantidadItem")!.Value);
        Assert.Equal("23", items[1].Element("UnidadMedida")!.Value); // LB en la tabla de la DGII
        Assert.Null(items[1].Element("DescuentoMonto"));

        Assert.Equal("15-09-2026 14:30:07", xml.Root.Element("FechaHoraFirma")!.Value);
        Assert.Equal("FechaHoraFirma", xml.Root.Elements().Last().Name.LocalName);
    }

    [Fact]
    public void Validador_acepta_un_documento_coherente_y_explica_cada_error()
    {
        Assert.Empty(Validar(Consumo()));

        var descuadrado = Validar(Consumo(montoCincel: 700m));
        Assert.Contains(descuadrado, e => e.Contains("MontoGravadoI1"));

        var creditoSinComprador = Consumo() with { TipoEcf = 31, Encf = "E310000000001" };
        Assert.Contains(Validar(creditoSinComprador), e => e.Contains("requiere el RNC o la cédula"));

        var encfDeOtroTipo = Consumo() with { Encf = "E310000000001" };
        Assert.Contains(Validar(encfDeOtroTipo), e => e.Contains("no corresponde al tipo"));

        var notaSinReferencia = Consumo() with { TipoEcf = 34, Encf = "E340000000001", Comprador = new CompradorEcf("131246796", "Constructora") };
        Assert.Contains(Validar(notaSinReferencia), e => e.Contains("referencia"));

        var gubernamentalConCedula = Consumo() with { TipoEcf = 45, Encf = "E450000000001", Comprador = new CompradorEcf("00113918205", "Persona") };
        Assert.Contains(Validar(gubernamentalConCedula), e => e.Contains("gubernamental"));
    }

    [Fact]
    public void El_xml_generado_se_firma_se_verifica_y_da_codigo_de_seguridad()
    {
        const string pin = "PIN-Caja-01";
        var pkcs12 = CertificadoFirma.CrearAutofirmadoDesarrollo("CN=CAJA 01 DESARROLLO, O=Contreras Group, C=DO", pin,
            DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
        using var certificado = CertificadoFirma.CargarPkcs12(pkcs12, pin);

        var firmado = new FirmadorEcf().Firmar(GeneradorXmlEcf.Generar(Consumo()), certificado);

        Assert.True(VerificadorFirmaEcf.Verificar(firmado).EsValida);
        Assert.Equal(CodigoSeguridadEcf.Largo, CodigoSeguridadEcf.Obtener(firmado).Length);
    }

    [Fact]
    public void Url_del_timbre_usa_la_consulta_simplificada_en_consumo_menor_y_la_completa_en_los_demas()
    {
        var consumo = TimbreEcf.Url(AmbienteEcf.Pruebas, Consumo(), "Ab+9/z", MontoIdentificacion);
        // La consulta simplificada vive en el servicio de facturas de consumo de la DGII, con su propio host.
        Assert.StartsWith("https://fc.dgii.gov.do/testecf/ConsultaTimbreFC?", consumo);
        Assert.Contains("ENCF=E320000000123", consumo);
        Assert.Contains("MontoTotal=904.63", consumo);
        Assert.Contains("CodigoSeguridad=Ab%2B9%2Fz", consumo);

        var credito = Consumo() with { TipoEcf = 31, Encf = "E310000000001", Comprador = new CompradorEcf("131246796", "Constructora") };
        var completa = TimbreEcf.Url(AmbienteEcf.Produccion, credito, "Ab+9/z", MontoIdentificacion);
        Assert.StartsWith("https://ecf.dgii.gov.do/ecf/ConsultaTimbre?", completa);
        Assert.Contains("RncComprador=131246796", completa);
        Assert.Contains("FechaFirma=15-09-2026%2014%3A30%3A07", completa);
    }

    [Fact]
    public void Sin_esquemas_xsd_la_validacion_contra_xsd_no_reporta_errores()
    {
        Assert.Empty(ValidadorEcf.ValidarContraXsd(GeneradorXmlEcf.Generar(Consumo()), Path.Combine(Path.GetTempPath(), $"sin-xsd-{Guid.NewGuid():N}")));
    }
}
