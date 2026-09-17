using CgPos.Central.Aplicacion.Abstracciones;
using CgPos.Central.Aplicacion.Seguridad;
using CgPos.Contratos.Central;
using CgPos.Dominio.Sincronizacion;

namespace CgPos.Central.Aplicacion.Sincronizacion;

/// <summary>Monitor de la sincronización de las cajas y del envío a la DGII (RF-277, RF-278): estado, alertas, reenvío dirigido y conflictos.</summary>
public interface IServicioMonitorCentral
{
    public const int TamanoMaximoPagina = 100;
    public const int MaximoConflictos = 500;

    Task<DatosMonitorCentral> ObtenerAsync(CancellationToken cancelacion = default);

    /// <param name="buscar">Parte del e-NCF o el trackId exacto.</param>
    /// <param name="soloConFallo">Solo pendientes cuyo envío ya falló.</param>
    /// <param name="sucursalId">Solo los comprobantes de las cajas de esa sucursal.</param>
    Task<PaginaComprobantesDgii> BuscarComprobantesAsync(EstadoEnvioDgii? estado, int? sucursalId, int? cajaId, string? buscar, bool soloConFallo, int pagina, int tamano,
        CancellationToken cancelacion = default);

    /// <returns>El XML firmado; nulo si el comprobante no existe.</returns>
    Task<string?> ObtenerXmlAsync(int comprobanteId, CancellationToken cancelacion = default);

    /// <summary>Vuelve a poner en cola de envío un e-CF rechazado o pendiente, ahora mismo (reenvío dirigido).</summary>
    Task<ResultadoAdministracion> ReenviarAsync(int comprobanteId, UsuarioAuditoria actor, CancellationToken cancelacion = default);

    Task<IReadOnlyList<DatosConflictoSincronizacion>> ListarConflictosAsync(bool abiertos, CancellationToken cancelacion = default);

    Task<ResultadoAdministracion> ResolverConflictoAsync(int conflictoId, string resolucion, UsuarioAuditoria actor, CancellationToken cancelacion = default);
}
