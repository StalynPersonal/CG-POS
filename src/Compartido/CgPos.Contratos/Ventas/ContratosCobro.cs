using CgPos.Dominio.Pagos;

namespace CgPos.Contratos.Ventas;

public sealed record DatosPagoVenta(
    int Numero,
    Guid FormaPagoId,
    string FormaPagoCodigo,
    string FormaPagoNombre,
    TipoFormaPago Tipo,
    string Moneda,
    decimal MontoRecibido,
    decimal? TasaCambio,
    decimal MontoAplicado,
    string? Referencia,
    string? BancoNombre,
    string? TipoTarjetaNombre,
    string? UltimosDigitos,
    bool AprobacionManual);

/// <param name="MontoRecibido">En la moneda de la forma de pago (ej. dólares).</param>
/// <param name="OperacionTerminalId">Operación aprobada por el terminal de pago (tarjeta integrada).</param>
/// <param name="AprobacionManual">Tarjeta aprobada a mano por caída de la pasarela; requiere permiso (RF-213).</param>
public sealed record SolicitudPago(
    Guid FormaPagoId,
    decimal MontoRecibido,
    string? Referencia = null,
    Guid? BancoId = null,
    Guid? TipoTarjetaId = null,
    string? UltimosDigitos = null,
    bool AprobacionManual = false,
    Guid? OperacionTerminalId = null);

/// <param name="AutorizacionId">Autorización para pagos con aprobación manual de tarjeta.</param>
public sealed record SolicitudCobro(IReadOnlyList<SolicitudPago> Pagos, Guid? AutorizacionId = null);

public sealed record DatosCobro(
    string NumeroTransaccion,
    decimal Total,
    decimal TotalCobrado,
    decimal Pagado,
    decimal Devuelta,
    decimal Redondeo,
    IReadOnlyList<DatosPagoVenta> Pagos,
    bool Impreso,
    bool GavetaAbierta);

/// <param name="Mensaje">En un cobro correcto puede traer un aviso (ej. la impresora no respondió).</param>
/// <param name="NuevaVenta">La siguiente venta en curso, lista para escanear.</param>
/// <param name="Venta">La venta cobrada (con sus pagos) o, en un rechazo, tal como quedó.</param>
public sealed record RespuestaCobro(
    CodigoResultadoVenta Resultado,
    string? Mensaje,
    DatosCobro? Cobro,
    DatosVenta? NuevaVenta,
    DatosVenta? Venta = null,
    string? PermisoRequerido = null)
{
    public bool Exitosa => Resultado == CodigoResultadoVenta.Correcto;
}

public sealed record SolicitudCobroTarjeta(decimal Monto);

public sealed record RespuestaOperacionTerminal(CodigoResultadoVenta Resultado, string? Mensaje, DatosOperacionTerminal? Operacion)
{
    public bool Exitosa => Resultado == CodigoResultadoVenta.Correcto;
}

public sealed record RespuestaImpresion(bool Correcto, string? Mensaje);

public sealed record DatosOperacionTerminal(
    Guid Id,
    bool Aprobada,
    bool SinConexion,
    decimal Monto,
    string? Aprobacion,
    string? UltimosDigitos,
    string? Marca,
    string? Mensaje);

/// <summary>Documento que la caja encola para el Central al cobrar (RF-2): la venta completa con sus pagos y su e-CF firmado.</summary>
public sealed record DocumentoVentaCobrada(
    DatosVenta Venta,
    Guid SucursalId,
    Guid CajaId,
    Guid TurnoId,
    Guid UsuarioId,
    DateTimeOffset CobradaEn,
    DocumentoElectronicoParaCentral? Ecf = null);
