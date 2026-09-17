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

    /// <summary>
    /// Tolerancia por redondeo entre la suma de líneas y los totales: el ITBIS se calcula por línea y en ventas largas su suma
    /// difiere unos centavos del total por tasa; también absorbe el redondeo del efectivo en las formas de pago.
    /// </summary>
    public const decimal Tolerancia = 1.00m;

    /// <summary>Líneas máximas de un e-CF según el formato de la DGII.</summary>
    public const int MaximoItems = 1000;

    public const int LargoMaximoRazonSocial = 150;
    public const int LargoMaximoNombreItem = 80;

    /// <summary>Largos que admite el NCF modificado de una nota de crédito o débito: e-NCF (13), NCF nuevo (11) o NCF antiguo (19).</summary>
    private static readonly int[] LargosNcfModificado = [11, 13, 19];

    /// <param name="montoIdentificacionConsumo">Total desde el cual la factura de consumo identifica al comprador (parámetro del negocio).</param>
    public static IReadOnlyList<string> Validar(DocumentoEcf documento, decimal montoIdentificacionConsumo)
    {
        ArgumentNullException.ThrowIfNull(documento);
        var errores = new List<string>();

        if (!TiposValidos.Contains(documento.TipoEcf))
            errores.Add($"Tipo de e-CF no válido: {documento.TipoEcf}.");
        if (documento.TipoIngresos is < 1 or > 6)
            errores.Add($"Tipo de ingresos no válido: {documento.TipoIngresos}.");
        if (documento.TipoPago is < 1 or > 3)
            errores.Add($"Tipo de pago no válido: {documento.TipoPago}.");
        if (!PatronEncf().IsMatch(documento.Encf))
            errores.Add($"El eNCF '{documento.Encf}' no tiene el formato E + tipo + 10 dígitos.");
        else if (documento.Encf.Substring(1, 2) != documento.TipoEcf.ToString("00"))
            errores.Add($"El eNCF '{documento.Encf}' no corresponde al tipo {documento.TipoEcf}.");

        if (!EsDocumento(documento.Emisor.Rnc))
            errores.Add("El RNC del emisor debe tener 9 u 11 dígitos.");
        if (string.IsNullOrWhiteSpace(documento.Emisor.RazonSocial))
            errores.Add("Falta la razón social del emisor.");
        else if (documento.Emisor.RazonSocial.Length > LargoMaximoRazonSocial)
            errores.Add($"La razón social del emisor excede {LargoMaximoRazonSocial} caracteres.");
        if (documento.Comprador?.RazonSocial is { Length: > LargoMaximoRazonSocial })
            errores.Add($"La razón social del comprador excede {LargoMaximoRazonSocial} caracteres.");
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
            case 32 when documento.Totales.MontoTotal >= montoIdentificacionConsumo && !EsDocumento(rncComprador):
                errores.Add($"La factura de consumo desde {montoIdentificacionConsumo:N2} requiere la cédula o el RNC del comprador.");
                break;
        }

        if (documento.TipoEcf == 34 && documento.Referencia is null)
            errores.Add("La nota de crédito requiere la referencia al comprobante modificado.");

        // Comprobante modificado (RF-227): la DGII acepta el e-NCF, el NCF nuevo o el antiguo, y su fecha no puede ser futura.
        if (documento.Referencia is { } referencia)
        {
            var modificado = (referencia.NcfModificado ?? string.Empty).Trim();
            if (!LargosNcfModificado.Contains(modificado.Length))
                errores.Add($"El comprobante modificado '{modificado}' debe tener 11, 13 o 19 caracteres.");
            if (referencia.FechaNcfModificado > DateOnly.FromDateTime(documento.FechaEmision.LocalDateTime))
                errores.Add("La fecha del comprobante modificado no puede ser posterior a la emisión.");
            if (referencia.CodigoModificacion is < 1 or > 5)
                errores.Add($"El código de modificación {referencia.CodigoModificacion} no es válido (1 a 5).");
        }

        if (documento.Items.Count == 0)
            errores.Add("El e-CF debe tener al menos un ítem.");
        else if (documento.Items.Count > MaximoItems)
            errores.Add($"El e-CF no puede tener más de {MaximoItems} líneas ({documento.Items.Count}).");

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
            if (item.Nombre is { Length: > LargoMaximoNombreItem })
                errores.Add($"El nombre del ítem {item.NumeroLinea} excede {LargoMaximoNombreItem} caracteres.");
            if (item.IndicadorBienServicio is < 1 or > 2)
                errores.Add($"El ítem {item.NumeroLinea} debe indicar si es bien (1) o servicio (2).");
            if (item.Descuento > item.PrecioUnitario * item.Cantidad + Tolerancia)
                errores.Add($"El descuento del ítem {item.NumeroLinea} es mayor que su importe.");
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
        void CuadreItbis(int indicador, decimal gravado, decimal? tasa, decimal itbis)
        {
            if (gravado <= 0)
                return;
            if (tasa is not { } porcentaje)
                errores.Add($"Falta la tasa de ITBIS de las líneas con indicador {indicador}.");
            else
                Cuadre($"TotalITBIS{indicador}", decimal.Round(gravado * porcentaje / 100m, 2), itbis);
        }

        CuadreItbis(1, totales.MontoGravadoI1, totales.TasaItbis1, totales.TotalItbis1);
        CuadreItbis(2, totales.MontoGravadoI2, totales.TasaItbis2, totales.TotalItbis2);
        CuadreItbis(3, totales.MontoGravadoI3, totales.TasaItbis3, totales.TotalItbis3);
        Cuadre("MontoTotal", totales.MontoGravadoTotal + totales.TotalItbis + totales.MontoExento, totales.MontoTotal);

        // Con retención (régimen especial, Ley 32-23) el cliente paga el ValorPagar, no el total de la factura.
        if (documento.FormasPago is { Count: > 0 } formas)
            Cuadre("La suma de las formas de pago", totales.ValorPagar ?? totales.MontoTotal, formas.Sum(f => f.Monto));

        if (documento.FechaHoraFirma < documento.FechaEmision.AddMinutes(-1))
            errores.Add("La fecha de firma no puede ser anterior a la fecha de emisión.");

        // La DGII rechaza un comprobante emitido "en el futuro" o con la secuencia ya vencida.
        if (documento.FechaEmision > documento.FechaHoraFirma.AddMinutes(1))
            errores.Add("La fecha de emisión no puede ser posterior a la fecha de firma.");
        if (documento.FechaVencimientoSecuencia is { } vence && DateOnly.FromDateTime(documento.FechaEmision.LocalDateTime) > vence)
            errores.Add($"La secuencia de e-CF venció el {vence:dd/MM/yyyy}.");

        if (totales.MontoTotal <= 0)
            errores.Add("El monto total del e-CF debe ser mayor que cero.");
        if (totales.TotalItbis < 0)
            errores.Add("El ITBIS total del e-CF no puede ser negativo.");

        return errores;
    }

    /// <summary>
    /// Valida el XML contra el esquema oficial de la DGII del tipo indicado, si está en la carpeta configurada. Cada tipo tiene su
    /// XSD (ej. "e-CF 32 v.1.0.xsd") y todos definen el mismo elemento raíz, así que se carga solo el del comprobante que se emite.
    /// Sin esquema no hay errores: la validación estructural de <see cref="Validar"/> sigue aplicando.
    /// </summary>
    /// <param name="tipoEcf">Tipo del comprobante (31, 32, 34…); nulo si se elige el esquema por nombre.</param>
    /// <param name="nombreEsquema">Esquema que no es un e-CF: "RFCE" para el resumen de consumo, "ACECF" para el acuse…</param>
    public static IReadOnlyList<string> ValidarContraXsd(string xml, string? carpetaXsd, int? tipoEcf = null, string? nombreEsquema = null)
    {
        if (string.IsNullOrWhiteSpace(carpetaXsd) || !Directory.Exists(carpetaXsd))
            return [];

        var archivos = Directory.GetFiles(carpetaXsd, "*.xsd");
        if (nombreEsquema is { Length: > 0 } nombre)
        {
            archivos = archivos.Where(a => Path.GetFileName(a).StartsWith(nombre, StringComparison.OrdinalIgnoreCase)).ToArray();
        }
        else if (tipoEcf is { } tipo)
        {
            // "e-CF 32 v.1.0.xsd" o cualquier nombre que lleve el tipo; los acuses y la semilla no aplican a un e-CF.
            archivos = archivos.Where(a => Path.GetFileNameWithoutExtension(a).Contains($"{tipo}", StringComparison.Ordinal)
                && Path.GetFileName(a).StartsWith("e-CF", StringComparison.OrdinalIgnoreCase)).ToArray();
        }

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
