using CgPos.Dominio.Pagos;

namespace CgPos.Contratos.Ventas;

public sealed record DatosPagoVenta(
    int Numero,
    int FormaPagoId,
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
    int FormaPagoId,
    decimal MontoRecibido,
    string? Referencia = null,
    int? BancoId = null,
    int? TipoTarjetaId = null,
    string? UltimosDigitos = null,
    bool AprobacionManual = false,
    int? OperacionTerminalId = null);

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
    bool GavetaAbierta,
    IReadOnlyList<string>? PendientesEntrega = null);

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

/// <param name="PagaSaldo">La tarjeta cubre todo lo que falta: si el terminal lee la tarjeta y hay descuento del banco, se cobra menos.</param>
public sealed record SolicitudCobroTarjeta(decimal Monto, bool PagaSaldo = false);

/// <param name="Venta">La venta actualizada cuando el descuento del banco por la tarjeta cambió el total (RF-98).</param>
public sealed record RespuestaOperacionTerminal(CodigoResultadoVenta Resultado, string? Mensaje, DatosOperacionTerminal? Operacion, DatosVenta? Venta = null)
{
    public bool Exitosa => Resultado == CodigoResultadoVenta.Correcto;
}

public sealed record RespuestaImpresion(bool Correcto, string? Mensaje);

public sealed record DatosOperacionTerminal(
    int Id,
    bool Aprobada,
    bool SinConexion,
    decimal Monto,
    string? Aprobacion,
    string? UltimosDigitos,
    string? Marca,
    string? Mensaje);
