using CgPos.Dominio.Fiscal;

namespace CgPos.Contratos.Ventas;

/// <summary>e-CF de una venta cobrada, para la representación impresa (RF-221).</summary>
public sealed record DatosComprobanteElectronico(
    string Encf,
    TipoComprobante TipoComprobante,
    string CodigoSeguridad,
    DateTimeOffset FechaFirma,
    string UrlTimbre,
    EstadoDocumentoElectronico Estado,
    DateOnly? VenceSecuencia = null);

/// <summary>Lo que el Central necesita del e-CF para enviarlo a la DGII (RF-222): el XML firmado y su hash.</summary>
/// <param name="EsResumenConsumo">
/// El XML firmado es el resumen de factura de consumo (RFCE) y no el e-CF completo: la DGII lo recibe en su servicio de
/// facturas de consumo. El e-CF completo queda en la caja y es el que se le entrega al cliente.
/// </param>
public sealed record DocumentoElectronicoParaCentral(string Encf, TipoComprobante TipoComprobante, string XmlFirmado, string HashXml, DateTimeOffset FechaFirma,
    bool EsResumenConsumo = false);

public sealed record DatosSecuenciaEcf(TipoComprobante TipoComprobante, long Desde, long Hasta, long Ultimo, long Restantes, decimal PorcentajeRestante, DateOnly VenceEn,
    bool Disponible, bool EnAlerta);

/// <summary>Estado fiscal de la caja: certificado (RF-217, RF-230) y secuencias (RF-225).</summary>
/// <param name="Alertas">Mensajes para mostrar al cajero (secuencias bajas o vencidas, certificado por vencer).</param>
public sealed record DatosEstadoEcf(
    bool CertificadoConfigurado,
    bool CertificadoCargado,
    string? CertificadoSujeto,
    DateTimeOffset? CertificadoVence,
    int? DiasParaVencer,
    IReadOnlyList<DatosSecuenciaEcf> Secuencias,
    IReadOnlyList<string> Alertas,
    int RechazadosDgii = 0,
    int AceptadosDgii = 0,
    int ContingenciasPendientes = 0);

public sealed record SolicitudCargarCertificado(string Pin);

public sealed record RespuestaCertificado(bool Correcto, string? Mensaje, DatosEstadoEcf? Estado);

public sealed record DatosDocumentoElectronico(
    int Id,
    int VentaId,
    string Encf,
    TipoComprobante TipoComprobante,
    decimal MontoTotal,
    DateTimeOffset FechaFirma,
    EstadoDocumentoElectronico Estado,
    DateTimeOffset EstadoActualizadoEn,
    string? MensajeEstado,
    string RutaXml);
