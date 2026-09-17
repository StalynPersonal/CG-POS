using CgPos.Dominio.Fiscal;

namespace CgPos.Central.Aplicacion.Dgii;

/// <param name="EsResumenConsumo">El XML es el resumen de una factura de consumo (RFCE): va al servicio de consumo de la DGII.</param>
public sealed record ComprobanteParaDgii(Guid Id, string Encf, TipoComprobante TipoComprobante, string XmlFirmado, bool EsResumenConsumo = false);

public enum ResultadoRespuestaDgii
{
    /// <summary>Recibido o en validación: el resultado se consulta después con el trackId.</summary>
    EnProceso,

    Aceptado,
    AceptadoCondicional,
    Rechazado,

    /// <summary>No se obtuvo respuesta válida (comunicación, autenticación, error del servicio): se reintenta.</summary>
    Error,
}

/// <param name="TrackId">Identificador de la recepción en la DGII.</param>
/// <param name="Mensaje">Motivo del rechazo o del error, o las observaciones de una aceptación condicional.</param>
public sealed record RespuestaDgii(ResultadoRespuestaDgii Resultado, string? TrackId = null, string? Mensaje = null)
{
    public static RespuestaDgii Fallo(string mensaje) => new(ResultadoRespuestaDgii.Error, Mensaje: mensaje);
}

/// <summary>Servicios de e-CF de la DGII. Cada servicio tiene su dirección en los parámetros del Central.</summary>
public interface IClienteDgii
{
    /// <summary>
    /// Envía el XML firmado. Un e-CF queda <see cref="ResultadoRespuestaDgii.EnProceso"/> con su trackId; el resumen de una factura de consumo
    /// (RFCE) no tiene trackId: la DGII responde en el mismo envío si lo acepta o lo rechaza.
    /// </summary>
    Task<RespuestaDgii> EnviarAsync(ComprobanteParaDgii comprobante, CancellationToken cancelacion = default);

    Task<RespuestaDgii> ConsultarAsync(string trackId, CancellationToken cancelacion = default);

    /// <summary>
    /// Busca en la DGII un envío anterior del comprobante (por e-NCF, o por e-NCF y código de seguridad si es un resumen de consumo). Se usa antes
    /// de reenviar tras un fallo de comunicación: el envío pudo llegar aunque no se recibiera la respuesta.
    /// </summary>
    /// <returns>El estado encontrado, o <c>null</c> si la DGII no tiene registro del comprobante.</returns>
    Task<RespuestaDgii?> RecuperarAsync(ComprobanteParaDgii comprobante, CancellationToken cancelacion = default);

    /// <summary>Envía la anulación de rangos de e-NCF no utilizados (ANECF). El cliente la firma con el certificado del emisor.</summary>
    Task<RespuestaAnulacionDgii> AnularAsync(string xmlAnulacion, CancellationToken cancelacion = default);
}

/// <param name="Error">No hubo respuesta válida de la DGII (comunicación o error del servicio): se puede volver a intentar.</param>
/// <param name="XmlFirmado">El ANECF tal como se envió (firmado), para conservarlo.</param>
public sealed record RespuestaAnulacionDgii(bool Aceptada, bool Error, string? Mensaje, string? XmlFirmado);

/// <param name="Habilitado">Falso si el envío a la DGII no está activado en los parámetros del Central.</param>
public sealed record ResultadoCicloDgii(bool Habilitado, int Enviados, int Aceptados, int Rechazados, int Fallidos, int EnProceso)
{
    public static ResultadoCicloDgii Deshabilitado { get; } = new(false, 0, 0, 0, 0, 0);
}

/// <summary>
/// Envía a la DGII los e-CF recibidos de las cajas y consulta sus resultados (RF-222, RN-18). Los fallos de comunicación se reintentan con
/// espera creciente; cada resultado queda en el comprobante para el monitor y las cajas.
/// </summary>
public interface IDespachadorDgii
{
    Task<ResultadoCicloDgii> ProcesarAsync(CancellationToken cancelacion = default);
}
