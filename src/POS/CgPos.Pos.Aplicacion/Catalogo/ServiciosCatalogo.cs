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
/// Importa artículos desde CSV (separador ; o ,). Columnas obligatorias: codigo, descripcion, familia, unidad, impuesto,
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

/// <summary>
/// Importa el padrón de RNC de la DGII (archivo de texto separado por |): documento, razón social, nombre comercial, …,
/// estado y régimen de pago en las dos últimas columnas. Inserta los nuevos y actualiza los que cambiaron.
/// </summary>
public interface IImportadorPadronDgii
{
    Task<ResultadoImportacionPadron> ImportarAsync(Stream contenido, CancellationToken cancelacion = default);
}

public sealed record ResultadoImportacionPadron(int LineasLeidas, int RegistrosValidos, int LineasDescartadas);

public interface IConsultaArticulos
{
    /// <summary>
    /// Busca por código de barras, código de proveedor, código interno o etiqueta de balanza (en ese orden).
    /// Solo artículos activos y marcados para venta en caja.
    /// </summary>
    Task<DatosArticuloVenta?> BuscarPorCodigoAsync(string codigo, CancellationToken cancelacion = default);

    /// <summary>Búsqueda por descripción, referencia o código; todas las palabras deben coincidir (RF-132).</summary>
    Task<IReadOnlyList<DatosArticuloResumen>> BuscarAsync(string? texto, Guid? familiaId = null, int maximo = 50, CancellationToken cancelacion = default);

    /// <summary>Artículos marcados para el catálogo visual de la caja (mosaicos), en orden alfabético.</summary>
    Task<IReadOnlyList<DatosArticuloResumen>> ListarCatalogoAsync(Guid? familiaId = null, CancellationToken cancelacion = default);

    /// <summary>Artículos de familias no codificadas en orden alfabético (RF-134).</summary>
    Task<IReadOnlyList<DatosArticuloResumen>> ListarNoCodificadosAsync(Guid? familiaId = null, CancellationToken cancelacion = default);

    /// <summary>Histórico de precios del artículo, del más reciente al más antiguo (RF-190).</summary>
    Task<IReadOnlyList<DatosPrecioHistorico>> ObtenerHistorialPreciosAsync(Guid articuloId, CancellationToken cancelacion = default);

    Task<IReadOnlyList<DatosFamilia>> ListarFamiliasAsync(CancellationToken cancelacion = default);
}

public interface IConsultaDocumentos
{
    /// <summary>Valida el RNC/cédula, lo busca en el padrón DGII local y en los clientes registrados (RF-181, RF-182).</summary>
    Task<DatosConsultaDocumento> ConsultarAsync(string documento, CancellationToken cancelacion = default);
}

public interface IConsultaCatalogoCobro
{
    Task<DatosCatalogoCobro> ObtenerAsync(CancellationToken cancelacion = default);
}

public interface IServicioPrecios
{
    /// <summary>Registra un cambio de precio con su vigencia y lo deja en la bitácora y en auditoría (RF-190).</summary>
    Task RegistrarCambioAsync(Guid articuloId, ListaPrecio lista, decimal precio, DateTimeOffset vigenteDesde, string origen,
        SesionUsuario? usuario = null, CancellationToken cancelacion = default);
}
