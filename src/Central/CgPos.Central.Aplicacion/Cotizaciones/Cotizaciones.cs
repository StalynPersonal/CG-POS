using CgPos.Central.Aplicacion.Abstracciones;
using CgPos.Central.Aplicacion.Reportes;
using CgPos.Central.Aplicacion.Seguridad;
using CgPos.Contratos.Central;
using CgPos.Dominio.Cotizaciones;

namespace CgPos.Central.Aplicacion.Cotizaciones;

/// <summary>
/// Cotizaciones: el presupuesto que se le arma a un cliente desde el Central, con los precios del día congelados. Se imprime
/// o se le envía, vale los días que el negocio configure, y cualquier caja la convierte en factura buscándola por su número.
/// Se hacen aquí y no en la caja porque quien cotiza suele estar en un mostrador que no cobra.
/// </summary>
public interface IServicioCotizaciones
{
    /// <summary>La cotización tal como la consulta una caja para facturarla, con los precios congelados.</summary>
    Task<DatosCotizacionParaCaja?> BuscarParaCajaAsync(string numero, CancellationToken cancelacion = default);

    Task<IReadOnlyList<DatosCotizacion>> ListarAsync(string? buscar, EstadoCotizacion? estado, CancellationToken cancelacion = default);

    Task<DatosCotizacion?> ObtenerAsync(int cotizacionId, CancellationToken cancelacion = default);

    /// <summary>Hasta cuándo vale una cotización hecha hoy, según los días de vigencia configurados.</summary>
    Task<DateOnly> VencimientoPredeterminadoAsync(CancellationToken cancelacion = default);

    Task<ResultadoAdministracion> CrearAsync(SolicitudCotizacion solicitud, UsuarioAuditoria actor, CancellationToken cancelacion = default);

    Task<ResultadoAdministracion> ActualizarAsync(int cotizacionId, SolicitudCotizacion solicitud, UsuarioAuditoria actor,
        CancellationToken cancelacion = default);

    /// <summary>La anula con su motivo; no se borra, queda para consulta.</summary>
    Task<ResultadoAdministracion> AnularAsync(int cotizacionId, string motivo, UsuarioAuditoria actor, CancellationToken cancelacion = default);

    /// <summary>El PDF en carta que se le entrega o se le envía al cliente; nulo si la cotización no existe.</summary>
    Task<ArchivoReporte?> DocumentoAsync(int cotizacionId, CancellationToken cancelacion = default);
}
