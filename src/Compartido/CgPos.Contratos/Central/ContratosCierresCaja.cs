using CgPos.Dominio.Pagos;

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
    bool Ciego,
    int CantidadVentas,
    decimal TotalVentas,
    decimal TotalEsperado,
    decimal TotalDeclarado,
    decimal Diferencia,
    DateTimeOffset CerradoEn,
    bool EnCierreSucursal,
    IReadOnlyList<DatosFormaPagoCierreCaja> FormasPago,
    IReadOnlyList<DatosAjusteCierre> Ajustes);

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
