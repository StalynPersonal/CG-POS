using CgPos.Central.Aplicacion.Abstracciones;
using CgPos.Central.Aplicacion.Seguridad;
using CgPos.Contratos.Central;
using CgPos.Contratos.Catalogo;

namespace CgPos.Central.Aplicacion.Reportes;

/// <summary>
/// Los cierres de caja que informaron las terminales y su corrección desde el Central. Cerrar en la caja es definitivo:
/// esta es la única forma de arreglar un cuadre que salió mal, y deja rastro de lo que informó la caja y de quién corrigió.
/// </summary>
public interface IServicioCierresCaja
{
    Task<IReadOnlyList<DatosCierreCaja>> ListarAsync(int? sucursalId, int? cajaId, DateOnly desde, DateOnly hasta, CancellationToken cancelacion = default);

    /// <summary>Las denominaciones activas del maestro: con ellas el supervisor cuenta el efectivo.</summary>
    Task<IReadOnlyList<DatosDenominacion>> ListarDenominacionesAsync(CancellationToken cancelacion = default);

    /// <summary>Los cierres que la caja entregó y todavía nadie ha cuadrado, de la sucursal indicada.</summary>
    Task<IReadOnlyList<DatosCierreCaja>> ListarPendientesDeCuadreAsync(int sucursalId, CancellationToken cancelacion = default);

    /// <summary>
    /// El supervisor declara lo que contó: el efectivo por denominaciones y el total de cada forma de pago. Es lo que
    /// convierte el cierre entregado en un cierre cuadrado, con su faltante o sobrante.
    /// </summary>
    Task<ResultadoAdministracion> CuadrarAsync(int cierreId, SolicitudCuadreCierre solicitud, UsuarioAuditoria actor, CancellationToken cancelacion = default);

    /// <summary>Corrige lo declarado en una forma de pago del cierre. No se puede si su día ya se consolidó en la sucursal.</summary>
    Task<ResultadoAdministracion> AjustarAsync(int cierreId, SolicitudAjusteCierre solicitud, UsuarioAuditoria actor, CancellationToken cancelacion = default);

    /// <summary>El día de una sucursal sumado por forma de pago, con las cajas que todavía faltan por cuadrar.</summary>
    Task<DatosResumenCuadre> ResumenDelDiaAsync(int sucursalId, DateOnly dia, CancellationToken cancelacion = default);

    /// <summary>Lo que le faltó y le sobró a cada cajera en el período.</summary>
    Task<IReadOnlyList<DatosDiferenciaCajero>> DiferenciasPorCajeroAsync(int sucursalId, DateOnly desde, DateOnly hasta, CancellationToken cancelacion = default);

    /// <summary>Retiros, reembolsos y relevos de los turnos del período, con motivo y quién autorizó.</summary>
    Task<IReadOnlyList<DatosMovimientoTurno>> MovimientosAsync(int sucursalId, DateOnly desde, DateOnly hasta, CancellationToken cancelacion = default);

    /// <summary>
    /// El cuadre de un cierre en PDF carta, para volver a imprimirlo; nulo si el cierre no existe o no es de esa sucursal.
    /// </summary>
    Task<ArchivoReporte?> CuadreEnPdfAsync(int cierreId, int? sucursalId, CancellationToken cancelacion = default);
}
