using CgPos.Contratos.Catalogo;
using CgPos.Dominio.Pagos;
using CgPos.Dominio.Turnos;

namespace CgPos.Contratos.Ventas;

public sealed record DatosMovimientoCaja(
    int Id,
    TipoMovimientoCaja Tipo,
    int Numero,
    decimal Monto,
    string? Moneda,
    string? Motivo,
    string UsuarioNombre,
    string? UsuarioAnteriorNombre,
    string? AutorizadoPorNombre,
    DateTimeOffset Fecha);

/// <summary>Forma de pago en el resumen del turno. En cierre ciego lo esperado solo lo ve quien tiene permiso de pre-cierre (RF-9).</summary>
public sealed record DatosFormaPagoTurno(int FormaPagoId, string Codigo, string Nombre, TipoFormaPago Tipo, string Moneda, int Orden, decimal? Esperado, int? Transacciones)
{
    public bool EsEfectivo => ReglasCuadre.EsEfectivo(Tipo);
}

/// <summary>Estado del turno para retiros, pre-cierre y cierre.</summary>
/// <param name="MuestraEsperado">Falso en cierre ciego para quien no tiene permiso de pre-cierre: lo esperado y las ventas llegan nulos.</param>
/// <param name="Bloqueos">Lo que impide cerrar el turno (RF-265, RN-22); vacío si se puede cerrar.</param>
public sealed record DatosResumenTurno(
    DatosTurno Turno,
    bool MuestraEsperado,
    bool FondoEnCuadre,
    int? CantidadVentas,
    decimal? TotalVentas,
    decimal TotalRetiros,
    IReadOnlyList<DatosFormaPagoTurno> FormasPago,
    IReadOnlyList<DatosDenominacion> Denominaciones,
    IReadOnlyList<DatosMovimientoCaja> Movimientos,
    IReadOnlyList<string> Bloqueos,
    string MonedaLocal);

/// <summary>
/// Cuadre de las tarjetas del turno contra el lote del terminal (RF-215): lo que la caja registró como aprobado frente a lo que
/// el terminal contabilizó al cerrar el lote.
/// </summary>
/// <param name="SoloEnCaja">Autorizaciones que la caja tiene y el terminal no reportó.</param>
/// <param name="SoloEnTerminal">Autorizaciones del lote que la caja no tiene.</param>
public sealed record DatosConciliacionTarjetas(
    bool LoteCerrado,
    string? NumeroLote,
    int TransaccionesCaja,
    decimal MontoCaja,
    int TransaccionesTerminal,
    decimal MontoTerminal,
    decimal Diferencia,
    IReadOnlyList<string> SoloEnCaja,
    IReadOnlyList<string> SoloEnTerminal,
    bool DetalleDelTerminal,
    string? Mensaje)
{
    /// <summary>El terminal detalló el lote y cuadra con la caja.</summary>
    public bool Cuadra => LoteCerrado && DetalleDelTerminal && Diferencia == 0m && SoloEnCaja.Count == 0 && SoloEnTerminal.Count == 0;
}

public sealed record SolicitudRetiroEfectivo(decimal Monto, string? Motivo, Guid? AutorizacionId);

public sealed record SolicitudDeclaracionFormaPago(int FormaPagoId, decimal Monto);

public sealed record SolicitudConteoDenominacion(int DenominacionId, int Cantidad);

/// <summary>
/// Cerrar el turno: la cajera no declara nada, solo pide la autorización del supervisor. El dinero se cuenta después, en el
/// módulo de cuadre del Central.
/// </summary>
public sealed record SolicitudCierreTurno(Guid? AutorizacionId);


public sealed record DatosCierreFormaPago(
    int FormaPagoId,
    string Codigo,
    string Nombre,
    TipoFormaPago Tipo,
    string Moneda,
    int Transacciones,
    decimal Esperado);

/// <summary>Cierre de turno completo: se imprime, se reimprime igual (RF-291) y viaja al Central (RF-267).</summary>
public sealed record DatosCierre(
    int Id,
    int TurnoId,
    long TurnoNumero,
    int Numero,
    int CajaId,
    int SucursalId,
    DateOnly FechaOperacion,
    decimal FondoInicial,
    bool FondoEnCuadre,
    string Moneda,
    int CantidadVentas,
    decimal TotalVentas,
    decimal TotalRetiros,
    decimal TotalEsperado,
    string UsuarioNombre,
    DateTimeOffset AbiertoEn,
    DateTimeOffset CerradoEn,
    IReadOnlyList<DatosCierreFormaPago> FormasPago,
    IReadOnlyList<DatosMovimientoCaja> Movimientos,

    /// <summary>El cierre del lote del terminal de tarjetas, si el cajero alcanzó a cerrarlo antes del turno.</summary>
    DatosLoteTarjetas? Lote);

/// <summary>
/// El lote del terminal cuadrado contra lo aprobado en la caja (RF-215). Cuando el terminal no detalla su lote solo queda
/// lo de la caja, para compararlo a mano contra el comprobante que imprimió.
/// </summary>
/// <param name="SoloEnCaja">Aprobaciones que tiene la caja y el lote no reporta.</param>
/// <param name="SoloEnTerminal">Aprobaciones del lote que la caja no tiene.</param>
public sealed record DatosLoteTarjetas(
    string? NumeroLote,
    int TransaccionesCaja,
    decimal MontoCaja,
    int TransaccionesTerminal,
    decimal MontoTerminal,
    decimal Diferencia,
    bool DetalleDelTerminal,
    string UsuarioNombre,
    DateTimeOffset CerradoEn,
    IReadOnlyList<string> SoloEnCaja,
    IReadOnlyList<string> SoloEnTerminal);


/// <param name="Programado">Tiempo previsto (almuerzo, receso): en el reporte no cuenta como una parada imprevista.</param>
/// <param name="ExigeNota">Pide escribir en qué consistió, para el motivo «Otro».</param>
public sealed record DatosMotivoSuspension(int Codigo, string Nombre, bool Programado, bool ExigeNota);

/// <summary>La caja está parada: desde cuándo y por qué. Con esto la pantalla bloqueada muestra el cronómetro.</summary>
public sealed record DatosSuspension(int Id, int? MotivoCodigo, string MotivoNombre, string? Nota, string UsuarioNombre, DateTimeOffset SuspendidaEn);

public enum CodigoResultadoCaja
{
    Correcto,
    TurnoNoAbierto,
    TurnoDeOtroUsuario,
    TurnoDelMismoUsuario,
    SinPermiso,
    RequiereAutorizacion,
    AutorizacionInvalida,
    MontoInvalido,
    EfectivoInsuficiente,
    MotivoRequerido,
    CierreBloqueado,
    DeclaracionInvalida,
    CierreNoEncontrado,
}

public sealed record RespuestaCaja(
    CodigoResultadoCaja Resultado,
    string? Mensaje,
    string? PermisoRequerido = null,
    DatosResumenTurno? Resumen = null,
    DatosMovimientoCaja? Movimiento = null,
    DatosCierre? Cierre = null,
    DatosTurno? Turno = null,
    IReadOnlyList<string>? Bloqueos = null)
{
    public bool Exitosa => Resultado == CodigoResultadoCaja.Correcto;
}
