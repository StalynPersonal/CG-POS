using CgPos.Central.Aplicacion.Abstracciones;
using CgPos.Central.Aplicacion.Seguridad;
using CgPos.Contratos.Central;

namespace CgPos.Central.Aplicacion.Reportes;

/// <summary>
/// Los cierres de caja que informaron las terminales y su corrección desde el Central. Cerrar en la caja es definitivo:
/// esta es la única forma de arreglar un cuadre que salió mal, y deja rastro de lo que informó la caja y de quién corrigió.
/// </summary>
public interface IServicioCierresCaja
{
    Task<IReadOnlyList<DatosCierreCaja>> ListarAsync(int? sucursalId, int? cajaId, DateOnly desde, DateOnly hasta, CancellationToken cancelacion = default);

    /// <summary>Corrige lo declarado en una forma de pago del cierre. No se puede si su día ya se consolidó en la sucursal.</summary>
    Task<ResultadoAdministracion> AjustarAsync(int cierreId, SolicitudAjusteCierre solicitud, UsuarioAuditoria actor, CancellationToken cancelacion = default);
}
