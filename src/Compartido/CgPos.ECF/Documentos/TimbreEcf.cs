using System.Globalization;

namespace CgPos.ECF.Documentos;

/// <summary>
/// URL de consulta del timbre que va en el código QR de la representación impresa (RF-221). La factura de consumo menor del monto
/// de identificación usa la consulta simplificada; los demás tipos, la completa. Pendiente confirmar contra la documentación DGII.
/// </summary>
public static class TimbreEcf
{
    public static string UrlBase(AmbienteEcf ambiente) => ambiente switch
    {
        AmbienteEcf.Pruebas => "https://ecf.dgii.gov.do/testecf",
        AmbienteEcf.Certificacion => "https://ecf.dgii.gov.do/certecf",
        _ => "https://ecf.dgii.gov.do/ecf",
    };

    /// <param name="montoIdentificacionConsumo">Total desde el cual la factura de consumo identifica al comprador (parámetro del negocio).</param>
    public static string Url(AmbienteEcf ambiente, DocumentoEcf documento, string codigoSeguridad, decimal montoIdentificacionConsumo)
    {
        ArgumentNullException.ThrowIfNull(documento);
        ArgumentException.ThrowIfNullOrWhiteSpace(codigoSeguridad);

        var baseUrl = UrlBase(ambiente);
        var monto = documento.Totales.MontoTotal.ToString("0.00", CultureInfo.InvariantCulture);
        var codigo = Uri.EscapeDataString(codigoSeguridad);

        if (documento.TipoEcf == 32 && documento.Totales.MontoTotal < montoIdentificacionConsumo)
            return $"{baseUrl}/ConsultaTimbreFC?RncEmisor={documento.Emisor.Rnc}&ENCF={documento.Encf}&MontoTotal={monto}&CodigoSeguridad={codigo}";

        return $"{baseUrl}/ConsultaTimbre?RncEmisor={documento.Emisor.Rnc}"
               + $"&RncComprador={Uri.EscapeDataString(documento.Comprador?.Rnc ?? string.Empty)}"
               + $"&ENCF={documento.Encf}"
               + $"&FechaEmision={GeneradorXmlEcf.Fecha(documento.FechaEmision)}"
               + $"&MontoTotal={monto}"
               + $"&FechaFirma={Uri.EscapeDataString(GeneradorXmlEcf.FechaHora(documento.FechaHoraFirma))}"
               + $"&CodigoSeguridad={codigo}";
    }
}
