using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Schema;

namespace CgPos.ECF.Documentos;

/// <summary>
/// Validaciones del e-CF antes de firmarlo (RF-29): formato del eNCF, documento del comprador según el tipo, coherencia de
/// ítems y totales. La validación contra los XSD oficiales se aplica si los esquemas están disponibles en la caja.
/// </summary>
public static partial class ValidadorEcf
{
    public static readonly IReadOnlySet<int> TiposValidos = new HashSet<int> { 31, 32, 33, 34, 41, 43, 44, 45, 46, 47 };

    /// <summary>Monto desde el cual la factura de consumo debe identificar al comprador.</summary>
    public const decimal MontoIdentificacionConsumo = 250_000m;

    /// <summary>
    /// Tolerancia por redondeo entre la suma de líneas y los totales: el ITBIS se calcula por línea y en ventas largas su suma
    /// difiere unos centavos del total por tasa; también absorbe el redondeo del efectivo en las formas de pago.
    /// </summary>
    public const decimal Tolerancia = 1.00m;

    public static IReadOnlyList<string> Validar(DocumentoEcf documento)
    {
        ArgumentNullException.ThrowIfNull(documento);
        var errores = new List<string>();

        if (!TiposValidos.Contains(documento.TipoEcf))
            errores.Add($"Tipo de e-CF no válido: {documento.TipoEcf}.");
        if (!PatronEncf().IsMatch(documento.Encf))
            errores.Add($"El eNCF '{documento.Encf}' no tiene el formato E + tipo + 10 dígitos.");
        else if (documento.Encf.Substring(1, 2) != documento.TipoEcf.ToString("00"))
            errores.Add($"El eNCF '{documento.Encf}' no corresponde al tipo {documento.TipoEcf}.");

        if (!EsDocumento(documento.Emisor.Rnc))
            errores.Add("El RNC del emisor debe tener 9 u 11 dígitos.");
        if (string.IsNullOrWhiteSpace(documento.Emisor.RazonSocial))
            errores.Add("Falta la razón social del emisor.");
        if (string.IsNullOrWhiteSpace(documento.Emisor.Direccion))
            errores.Add("Falta la dirección del emisor.");

        var rncComprador = documento.Comprador?.Rnc;
        switch (documento.TipoEcf)
        {
            case 31 or 33 or 34 or 44 when !EsDocumento(rncComprador):
                errores.Add($"El e-CF tipo {documento.TipoEcf} requiere el RNC o la cédula del comprador.");
                break;
            case 45 when rncComprador is not { Length: 9 } || !rncComprador.All(char.IsAsciiDigit):
                errores.Add("El e-CF gubernamental (45) requiere el RNC del comprador.");
                break;
            case 32 when documento.Totales.MontoTotal >= MontoIdentificacionConsumo && !EsDocumento(rncComprador):
                errores.Add($"La factura de consumo desde RD${MontoIdentificacionConsumo:N2} requiere la cédula o el RNC del comprador.");
                break;
        }

        if (documento.TipoEcf == 34 && documento.Referencia is null)
            errores.Add("La nota de crédito requiere la referencia al comprobante modificado.");

        if (documento.Items.Count == 0)
            errores.Add("El e-CF debe tener al menos un ítem.");

        for (var i = 0; i < documento.Items.Count; i++)
        {
            var item = documento.Items[i];
            if (item.NumeroLinea != i + 1)
                errores.Add($"Los ítems deben numerarse de forma consecutiva desde 1 (línea {item.NumeroLinea}).");
            if (string.IsNullOrWhiteSpace(item.Nombre))
                errores.Add($"El ítem {item.NumeroLinea} no tiene nombre.");
            if (item.Cantidad <= 0)
                errores.Add($"El ítem {item.NumeroLinea} debe tener cantidad mayor que cero.");
            if (item.Monto < 0 || item.PrecioUnitario < 0 || item.Descuento < 0)
                errores.Add($"El ítem {item.NumeroLinea} tiene montos negativos.");
            if (item.IndicadorFacturacion is < 1 or > 4)
                errores.Add($"El ítem {item.NumeroLinea} tiene un indicador de facturación no válido.");
        }

        var totales = documento.Totales;
        void Cuadre(string campo, decimal esperado, decimal real)
        {
            if (Math.Abs(esperado - real) > Tolerancia)
                errores.Add($"{campo} no cuadra: {real:0.00} en totales contra {esperado:0.00} calculado.");
        }

        Cuadre("MontoGravadoI1", documento.Items.Where(i => i.IndicadorFacturacion == 1).Sum(i => i.Monto), totales.MontoGravadoI1);
        Cuadre("MontoGravadoI2", documento.Items.Where(i => i.IndicadorFacturacion == 2).Sum(i => i.Monto), totales.MontoGravadoI2);
        Cuadre("MontoGravadoI3", documento.Items.Where(i => i.IndicadorFacturacion == 3).Sum(i => i.Monto), totales.MontoGravadoI3);
        Cuadre("MontoExento", documento.Items.Where(i => i.IndicadorFacturacion == 4).Sum(i => i.Monto), totales.MontoExento);
        Cuadre("TotalITBIS1", decimal.Round(totales.MontoGravadoI1 * TotalesEcf.TasaItbis1 / 100m, 2), totales.TotalItbis1);
        Cuadre("TotalITBIS2", decimal.Round(totales.MontoGravadoI2 * TotalesEcf.TasaItbis2 / 100m, 2), totales.TotalItbis2);
        Cuadre("MontoTotal", totales.MontoGravadoTotal + totales.TotalItbis + totales.MontoExento, totales.MontoTotal);

        if (documento.FormasPago is { Count: > 0 } formas)
            Cuadre("La suma de las formas de pago", totales.MontoTotal, formas.Sum(f => f.Monto));

        if (documento.FechaHoraFirma < documento.FechaEmision.AddMinutes(-1))
            errores.Add("La fecha de firma no puede ser anterior a la fecha de emisión.");

        return errores;
    }

    /// <summary>
    /// Valida el XML contra los esquemas XSD oficiales de la DGII si hay archivos .xsd en la carpeta indicada.
    /// Sin esquemas no hay errores: la validación estructural de <see cref="Validar"/> sigue aplicando.
    /// </summary>
    public static IReadOnlyList<string> ValidarContraXsd(string xml, string? carpetaXsd)
    {
        if (string.IsNullOrWhiteSpace(carpetaXsd) || !Directory.Exists(carpetaXsd))
            return [];

        var archivos = Directory.GetFiles(carpetaXsd, "*.xsd");
        if (archivos.Length == 0)
            return [];

        var esquemas = new XmlSchemaSet { XmlResolver = null };
        foreach (var archivo in archivos)
        {
            using var lector = XmlReader.Create(archivo, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
            esquemas.Add(null, lector);
        }

        var errores = new List<string>();
        var ajustes = new XmlReaderSettings
        {
            ValidationType = ValidationType.Schema,
            Schemas = esquemas,
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
        };
        ajustes.ValidationEventHandler += (_, evento) => errores.Add($"XSD línea {evento.Exception?.LineNumber}: {evento.Message}");

        using var validador = XmlReader.Create(new StringReader(xml), ajustes);
        while (validador.Read())
        {
        }

        return errores;
    }

    private static bool EsDocumento(string? documento) =>
        documento is { Length: 9 or 11 } && documento.All(char.IsAsciiDigit);

    [GeneratedRegex(@"^E\d{12}$")]
    private static partial Regex PatronEncf();
}
