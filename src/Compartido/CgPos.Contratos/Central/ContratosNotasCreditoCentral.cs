using CgPos.Dominio.Devoluciones;

namespace CgPos.Contratos.Central;

/// <param name="Reservado">Saldo retenido por reservas vigentes de otras cajas.</param>
/// <param name="Disponible">Lo que se puede consumir ahora: saldo menos reservas vigentes.</param>
public sealed record DatosNotaCreditoCentral(
    Guid Id,
    string Numero,
    string? Encf,
    Guid SucursalId,
    string SucursalCodigo,
    string CajaCodigo,
    string ClienteDocumento,
    string ClienteNombre,
    string Moneda,
    decimal Total,
    decimal Consumido,
    decimal Reservado,
    decimal Disponible,
    DateOnly VenceEn,
    EstadoNotaCreditoCentral Estado,
    bool Sobregirada,
    DateTimeOffset EmitidaEn,
    DateTimeOffset? ProrrogadaEn,
    string? ProrrogadaPor,
    string? MotivoProrroga);

public sealed record PaginaNotasCreditoCentral(IReadOnlyList<DatosNotaCreditoCentral> Elementos, int Total);

public sealed record SolicitudReservaNotaCredito(decimal Monto);

/// <param name="ReservaId">Identificador con el que la caja libera la reserva si no cobra.</param>
public sealed record RespuestaReservaNotaCredito(
    bool Exitosa,
    string? Mensaje,
    Guid? ReservaId = null,
    decimal Monto = 0m,
    DateTimeOffset? VenceEn = null,
    DatosNotaCreditoCentral? NotaCredito = null);

/// <param name="Tipo">Consumo, reserva o prórroga.</param>
public sealed record DatosMovimientoNotaCredito(DateTimeOffset Fecha, string Tipo, string CajaCodigo, decimal Monto, string? Detalle);

public sealed record SolicitudProrrogaNotaCredito(DateOnly VenceEn, string Motivo);
