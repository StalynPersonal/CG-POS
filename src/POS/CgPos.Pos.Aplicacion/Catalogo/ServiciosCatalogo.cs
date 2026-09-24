using CgPos.Contratos.Catalogo;
using CgPos.Dominio.Catalogo;
using CgPos.Pos.Aplicacion.Seguridad;

using CgPos.Contratos.Sincronizacion;

using CgPos.Dominio.Sincronizacion;

namespace CgPos.Pos.Aplicacion.Catalogo;

/// <summary>Aplica un <see cref="PaqueteMaestros"/> de forma idempotente, en una sola transacción.</summary>
/// <summary>
/// Lo que se lleva aplicado de todo lo que hay que bajar. Los maestros vienen por tandas, así que sin esto el conteo de la
/// pantalla se reiniciaría en cada una y el cajero vería «2,000 de 5,000» una y otra vez en vez de avanzar hacia el total.
/// </summary>
public sealed class AvanceMaestros(IReadOnlyList<ConteoMaestro>? totales = null)
{
    private readonly Dictionary<TipoMaestro, int> _totales = totales?.GroupBy(c => c.Tipo).ToDictionary(g => g.Key, g => g.Sum(c => c.Cantidad)) ?? [];
    private readonly Dictionary<TipoMaestro, int> _aplicados = [];

    /// <summary>Lo aplicado en las tandas anteriores; en la primera, cero.</summary>
    public int Aplicados(TipoMaestro tipo) => _aplicados.GetValueOrDefault(tipo);

    /// <summary>El total que hay que bajar. Si el Central no lo dijo, lo que se lleva más lo de esta tanda: nunca menos de lo hecho.</summary>
    public int Total(TipoMaestro tipo, int enLaTanda) => Math.Max(_totales.GetValueOrDefault(tipo), Aplicados(tipo) + enLaTanda);

    /// <summary>Cierra la tanda: lo que trajo pasa a contar como aplicado.</summary>
    public void Sumar(TipoMaestro tipo, int cantidad) => _aplicados[tipo] = Aplicados(tipo) + cantidad;

    /// <summary>Los totales que informó el Central, que llegan con la primera tanda.</summary>
    public void FijarTotales(IReadOnlyList<ConteoMaestro> totales)
    {
        ArgumentNullException.ThrowIfNull(totales);
        foreach (var conteo in totales)
            _totales[conteo.Tipo] = conteo.Cantidad;
    }
}

public interface ICargaMaestros
{
    /// <param name="origen">De dónde vienen los datos (queda en la auditoría de la carga), ej. "Central", "Carga inicial".</param>
    /// <exception cref="CargaMaestrosInvalidaExcepcion">El paquete tiene errores; no se guarda nada.</exception>
    /// <param name="avance">Lo que se lleva aplicado del total, para que el conteo de la pantalla no se reinicie en cada tanda.</param>
    Task<ResultadoCargaMaestros> AplicarAsync(PaqueteMaestros paquete, string origen, AvanceMaestros? avance = null, CancellationToken cancelacion = default);

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
