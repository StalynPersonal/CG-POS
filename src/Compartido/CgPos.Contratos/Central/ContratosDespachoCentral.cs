using CgPos.Contratos.Sincronizacion;
using CgPos.Contratos.Ventas;
using CgPos.Dominio.Entregas;

namespace CgPos.Contratos.Central;

/// <summary>Pendiente de entrega o envío visto desde el Central, con la sucursal y la caja que lo emitieron.</summary>
/// <param name="Atrasado">Pasó la fecha comprometida y todavía no se entregó.</param>
public sealed record DatosPendienteCentralResumen(
    int Id,
    /// <summary>Número con el que la caja lo creó al cobrar.</summary>
    string Numero,
    /// <summary>Número que le puso el Central al recibirlo; nulo si faltaba su secuencia.</summary>
    string? NumeroCentral,
    string VentaNumero,
    string SucursalCodigo,
    string CajaCodigo,
    MetodoEntrega Metodo,
    EstadoPendiente Estado,
    string? Destino,
    string? ClienteNombre,
    string? ClienteDocumento,
    string? Telefono,
    DateOnly? FechaComprometida,
    bool Atrasado,
    decimal Unidades,
    decimal UnidadesEntregadas,
    DateTimeOffset CreadoEn,
    DateTimeOffset ActualizadoEn);

public sealed record PaginaPendientesCentral(IReadOnlyList<DatosPendienteCentralResumen> Elementos, int Total);

/// <summary>Cuántos pendientes hay abiertos, atrasados y por método, para el tablero del Central.</summary>
public sealed record ResumenDespachoCentral(int Abiertos, int Atrasados, int Retiros, int Envios, int EntregadosHoy);

/// <param name="Pendiente">Documento completo tal como lo informó la caja, con líneas y entregas.</param>
public sealed record DetallePendienteCentral(DatosPendienteCentralResumen Resumen, DocumentoPendienteEntrega Pendiente);
