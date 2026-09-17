using CgPos.Dominio.Devoluciones;

namespace CgPos.Contratos.Central;

/// <param name="Reservado">Saldo retenido por reservas vigentes de otras cajas.</param>
/// <param name="Disponible">Lo que se puede consumir ahora: saldo menos reservas vigentes.</param>
public sealed record DatosNotaCreditoCentral(
    int Id,
    string Numero,
    string? Encf,
    int SucursalId,
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
    DateTimeOffset EmitidaEn);

public sealed record PaginaNotasCreditoCentral(IReadOnlyList<DatosNotaCreditoCentral> Elementos, int Total);

/// <summary>Nota de crédito del Central tal como la consulta una caja: identificada por su número, sin Id del Central.</summary>
/// <param name="Disponible">Lo que se puede consumir ahora: saldo menos reservas vigentes.</param>
public sealed record DatosNotaCreditoParaCaja(
    string Numero,
    string? Encf,
    string SucursalCodigo,
    string CajaCodigo,
    string ClienteDocumento,
    string ClienteNombre,
    string Moneda,
    decimal Total,
    decimal Disponible,
    DateOnly VenceEn,
    EstadoNotaCreditoCentral Estado);

/// <summary>Reserva de saldo para la factura <paramref name="VentaNumero"/>; la caja la libera con el mismo número si no cobra.</summary>
public sealed record SolicitudReservaNotaCredito(string VentaNumero, decimal Monto);

public sealed record RespuestaReservaNotaCredito(
    bool Exitosa,
    string? Mensaje,
    decimal Monto = 0m,
    DateTimeOffset? VenceEn = null,
    DatosNotaCreditoParaCaja? NotaCredito = null);

/// <param name="Tipo">Consumo, reserva o prórroga.</param>
public sealed record DatosMovimientoNotaCredito(DateTimeOffset Fecha, string Tipo, string CajaCodigo, decimal Monto, string? Detalle);

