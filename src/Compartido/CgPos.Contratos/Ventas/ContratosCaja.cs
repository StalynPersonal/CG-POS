using CgPos.Contratos.Catalogo;
using CgPos.Dominio.Pagos;
using CgPos.Dominio.Turnos;

namespace CgPos.Contratos.Ventas;

public sealed record DatosMovimientoCaja(
    Guid Id,
    TipoMovimientoCaja Tipo,
    int Numero,
    decimal Monto,
    string Moneda,
    string? Motivo,
    string UsuarioNombre,
    string? UsuarioAnteriorNombre,
    string? AutorizadoPorNombre,
    DateTimeOffset Fecha);

/// <summary>Forma de pago en el resumen del turno. En cierre ciego lo esperado solo lo ve quien tiene permiso de pre-cierre (RF-9).</summary>
public sealed record DatosFormaPagoTurno(Guid FormaPagoId, string Codigo, string Nombre, TipoFormaPago Tipo, string Moneda, int Orden, decimal? Esperado, int? Transacciones)
{
    public bool EsEfectivo => ReglasCuadre.EsEfectivo(Tipo);
}

/// <summary>Estado del turno para retiros, pre-cierre y cierre.</summary>
/// <param name="MuestraEsperado">Falso en cierre ciego para quien no tiene permiso de pre-cierre: lo esperado y las ventas llegan nulos.</param>
/// <param name="Bloqueos">Lo que impide cerrar el turno (RF-265, RN-22); vacío si se puede cerrar.</param>
public sealed record DatosResumenTurno(
    DatosTurno Turno,
    bool CierreCiego,
    bool MuestraEsperado,
    bool FondoEnCuadre,
    int? CantidadVentas,
    decimal? TotalVentas,
    decimal TotalRetiros,
    IReadOnlyList<DatosFormaPagoTurno> FormasPago,
    IReadOnlyList<DatosDenominacion> Denominaciones,
    IReadOnlyList<DatosMovimientoCaja> Movimientos,
    IReadOnlyList<string> Bloqueos);

public sealed record SolicitudRetiroEfectivo(decimal Monto, string? Motivo, Guid? AutorizacionId);

public sealed record SolicitudDeclaracionFormaPago(Guid FormaPagoId, decimal Monto);

public sealed record SolicitudConteoDenominacion(Guid DenominacionId, int Cantidad);

/// <param name="Conteo">Efectivo por denominaciones; si viene, es lo declarado de la forma de efectivo de esa moneda.</param>
public sealed record SolicitudCierreTurno(
    IReadOnlyList<SolicitudDeclaracionFormaPago>? Declaraciones,
    IReadOnlyList<SolicitudConteoDenominacion>? Conteo,
    Guid? AutorizacionId);

public sealed record SolicitudReabrirCierre(string? Motivo, Guid? AutorizacionId);

public sealed record DatosCierreFormaPago(
    Guid FormaPagoId,
    string Codigo,
    string Nombre,
    TipoFormaPago Tipo,
    string Moneda,
    int Transacciones,
    decimal Esperado,
    decimal Declarado,
    decimal Diferencia);

public sealed record DatosCierreDenominacion(string Moneda, decimal Valor, TipoDenominacion Tipo, int Cantidad, decimal Importe);

/// <summary>Cierre de turno completo: se imprime, se reimprime igual (RF-291) y viaja al Central (RF-267).</summary>
public sealed record DatosCierre(
    Guid Id,
    Guid TurnoId,
    long TurnoNumero,
    int Numero,
    Guid CajaId,
    Guid SucursalId,
    DateOnly FechaOperacion,
    bool Ciego,
    decimal FondoInicial,
    bool FondoEnCuadre,
    int CantidadVentas,
    decimal TotalVentas,
    decimal TotalRetiros,
    decimal TotalEsperado,
    decimal TotalDeclarado,
    decimal Diferencia,
    string UsuarioNombre,
    DateTimeOffset AbiertoEn,
    DateTimeOffset CerradoEn,
    EstadoCierre Estado,
    string? ReabiertoPorNombre,
    DateTimeOffset? ReabiertoEn,
    string? MotivoReapertura,
    IReadOnlyList<DatosCierreFormaPago> FormasPago,
    IReadOnlyList<DatosCierreDenominacion> Denominaciones,
    IReadOnlyList<DatosMovimientoCaja> Movimientos);

/// <summary>Mensaje para el Central de un retiro o un relevo.</summary>
public sealed record DocumentoMovimientoCaja(DatosMovimientoCaja Movimiento, Guid TurnoId, long TurnoNumero, Guid CajaId, Guid SucursalId);

public sealed record DocumentoReaperturaCierre(Guid CierreId, Guid TurnoId, long TurnoNumero, Guid CajaId, Guid SucursalId, string ReabiertoPorNombre,
    string Motivo, DateTimeOffset ReabiertoEn);

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
    NoSePuedeReabrir,
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
