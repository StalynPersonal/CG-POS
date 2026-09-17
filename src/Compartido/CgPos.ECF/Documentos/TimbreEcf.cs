using System.Globalization;

namespace CgPos.ECF.Documentos;

/// <summary>
/// URL de consulta del timbre que va en el código QR de la representación impresa (RF-221). La factura de consumo menor del monto
/// de identificación usa la consulta simplificada (servicio de facturas de consumo, en su propio host); los demás tipos, la completa.
/// Las direcciones son parámetros: definen el ambiente de la DGII (pruebas, certificación o producción) y no hay valores fijos.
/// </summary>
public static class TimbreEcf
{
    /// <param name="urlConsultaTimbre">Consulta completa del timbre (parámetro de la caja, según el ambiente de la DGII).</param>
    /// <param name="urlConsultaTimbreConsumo">Consulta simplificada de las facturas de consumo (servicio de facturas de consumo de la DGII).</param>
    /// <param name="montoIdentificacionConsumo">Total desde el cual la factura de consumo identifica al comprador (parámetro del negocio).</param>
    public static string Url(string urlConsultaTimbre, string urlConsultaTimbreConsumo, DocumentoEcf documento, string codigoSeguridad,
        decimal montoIdentificacionConsumo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(urlConsultaTimbre);
        ArgumentException.ThrowIfNullOrWhiteSpace(urlConsultaTimbreConsumo);
        ArgumentNullException.ThrowIfNull(documento);
        ArgumentException.ThrowIfNullOrWhiteSpace(codigoSeguridad);

        var monto = documento.Totales.MontoTotal.ToString("0.00", CultureInfo.InvariantCulture);
        var codigo = Uri.EscapeDataString(codigoSeguridad);

        if (documento.TipoEcf == 32 && documento.Totales.MontoTotal < montoIdentificacionConsumo)
            return $"{urlConsultaTimbreConsumo.TrimEnd('?', '/')}?RncEmisor={documento.Emisor.Rnc}&ENCF={documento.Encf}&MontoTotal={monto}&CodigoSeguridad={codigo}";

        return $"{urlConsultaTimbre.TrimEnd('?', '/')}?RncEmisor={documento.Emisor.Rnc}"
               + $"&RncComprador={Uri.EscapeDataString(documento.Comprador?.Rnc ?? string.Empty)}"
               + $"&ENCF={documento.Encf}"
               + $"&FechaEmision={GeneradorXmlEcf.Fecha(documento.FechaEmision)}"
               + $"&MontoTotal={monto}"
               + $"&FechaFirma={Uri.EscapeDataString(GeneradorXmlEcf.FechaHora(documento.FechaHoraFirma))}"
               + $"&CodigoSeguridad={codigo}";
    }
}
