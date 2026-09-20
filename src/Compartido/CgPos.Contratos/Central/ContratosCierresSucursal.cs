using CgPos.Dominio.Pagos;

namespace CgPos.Contratos.Central;

/// <summary>Cierre de turno de una caja que entra en el cierre de la sucursal.</summary>
/// <param name="Declarado">Con las correcciones hechas desde el Central, si las hubo.</param>
/// <param name="DeclaradoPorLaCaja">Lo que declaró la terminal al cerrar.</param>
/// <param name="Ajustado">El cuadre se corrigió desde el Central: se consolida con la cifra corregida.</param>
public sealed record DatosCierreTurnoSucursal(
    string CajaCodigo,
    long TurnoNumero,
    string UsuarioNombre,
    int CantidadVentas,
    decimal TotalVentas,
    decimal Esperado,
    decimal Declarado,
    decimal Diferencia,
    DateTimeOffset CerradoEn,
    decimal DeclaradoPorLaCaja = 0m,
    bool Ajustado = false);

public sealed record DatosFormaPagoCierreSucursal(TipoFormaPago Tipo, string Nombre, string Moneda, int Transacciones, decimal Esperado, decimal Declarado,
    decimal Diferencia);

/// <param name="ADepositar">Efectivo declarado en los cierres de caja en esa moneda.</param>
/// <param name="Diferencia">Depositado menos lo que había que depositar.</param>
public sealed record DatosEfectivoCierreSucursal(string Moneda, decimal ADepositar, decimal Depositado, decimal Diferencia);

public sealed record DatosDepositoCierreSucursal(string Moneda, string BancoCodigo, string BancoNombre, string NumeroBoleta, decimal Monto, DateOnly FechaDeposito);

/// <summary>
/// Lo que se consolidaría hoy para la sucursal y el día (RF-264): sus cierres de caja, las formas de pago sumadas y el efectivo a depositar.
/// </summary>
/// <param name="Pendientes">Lo que impide consolidar (turnos con ventas sin cerrar, cierres reabiertos); vacío si se puede.</param>
/// <param name="CierreSucursalId">El cierre ya hecho de ese día, si existe.</param>
public sealed record DatosPreparacionCierreSucursal(
    int SucursalId,
    string SucursalCodigo,
    string SucursalNombre,
    DateOnly FechaOperacion,
    IReadOnlyList<DatosCierreTurnoSucursal> Cierres,
    IReadOnlyList<DatosFormaPagoCierreSucursal> FormasPago,
    IReadOnlyList<DatosEfectivoCierreSucursal> Efectivo,
    IReadOnlyList<string> Pendientes,
    int? CierreSucursalId);

/// <param name="CierresPosteriores">Cierres de caja de ese día que llegaron después de consolidar: no están en los totales.</param>
public sealed record DatosCierreSucursal(
    int Id,
    int SucursalId,
    string SucursalCodigo,
    string SucursalNombre,
    DateOnly FechaOperacion,
    int CantidadCierres,
    int CantidadVentas,
    decimal TotalVentas,
    decimal TotalEsperado,
    decimal TotalDeclarado,
    decimal Diferencia,
    IReadOnlyList<DatosFormaPagoCierreSucursal> FormasPago,
    IReadOnlyList<DatosEfectivoCierreSucursal> Efectivo,
    IReadOnlyList<DatosDepositoCierreSucursal> Depositos,
    string? Observacion,
    string CerradoPor,
    DateTimeOffset CerradoEn,
    int CierresPosteriores);

public sealed record SolicitudDepositoCierreSucursal(string Moneda, string BancoCodigo, string NumeroBoleta, decimal Monto, DateOnly FechaDeposito);

public sealed record SolicitudCierreSucursal(int SucursalId, DateOnly FechaOperacion, IReadOnlyList<SolicitudDepositoCierreSucursal> Depositos, string? Observacion);
