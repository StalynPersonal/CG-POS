using CgPos.Contratos.Catalogo;
using CgPos.Dominio.Catalogo;
using CgPos.Pos.Aplicacion.Seguridad;

namespace CgPos.Pos.Aplicacion.Catalogo;

/// <summary>Aplica un <see cref="PaqueteMaestros"/> de forma idempotente, en una sola transacción.</summary>
public interface ICargaMaestros
{
    /// <param name="origen">De dónde vienen los datos (queda en la bitácora de precios), ej. "Central", "Carga inicial".</param>
    /// <exception cref="CargaMaestrosInvalidaExcepcion">El paquete tiene errores; no se guarda nada.</exception>
    Task<ResultadoCargaMaestros> AplicarAsync(PaqueteMaestros paquete, string origen, CancellationToken cancelacion = default);

    Task<ResultadoCargaMaestros> AplicarDesdeArchivoAsync(string ruta, CancellationToken cancelacion = default);
}

public sealed record ResultadoCargaMaestros(int Creados, int Actualizados, int PreciosRegistrados);

public sealed class CargaMaestrosInvalidaExcepcion(IReadOnlyList<string> errores)
    : Exception("Los maestros no son válidos:" + Environment.NewLine + string.Join(Environment.NewLine, errores.Select(e => "- " + e)))
{
    public IReadOnlyList<string> Errores { get; } = errores;
}

/// <summary>
/// Importa artículos desde CSV (separador ; o ,). Columnas obligatorias: codigo, descripcion, departamento, unidad, impuesto,
/// precio_detalle. Opcionales: precio_mayor, cantidad_minima_mayor, precio_minimo, costo, tipo, referencia,
/// codigos_barras y codigos_proveedor (separados por |), ruta_imagen, mostrar_en_catalogo, activo.
/// Las líneas con errores se informan y se omiten; las demás se guardan.
/// </summary>
public interface IImportadorArticulos
{
    Task<ResultadoImportacionArticulos> ImportarCsvAsync(Stream contenido, string origen, CancellationToken cancelacion = default);
}

public sealed record ResultadoImportacionArticulos(int Creados, int Actualizados, int PreciosRegistrados, IReadOnlyList<ErrorImportacion> Errores);

public sealed record ErrorImportacion(int Linea, string Mensaje);

public interface IConsultaArticulos
{
    /// <summary>
    /// Busca por código de barras, código de proveedor, código interno o etiqueta de balanza (en ese orden).
    /// Solo artículos activos y marcados para venta en caja.
    /// </summary>
    Task<DatosArticuloVenta?> BuscarPorCodigoAsync(string codigo, CancellationToken cancelacion = default);

    /// <summary>Búsqueda por descripción, referencia o código; todas las palabras deben coincidir (RF-132).</summary>
    Task<IReadOnlyList<DatosArticuloResumen>> BuscarAsync(string? texto, int? departamentoId = null, int maximo = 50, CancellationToken cancelacion = default);

    /// <summary>Artículos marcados para el catálogo visual de la caja (mosaicos), en orden alfabético.</summary>
    Task<IReadOnlyList<DatosArticuloResumen>> ListarCatalogoAsync(int? departamentoId = null, CancellationToken cancelacion = default);

    /// <summary>Artículos de departamentos no codificadas en orden alfabético (RF-134).</summary>
    Task<IReadOnlyList<DatosArticuloResumen>> ListarNoCodificadosAsync(int? departamentoId = null, CancellationToken cancelacion = default);

    /// <summary>Histórico de precios del artículo, del más reciente al más antiguo (RF-190).</summary>
    Task<IReadOnlyList<DatosPrecioHistorico>> ObtenerHistorialPreciosAsync(int articuloId, CancellationToken cancelacion = default);

    Task<IReadOnlyList<DatosDepartamento>> ListarDepartamentosAsync(CancellationToken cancelacion = default);
}

public interface IConsultaDocumentos
{
    /// <summary>Valida el RNC/cédula y lo busca en los clientes, que bajan del Central (RF-181, RF-182).</summary>
    Task<DatosConsultaDocumento> ConsultarAsync(string documento, CancellationToken cancelacion = default);
}

public interface IConsultaCatalogoCobro
{
    Task<DatosCatalogoCobro> ObtenerAsync(CancellationToken cancelacion = default);
}

public interface IServicioPrecios
{
    /// <summary>Registra un cambio de precio con su vigencia y lo deja en la bitácora y en auditoría (RF-190).</summary>
    Task RegistrarCambioAsync(int articuloId, ListaPrecio lista, decimal precio, DateTimeOffset vigenteDesde, string origen,
        SesionUsuario? usuario = null, CancellationToken cancelacion = default);
}
