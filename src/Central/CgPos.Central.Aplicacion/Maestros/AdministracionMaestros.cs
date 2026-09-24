using CgPos.Central.Aplicacion.Abstracciones;
using CgPos.Central.Aplicacion.Seguridad;
using CgPos.Contratos.Catalogo;
using CgPos.Contratos.Central;

namespace CgPos.Central.Aplicacion.Maestros;

/// <summary>
/// Administración de los maestros que el Central publica para las cajas (RN-24). Cada registro se identifica por su código: cada cambio pasa por el
/// publicador de maestros, con las reglas del dominio de la caja y las referencias ya publicadas, y baja en la próxima sincronización.
/// </summary>
public interface IServicioMaestrosCentral
{
    public const int TamanoMaximoPagina = 100;

    /// <typeparam name="T">Registro de carga del maestro (ej. <see cref="DepartamentoCarga"/>).</typeparam>
    Task<IReadOnlyList<DatosMaestroCentral<T>>> ListarAsync<T>(CancellationToken cancelacion = default) where T : class;

    /// <summary>Busca por código, descripción, códigos de barras…, ordenado por código.</summary>
    /// <param name="pagina">Página desde cero.</param>
    /// <param name="campo">Acota la búsqueda a un campo (ver <see cref="CamposBusquedaArticulo"/>); sin él se busca en todos.</param>
    /// <param name="filtro">Filtro propio del maestro que no es texto: en los artículos, su tipo.</param>
    /// <param name="activos">Solo los activos, solo los inactivos, o nulo para verlos todos.</param>
    Task<PaginaMaestros<T>> BuscarAsync<T>(string? texto, int pagina, int tamano, string? campo = null, string? filtro = null,
        CancellationToken cancelacion = default, bool? activos = null) where T : class;

    /// <param name="nuevo">Verdadero para crear (el código no puede existir); falso para cambiar uno existente.</param>
    Task<ResultadoAdministracion> GuardarAsync<T>(T dato, bool nuevo, UsuarioAuditoria actor, CancellationToken cancelacion = default) where T : class;

    /// <summary>Crea o cambia los datos de un artículo. Un artículo ya publicado conserva sus precios: se cambian con <see cref="CambiarPreciosAsync"/>.</summary>
    Task<ResultadoAdministracion> GuardarArticuloAsync(ArticuloCarga articulo, bool nuevo, UsuarioAuditoria actor, CancellationToken cancelacion = default);

    /// <summary>
    /// Corrige el tipo y el número de documento de un cliente: valida el formato y el dígito verificador, que no lo tenga otro cliente y que haya motivo.
    /// Queda en la auditoría con el documento anterior y el nuevo. Las facturas ya emitidas conservan los datos con que se emitieron.
    /// </summary>
    Task<ResultadoAdministracion> CorregirDocumentoClienteAsync(string codigoCliente, SolicitudCorreccionDocumentoCliente solicitud, UsuarioAuditoria actor,
        CancellationToken cancelacion = default);

    Task<ResultadoAdministracion> CambiarPreciosAsync(string codigoArticulo, SolicitudPreciosArticulo solicitud, UsuarioAuditoria actor,
        CancellationToken cancelacion = default);

    /// <summary>Topes de descuento con su alcance legible: generales, luego por departamento, marca, categoría y artículo.</summary>
    Task<IReadOnlyList<DatosTopeDescuentoCentral>> ListarTopesAsync(CancellationToken cancelacion = default);

    /// <summary>Código que se sugiere para un registro nuevo de un catálogo con código numérico (el mayor más uno); se puede cambiar al crear.</summary>
    Task<int> SiguienteCodigoAsync<T>(CancellationToken cancelacion = default) where T : class;
}
