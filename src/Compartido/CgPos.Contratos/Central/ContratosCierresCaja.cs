using CgPos.Dominio.Pagos;
using CgPos.Dominio.Turnos;

namespace CgPos.Contratos.Central;

/// <summary>
/// Cierre de caja como lo ve el Central: lo que informó la terminal y las correcciones que se le hayan hecho aquí. La caja
/// no puede reabrir un cierre, así que un cuadre mal hecho se arregla con un ajuste sobre la forma de pago que quedó mal.
/// </summary>
/// <param name="EnCierreSucursal">Su día ya se consolidó en el cierre de la sucursal: entonces no se puede corregir.</param>
public sealed record DatosCierreCaja(
    int Id,
    string SucursalCodigo,
    string CajaCodigo,
    long TurnoNumero,
    int Numero,
    DateOnly FechaOperacion,
    string UsuarioNombre,

    /// <summary>La caja cerró el turno pero el supervisor todavía no ha contado el dinero.</summary>
    bool PendienteDeCuadre,
    string? CuadradoPor,
    DateTimeOffset? CuadradoEn,
    int CantidadVentas,
    decimal TotalVentas,
    decimal TotalEsperado,
    decimal TotalDeclarado,
    decimal Diferencia,
    DateTimeOffset CerradoEn,
    bool EnCierreSucursal,
    IReadOnlyList<DatosFormaPagoCierreCaja> FormasPago,
    IReadOnlyList<DatosAjusteCierre> Ajustes,

    /// <summary>El lote de tarjetas del turno; vacío si el cajero no lo cerró en la caja.</summary>
    DatosLoteCierreCaja? Lote = null);

/// <summary>
/// Conciliación del lote de tarjetas del turno (RF-215): lo aprobado en la caja contra lo que reportó el terminal, con las
/// aprobaciones que aparecen de un solo lado.
/// </summary>
public sealed record DatosLoteCierreCaja(
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

/// <param name="Declarados">Lo contado en cada forma de pago del cierre.</param>
/// <param name="Conteo">Las denominaciones del efectivo; su suma tiene que cuadrar con lo declarado en efectivo.</param>
public sealed record SolicitudCuadreCierre(
    IReadOnlyList<SolicitudDeclaracionCuadre> Declarados,
    IReadOnlyList<SolicitudConteoCuadre> Conteo);

public sealed record SolicitudDeclaracionCuadre(int FormaPagoId, decimal Monto);

public sealed record SolicitudConteoCuadre(int DenominacionId, int Cantidad);

public sealed record DatosFormaPagoCierreCaja(int Id, TipoFormaPago Tipo, string Nombre, string Moneda, int Transacciones, decimal Esperado, decimal Declarado,
    decimal Diferencia);

/// <param name="DeclaradoAnterior">Lo que la caja había declarado antes de esta corrección; el original nunca se pierde.</param>
public sealed record DatosAjusteCierre(
    string FormaPagoNombre,
    string Moneda,
    decimal DeclaradoAnterior,
    decimal DeclaradoNuevo,
    string Motivo,
    string AjustadoPorNombre,
    DateTimeOffset AjustadoEn);

/// <param name="Declarado">Lo que de verdad había en esa forma de pago.</param>
/// <param name="Motivo">Obligatorio: queda en la auditoría y en el historial del cierre.</param>
public sealed record SolicitudAjusteCierre(int FormaPagoId, decimal Declarado, string Motivo);

// ---------- Módulo de cuadre ----------

public sealed record SolicitudIngresoCuadre(string? Usuario, string? Clave);

/// <param name="Sucursales">Las sucursales de las cajas que tiene asignadas: lo único que puede ver y cuadrar.</param>
public sealed record DatosSesionCuadre(
    int UsuarioId,
    string Codigo,
    string Nombre,
    string RolNombre,
    IReadOnlyList<string> Permisos,
    IReadOnlyList<DatosSucursalCuadre> Sucursales);

public sealed record DatosSucursalCuadre(int Id, string Codigo, string Nombre);

public sealed record RespuestaSesionCuadre(
    bool Exitosa,
    string? Mensaje = null,
    string? TokenAcceso = null,
    DateTimeOffset? ExpiraEn = null,
    DatosSesionCuadre? Sesion = null);


// ---------- Reportes del módulo de cuadre ----------

/// <summary>
/// El día de una sucursal visto de una sola vez: cuánto se esperaba y cuánto se declaró en cada forma de pago, y cuántas
/// cajas faltan por cuadrar. Es lo que el gerente mira antes de mandar el depósito.
/// </summary>
/// <param name="CajasPendientes">Cierres del día que todavía nadie ha cuadrado: mientras haya, el resumen está incompleto.</param>
public sealed record DatosResumenCuadre(
    DateOnly Dia,
    int Cierres,
    int CajasPendientes,
    decimal TotalVentas,
    decimal TotalEsperado,
    decimal TotalDeclarado,
    decimal Diferencia,
    IReadOnlyList<DatosResumenFormaPagoCuadre> FormasPago);

public sealed record DatosResumenFormaPagoCuadre(
    string Nombre,
    TipoFormaPago Tipo,
    string Moneda,
    int Transacciones,
    decimal Esperado,
    decimal Declarado,
    decimal Diferencia);

/// <summary>Lo que le ha faltado y sobrado a cada cajera en el período; la diferencia es suya aunque la declare el supervisor.</summary>
/// <param name="Faltantes">Suma de los faltantes, en positivo.</param>
public sealed record DatosDiferenciaCajero(
    string UsuarioNombre,
    int Cierres,
    int CierresConDiferencia,
    decimal Faltantes,
    decimal Sobrantes,
    decimal Diferencia);

/// <summary>Un rato en que la caja estuvo parada (RF-23): quién la dejó, por qué y cuánto duró.</summary>
/// <param name="Programado">Tiempo previsto (almuerzo, receso): no se cuenta junto con las paradas imprevistas.</param>
/// <param name="CerradaPorCierreDeTurno">Nadie volvió a la caja: la cerró el cierre del turno.</param>
public sealed record DatosParadaCaja(
    DateOnly FechaOperacion,
    string CajaCodigo,
    long TurnoNumero,
    string UsuarioNombre,
    string MotivoNombre,
    bool Programado,
    string? Nota,
    DateTimeOffset SuspendidaEn,
    DateTimeOffset ReanudadaEn,
    int Minutos,
    bool CerradaPorCierreDeTurno);

/// <summary>Cuánto tiempo estuvo parada una caja, o cuánto paró una cajera, en el período.</summary>
public sealed record DatosTotalParada(string Nombre, int Paradas, int Minutos, int MinutosProgramados, int MinutosImprevistos);

/// <summary>
/// Tiempos de caja parada del período: el total, y el mismo tiempo visto por caja, por cajera y por motivo, con el detalle
/// de cada parada. El tiempo previsto se separa del imprevisto: si se suman, el reporte solo dice que la caja estuvo cerrada.
/// </summary>
public sealed record DatosTiemposParada(
    int Paradas,
    int Minutos,
    int MinutosProgramados,
    int MinutosImprevistos,
    IReadOnlyList<DatosTotalParada> PorCaja,
    IReadOnlyList<DatosTotalParada> PorCajero,
    IReadOnlyList<DatosTotalParada> PorMotivo,
    IReadOnlyList<DatosParadaCaja> Detalle);

/// <summary>Un retiro, reembolso o relevo del turno, con su motivo y quién lo autorizó.</summary>
public sealed record DatosMovimientoTurno(
    DateOnly FechaOperacion,
    string CajaCodigo,
    long TurnoNumero,
    TipoMovimientoCaja Tipo,
    int Numero,
    decimal Monto,
    string Moneda,
    string? Motivo,
    string UsuarioNombre,
    string? UsuarioAnteriorNombre,
    string? AutorizadoPorNombre,
    DateTimeOffset Fecha);
