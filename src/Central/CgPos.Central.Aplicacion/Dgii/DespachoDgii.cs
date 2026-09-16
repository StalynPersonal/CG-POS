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

/// <summary>Servicios de e-CF de la DGII: recepción del XML firmado y consulta de su resultado.</summary>
public interface IClienteDgii
{
    Task<RespuestaDgii> EnviarAsync(ComprobanteParaDgii comprobante, CancellationToken cancelacion = default);

    Task<RespuestaDgii> ConsultarAsync(string trackId, CancellationToken cancelacion = default);
}

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
