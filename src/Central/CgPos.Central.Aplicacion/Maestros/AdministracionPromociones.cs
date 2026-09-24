using CgPos.Central.Aplicacion.Abstracciones;
using CgPos.Central.Aplicacion.Seguridad;
using CgPos.Contratos.Catalogo;
using CgPos.Contratos.Central;

namespace CgPos.Central.Aplicacion.Maestros;

/// <summary>Promociones del Central (RF-59): consulta con su distribución a las cajas, importación desde CSV y simulación con el motor de la caja.</summary>
public interface IServicioPromocionesCentral
{
    Task<IReadOnlyList<DatosPromocionCentral>> ListarAsync(CancellationToken cancelacion = default);

    /// <summary>
    /// Columnas obligatorias: codigo, nombre, tipo, desde, hasta. Opcionales: valor, articulos, departamentos, categorias, marcas y sucursales (códigos separados por |),
    /// lleva, paga, cantidad_minima, limite_cliente, dias (todos o lun|mar…), hora_desde, hora_hasta, solo_fidelidad, activa.
    /// Un código ya publicado actualiza esa promoción. Con cualquier error no se publica nada.
    /// </summary>
    Task<ResultadoImportacionPromociones> ImportarAsync(SolicitudImportacionPromociones solicitud, UsuarioAuditoria actor, CancellationToken cancelacion = default);

    /// <summary>
    /// Apaga o vuelve a encender una promoción. Es lo único que se le puede hacer a una promoción ya publicada: lo que
    /// define la oferta —tipo, valor, artículos, sucursales— no se cambia, porque entonces no habría manera de explicar
    /// por qué una factura de la semana pasada salió con ese descuento. Para otra oferta se rehace.
    /// </summary>
    /// <returns>Rechazo si no existe, o si se intenta encender una que ya venció.</returns>
    Task<ResultadoAdministracion> CambiarEstadoAsync(string codigo, bool activa, UsuarioAuditoria actor, CancellationToken cancelacion = default);

    /// <returns>Nulo si el artículo no existe.</returns>
    Task<ResultadoSimulacionPromociones?> SimularAsync(SolicitudSimulacionPromociones solicitud, CancellationToken cancelacion = default);

    /// <summary>Artículos de una promoción, para mostrarlos al editarla.</summary>
    Task<IReadOnlyList<ArticuloCarga>> ArticulosPorCodigoAsync(IReadOnlyList<string> codigos, CancellationToken cancelacion = default);
}
