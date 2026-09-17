using CgPos.Central.Aplicacion.Abstracciones;
using CgPos.Central.Aplicacion.Seguridad;
using CgPos.Contratos.Catalogo;
using CgPos.Contratos.Central;
using CgPos.Dominio.Sincronizacion;

namespace CgPos.Central.Aplicacion.Maestros;

/// <summary>
/// Administración de los maestros que el Central publica para las cajas (RN-24). Cada cambio pasa por el publicador de maestros, con las reglas
/// del dominio de la caja y las referencias ya publicadas, y baja en la próxima sincronización.
/// </summary>
public interface IServicioMaestrosCentral
{
    public const int TamanoMaximoPagina = 100;

    /// <typeparam name="T">Registro de carga del tipo de maestro.</typeparam>
    Task<IReadOnlyList<DatosMaestroCentral<T>>> ListarAsync<T>(TipoMaestro tipo, CancellationToken cancelacion = default);

    /// <summary>Busca en el código y en el contenido del registro (descripción, códigos de barras…), ordenado por código.</summary>
    /// <param name="pagina">Página desde cero.</param>
    Task<PaginaMaestros<T>> BuscarAsync<T>(TipoMaestro tipo, string? texto, int pagina, int tamano, CancellationToken cancelacion = default);

    /// <summary>Publica un paquete con un solo registro nuevo o cambiado.</summary>
    /// <param name="id">Id del registro publicado, para la respuesta.</param>
    Task<ResultadoAdministracion> PublicarAsync(PaqueteMaestros paquete, Guid id, UsuarioAuditoria actor, CancellationToken cancelacion = default);

    /// <summary>Crea o cambia los datos de un artículo. Un artículo ya publicado conserva sus precios: se cambian con <see cref="CambiarPreciosAsync"/>.</summary>
    Task<ResultadoAdministracion> GuardarArticuloAsync(Guid articuloId, ArticuloCarga articulo, UsuarioAuditoria actor, CancellationToken cancelacion = default);

    /// <summary>
    /// Corrige el tipo y el número de documento de un cliente: valida el formato y el dígito verificador, que no lo tenga otro cliente y que haya motivo.
    /// Queda en la auditoría con el documento anterior y el nuevo. Las facturas ya emitidas conservan los datos con que se emitieron.
    /// </summary>
    Task<ResultadoAdministracion> CorregirDocumentoClienteAsync(Guid clienteId, SolicitudCorreccionDocumentoCliente solicitud, UsuarioAuditoria actor,
        CancellationToken cancelacion = default);

    Task<ResultadoAdministracion> CambiarPreciosAsync(Guid articuloId, SolicitudPreciosArticulo solicitud, UsuarioAuditoria actor, CancellationToken cancelacion = default);

    /// <summary>Topes de descuento con su alcance legible: generales, luego por departamento y por artículo.</summary>
    Task<IReadOnlyList<DatosTopeDescuentoCentral>> ListarTopesAsync(CancellationToken cancelacion = default);
}
