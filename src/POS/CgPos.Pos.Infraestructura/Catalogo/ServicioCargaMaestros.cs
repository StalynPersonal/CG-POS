using System.Text.Json;
using CgPos.Contratos.Catalogo;
using CgPos.Contratos.Serializacion;
using CgPos.Dominio.Catalogo;
using CgPos.Pos.Aplicacion.Abstracciones;
using CgPos.Pos.Aplicacion.Catalogo;
using CgPos.Pos.Aplicacion.Sincronizacion;
using CgPos.Pos.Infraestructura.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using CgPos.Dominio.Comun;

using CgPos.Dominio.Sincronizacion;

namespace CgPos.Pos.Infraestructura.Catalogo;

/// <summary>
/// Aplica los maestros que envía el Central (o un archivo en desarrollo). Todo se identifica por código: los Id son de esta caja.
/// </summary>
internal sealed class ServicioCargaMaestros(
    ContextoDatosPos contexto,
    IAuditoria auditoria,
    IProgresoActualizacion progreso,
    TimeProvider reloj,
    ILogger<ServicioCargaMaestros> registro) : ICargaMaestros
{
    /// <summary>Cada cuántos artículos se avisa del avance: mover el número en cada uno solo gasta tiempo.</summary>
    private const int CadaCuantos = 250;

    private int _creados;
    private int _actualizados;
    private int _precios;

    /// <summary>
    /// Los artículos y las categorías que ya se tocaron en esta carga. EF también los tiene rastreados, pero buscarlos
    /// ahí es recorrer la lista entera cada vez: con el catálogo completo eso solo no terminaba nunca.
    /// </summary>
    private readonly Dictionary<string, Articulo> _articulos = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<int, Categoria> _categorias = [];
    private readonly Dictionary<string, CgPos.Dominio.Clientes.Cliente> _clientes = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// De los códigos que trae esta tanda, cuáles ya están en la caja. Sin esto cada registro del paquete pregunta a la
    /// base si existe, y en la primera carga de una caja son más de medio millón de preguntas cuya respuesta siempre es no.
    /// Se pregunta solo por los de la tanda: preguntar por el maestro entero era leer cientos de miles de filas para
    /// aplicar dos mil, y cada tanda salía más lenta que la anterior.
    /// </summary>
    private HashSet<string> _articulosEnLaCaja = new(StringComparer.OrdinalIgnoreCase);
    private HashSet<string> _clientesEnLaCaja = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Cuántos códigos se preguntan por consulta: el tope de una lista IN de SQL Server es 2,100 parámetros.</summary>
    private const int CodigosPorConsulta = 1_000;

    public async Task<ResultadoCargaMaestros> AplicarDesdeArchivoAsync(string ruta, CancellationToken cancelacion = default)
    {
        if (!File.Exists(ruta))
            throw new FileNotFoundException($"No se encontró el archivo de maestros: {ruta}", ruta);

        PaqueteMaestros? paquete;
        try
        {
            await using var archivo = File.OpenRead(ruta);
            paquete = await JsonSerializer.DeserializeAsync<PaqueteMaestros>(archivo, OpcionesJson.Predeterminadas, cancelacion);
        }
        catch (JsonException excepcion)
        {
            throw new CargaMaestrosInvalidaExcepcion([$"JSON inválido en {Path.GetFileName(ruta)}: {excepcion.Message}"]);
        }

        return await AplicarAsync(paquete ?? throw new CargaMaestrosInvalidaExcepcion(["El archivo está vacío."]), $"Archivo {Path.GetFileName(ruta)}",
            avance: null, cancelacion);
    }

    public async Task<ResultadoCargaMaestros> AplicarAsync(PaqueteMaestros paquete, string origen, AvanceMaestros? avance = null,
        CancellationToken cancelacion = default)
    {
        // Sin contador, cada tanda se cuenta sola: es lo que pasa en la carga desde archivo y en las pruebas.
        var cuenta = avance ?? new AvanceMaestros();

        ArgumentNullException.ThrowIfNull(paquete);
        ArgumentException.ThrowIfNullOrWhiteSpace(origen);
        _creados = 0;
        _actualizados = 0;
        _precios = 0;
        _articulos.Clear();
        _categorias.Clear();
        _clientes.Clear();

        ValidarRepetidos(paquete);
        await ValidarCodigosArticulosAsync(paquete.Articulos ?? [], cancelacion);

        var resolutor = new ResolutorCodigosPos(contexto);
        await resolutor.PrepararAsync(
            (paquete.Promociones ?? []).SelectMany(p => p.Articulos ?? [])
                .Concat((paquete.TopesDescuento ?? []).Select(t => t.ArticuloCodigo).OfType<string>())
                .Concat((paquete.ReglasAcumulacion ?? []).Where(r => r.Tipo == Dominio.Fidelidad.TipoReglaAcumulacion.Articulo).Select(r => r.Referencia).OfType<string>()),
            cancelacion);

        try
        {
            var ahora = reloj.Ahora();

            // Lo referido antes que lo que lo refiere: monedas, clasificación, unidades e impuestos, luego artículos y lo que los usa.
            foreach (var d in paquete.Monedas ?? [])
                await AplicarAsync(contexto.Monedas, e => e.Codigo == d.Codigo.Trim().ToUpper(), () => MapeoMaestros.Crear(d), e => MapeoMaestros.Actualizar(e, d), cancelacion);
            foreach (var d in paquete.Departamentos ?? [])
                resolutor.RegistrarDepartamento(d.Codigo,
                    await AplicarAsync(contexto.Departamentos, e => e.Codigo == d.Codigo, () => MapeoMaestros.Crear(d), e => MapeoMaestros.Actualizar(e, d), cancelacion));
            foreach (var d in paquete.Categorias ?? [])
                resolutor.RegistrarCategoria(d.Codigo,
                    await AplicarAsync(contexto.Categorias, e => e.Codigo == d.Codigo, () => MapeoMaestros.Crear(d, resolutor), e => MapeoMaestros.Actualizar(e, d, resolutor), cancelacion));
            foreach (var d in paquete.Marcas ?? [])
                resolutor.RegistrarMarca(d.Codigo,
                    await AplicarAsync(contexto.Marcas, e => e.Codigo == d.Codigo, () => MapeoMaestros.Crear(d), e => MapeoMaestros.Actualizar(e, d), cancelacion));
            foreach (var d in paquete.UnidadesMedida ?? [])
                resolutor.RegistrarUnidad(d.Codigo,
                    await AplicarAsync(contexto.UnidadesMedida, e => e.Codigo == d.Codigo, () => MapeoMaestros.Crear(d), e => MapeoMaestros.Actualizar(e, d), cancelacion));
            foreach (var d in paquete.Impuestos ?? [])
                resolutor.RegistrarImpuesto(d.Codigo.Trim(),
                    await AplicarAsync(contexto.Impuestos, e => e.Codigo == d.Codigo.Trim(), () => MapeoMaestros.Crear(d), e => MapeoMaestros.Actualizar(e, d), cancelacion));

            // Los artículos son lo que tarda: una caja nueva recibe el catálogo entero y se avisa del avance mientras entra.
            var articulos = paquete.Articulos ?? [];
            if (articulos.Count > 0)
            {
                registro.LogInformation("Aplicando {Total} artículos del paquete de maestros ({Origen}).", articulos.Count, origen);
                progreso.Etapa("Actualizando la caja con artículos y precios");
                _articulosEnLaCaja = await CodigosYaEnLaCajaAsync(contexto.Articulos.AsNoTracking().Select(a => a.Codigo),
                    articulos.Select(a => a.Codigo.Trim()), cancelacion);
            }

            var hechos = 0;
            foreach (var d in articulos)
            {
                resolutor.RegistrarArticulo(d.Codigo.Trim(), await AplicarArticuloAsync(d, resolutor, origen, ahora, cancelacion));

                if (++hechos % CadaCuantos != 0 && hechos != articulos.Count)
                    continue;

                progreso.Avance(cuenta.Aplicados(TipoMaestro.Articulo) + hechos, cuenta.Total(TipoMaestro.Articulo, articulos.Count));
                if (hechos % 10_000 == 0 || hechos == articulos.Count)
                    registro.LogInformation("Maestros: {Hechos} de {Total} artículos aplicados.", hechos, articulos.Count);
            }

            cuenta.Sumar(TipoMaestro.Articulo, articulos.Count);

            // Los clientes son el padrón de la DGII entero: es la etapa más larga de la primera carga de una caja.
            var clientes = paquete.Clientes ?? [];
            if (clientes.Count > 0)
            {
                registro.LogInformation("Aplicando {Total} clientes del paquete de maestros ({Origen}).", clientes.Count, origen);
                progreso.Etapa("Actualizando la caja con clientes");
                _clientesEnLaCaja = await CodigosYaEnLaCajaAsync(contexto.Clientes.AsNoTracking().Select(c => c.Codigo),
                    clientes.Select(c => c.Codigo.Trim().ToUpperInvariant()), cancelacion);
            }

            hechos = 0;
            foreach (var d in clientes)
            {
                await AplicarClienteAsync(d, cancelacion);

                if (++hechos % CadaCuantos != 0 && hechos != clientes.Count)
                    continue;

                progreso.Avance(cuenta.Aplicados(TipoMaestro.Cliente) + hechos, cuenta.Total(TipoMaestro.Cliente, clientes.Count));
                if (hechos % 25_000 == 0 || hechos == clientes.Count)
                    registro.LogInformation("Maestros: {Hechos} de {Total} clientes aplicados.", hechos, clientes.Count);
            }
            cuenta.Sumar(TipoMaestro.Cliente, clientes.Count);

            foreach (var d in paquete.FormasPago ?? [])
            {
                await ValidarMonedaAsync(d.Moneda, $"La forma de pago '{d.Codigo}'", cancelacion);
                await AplicarAsync(contexto.FormasPago, e => e.Codigo == d.Codigo.Trim().ToUpper(), () => MapeoMaestros.Crear(d), e => MapeoMaestros.Actualizar(e, d), cancelacion);
            }
            foreach (var d in paquete.Bancos ?? [])
                resolutor.RegistrarBanco(d.Codigo.Trim().ToUpperInvariant(),
                    await AplicarAsync(contexto.Bancos, e => e.Codigo == d.Codigo.Trim().ToUpper(), () => MapeoMaestros.Crear(d), e => MapeoMaestros.Actualizar(e, d), cancelacion));
            foreach (var d in paquete.TiposTarjeta ?? [])
                await AplicarAsync(contexto.TiposTarjeta, e => e.Codigo == d.Codigo, () => MapeoMaestros.Crear(d), e => MapeoMaestros.Actualizar(e, d), cancelacion);
            foreach (var d in paquete.Denominaciones ?? [])
            {
                await ValidarMonedaAsync(d.Moneda, $"La denominación {d.Valor}", cancelacion);
                var moneda = d.Moneda.Trim().ToUpperInvariant();
                await AplicarAsync(contexto.Denominaciones, e => e.Moneda == moneda && e.Valor == d.Valor && e.Tipo == d.Tipo,
                    () => MapeoMaestros.Crear(d), e => MapeoMaestros.Actualizar(e, d), cancelacion);
            }
            foreach (var d in paquete.Promociones ?? [])
                resolutor.RegistrarPromocion(d.Codigo.Trim().ToUpperInvariant(),
                    await AplicarAsync(contexto.Promociones, e => e.Codigo == d.Codigo.Trim().ToUpper(), () => MapeoMaestros.Crear(d, resolutor),
                        e => MapeoMaestros.Actualizar(e, d, resolutor), cancelacion));
            foreach (var d in paquete.MotivosDescuento ?? [])
                await AplicarAsync(contexto.MotivosDescuento, e => e.Codigo == d.Codigo, () => MapeoMaestros.Crear(d), e => MapeoMaestros.Actualizar(e, d), cancelacion);
            foreach (var d in paquete.TopesDescuento ?? [])
                await AplicarAsync(contexto.TopesDescuento, e => e.Codigo == d.Codigo, () => MapeoMaestros.Crear(d, resolutor), e => MapeoMaestros.Actualizar(e, d, resolutor),
                    cancelacion);
            foreach (var d in paquete.TasasCambio ?? [])
            {
                await ValidarMonedaAsync(d.Moneda, "La tasa de cambio", cancelacion);
                var moneda = d.Moneda.Trim().ToUpperInvariant();
                await AplicarAsync(contexto.TasasCambio, e => e.Moneda == moneda && e.VigenteDesde == d.VigenteDesde, () => MapeoMaestros.Crear(d),
                    e => MapeoMaestros.Actualizar(e, d), cancelacion);
            }
            foreach (var d in paquete.SecuenciasEcf ?? [])
            {
                await AplicarAsync(contexto.SecuenciasEcf, e => e.TipoComprobante == d.TipoComprobante && e.Desde == d.Desde, () => MapeoMaestros.Crear(d, resolutor),
                    e => MapeoMaestros.Actualizar(e, d, resolutor), cancelacion);

                // El rango queda marcado con los códigos de su caja, que no cambian aunque la base se recree.
                var rango = contexto.SecuenciasEcf.Local.First(e => e.TipoComprobante == d.TipoComprobante && e.Desde == d.Desde);
                var entrada = contexto.Entry(rango);
                entrada.Property<string>(Persistencia.Configuraciones.CodigosSecuenciaEcf.Sucursal).CurrentValue = d.SucursalCodigo.Trim();
                entrada.Property<string>(Persistencia.Configuraciones.CodigosSecuenciaEcf.Caja).CurrentValue = d.CajaCodigo.Trim();
            }
            foreach (var d in paquete.MotivosSuspension ?? [])
                await AplicarAsync(contexto.MotivosSuspension, e => e.Codigo == d.Codigo,
                    () => CgPos.Dominio.Turnos.MotivoSuspension.Crear(d.Codigo, d.Nombre, d.Programado, d.ExigeNota),
                    e => e.Actualizar(d.Nombre, d.Programado, d.ExigeNota, d.Activo), cancelacion);
            foreach (var d in paquete.MotivosDevolucion ?? [])
                await AplicarAsync(contexto.MotivosDevolucion, e => e.Codigo == d.Codigo, () => MapeoMaestros.Crear(d), e => MapeoMaestros.Actualizar(e, d), cancelacion);
            foreach (var d in paquete.NivelesFidelidad ?? [])
                resolutor.RegistrarNivel(d.Codigo,
                    await AplicarAsync(contexto.NivelesFidelidad, e => e.Codigo == d.Codigo, () => MapeoMaestros.Crear(d), e => MapeoMaestros.Actualizar(e, d), cancelacion));
            foreach (var d in paquete.ReglasAcumulacion ?? [])
                await AplicarAsync(contexto.ReglasAcumulacion, e => e.Codigo == d.Codigo, () => MapeoMaestros.Crear(d, resolutor), e => MapeoMaestros.Actualizar(e, d, resolutor),
                    cancelacion);
            foreach (var d in paquete.MiembrosFidelidad ?? [])
            {
                // La misma persona inscrita sin conexión en dos cajas llega del Central una sola vez: la cédula la identifica.
                var cedula = Dominio.Fidelidad.MiembroFidelidad.ValidarCedula(d.Cedula);
                await AplicarAsync(contexto.MiembrosFidelidad, e => e.Cedula == cedula, () => MapeoMaestros.Crear(d, resolutor, ahora),
                    e => MapeoMaestros.Actualizar(e, d, resolutor), cancelacion);
            }
            foreach (var d in paquete.DescuentosTarjeta ?? [])
                await AplicarAsync(contexto.DescuentosTarjeta, e => e.Codigo == d.Codigo.Trim().ToUpper(), () => MapeoMaestros.Crear(d, resolutor),
                    e => MapeoMaestros.Actualizar(e, d, resolutor), cancelacion);

            // Guardar tarda, pero no se anuncia: la pantalla se queda diciendo con qué venía trabajando, que es lo que el
            // cajero necesita saber. Cambiar el texto aquí solo agregaba una frase más que no dice nada.
            var resultado = new ResultadoCargaMaestros(_creados, _actualizados, _precios);
            auditoria.Registrar(new EntradaAuditoria("Catalogo.CargaMaestros", "Maestros", Detalle: new { Origen = origen, resultado.Creados, resultado.Actualizados, resultado.PreciosRegistrados }));
            await contexto.SaveChangesAsync(cancelacion);

            registro.LogInformation("Maestros aplicados ({Origen}): {Creados} creados, {Actualizados} actualizados, {Precios} precios registrados",
                origen, resultado.Creados, resultado.Actualizados, resultado.PreciosRegistrados);

            // Lo guardado se suelta. En el aprovisionamiento son cientos de tandas seguidas con el mismo contexto: dejarlas
            // rastreadas hace que cada guardado recorra todo lo anterior y la carga se va frenando sola.
            contexto.ChangeTracker.Clear();
            return resultado;
        }
        catch (Exception excepcion) when (excepcion is ArgumentException or InvalidOperationException or DbUpdateException)
        {
            contexto.ChangeTracker.Clear();
            var detalle = excepcion is DbUpdateException { InnerException: { } interna } ? interna.Message : ValidacionMaestros.MensajeError(excepcion);
            throw new CargaMaestrosInvalidaExcepcion([detalle]);
        }
    }

    private Task<int> AplicarAsync<T>(DbSet<T> conjunto, System.Linq.Expressions.Expression<Func<T, bool>> llave, Func<T> crear, Action<T> actualizar,
        CancellationToken cancelacion) where T : Dominio.Comun.Entidad =>
        AplicarAsync(conjunto, conjunto, llave, crear, actualizar, cancelacion);

    /// <summary>Busca por la llave (código o llave natural), primero entre lo agregado en este paquete; crea o actualiza.</summary>
    private async Task<int> AplicarAsync<T>(IQueryable<T> consulta, DbSet<T> conjunto, System.Linq.Expressions.Expression<Func<T, bool>> llave, Func<T> crear,
        Action<T> actualizar, CancellationToken cancelacion) where T : Dominio.Comun.Entidad
    {
        var entidad = conjunto.Local.AsQueryable().FirstOrDefault(llave) ?? await consulta.FirstOrDefaultAsync(llave, cancelacion);
        if (entidad is null)
        {
            entidad = crear();
            conjunto.Add(entidad);
            _creados++;
        }
        else
        {
            actualizar(entidad);
            _actualizados++;
        }

        return entidad.Id;
    }

    private async Task<int> AplicarArticuloAsync(ArticuloCarga dato, ResolutorCodigosPos resolutor, string origen, DateTimeOffset ahora, CancellationToken cancelacion)
    {
        var codigo = dato.Codigo.Trim();
        var nuevo = false;

        if (!_articulos.TryGetValue(codigo, out var articulo))
        {
            // A la base solo se le pregunta por los códigos que ya estaban; los demás son altas y no hay nada que buscar.
            articulo = _articulosEnLaCaja.Contains(codigo)
                ? await contexto.Articulos.Include(a => a.Codigos).FirstOrDefaultAsync(a => a.Codigo == codigo, cancelacion)
                : null;
            nuevo = articulo is null;
        }

        // El paquete trae miles de artículos: sin el código en el mensaje, un dato malo es imposible de encontrar.
        try
        {
            if (articulo is null)
            {
                articulo = MapeoMaestros.Crear(dato, resolutor);
                contexto.Articulos.Add(articulo);
                _creados++;
            }
            else
            {
                MapeoMaestros.Actualizar(articulo, dato, resolutor);
                _actualizados++;
            }
        }
        catch (Exception excepcion) when (excepcion is ArgumentException or InvalidOperationException)
        {
            throw new InvalidOperationException($"Artículo '{codigo}': {excepcion.Message}", excepcion);
        }

        _articulos[codigo] = articulo;
        var id = articulo.Id;

        // La categoría es obligatoria (la exige el mapeo), pero en la entidad es opcional: si faltara, se dice cuál es.
        var categoria = articulo.CategoriaId is { } categoriaId
            ? await CategoriaAsync(categoriaId, cancelacion)
            : throw new InvalidOperationException($"El artículo '{codigo}' no tiene categoría.");
        if (categoria.DepartamentoId != articulo.DepartamentoId)
            throw new InvalidOperationException($"El artículo '{codigo}' tiene una categoría que no es de su departamento.");

        // Los precios son del artículo, como en el Central: llegan con él y se guardan encima de los que tenía. Un artículo
        // inactivo puede venir sin precio (lo descontinuado de un sistema viejo queda en cero): se queda sin precio, no se
        // vende, y el día que lo activen habrá que ponérselo.
        if (PreciosDelArticulo.Establecer(contexto, articulo, dato.PrecioDetalle, dato.PrecioMayor, dato.PreciosVigentesDesde ?? ahora))
            _precios++;

        return id;
    }

    /// <summary>
    /// El cliente por su código, recordando los ya vistos. Igual que con los artículos: buscar entre lo que EF lleva
    /// rastreado es recorrer la lista completa, y aquí son cientos de miles.
    /// </summary>
    private async Task AplicarClienteAsync(ClienteCarga dato, CancellationToken cancelacion)
    {
        var codigo = dato.Codigo.Trim().ToUpperInvariant();
        if (!_clientes.TryGetValue(codigo, out var cliente) && _clientesEnLaCaja.Contains(codigo))
            cliente = await contexto.Clientes.Include(c => c.Direcciones).FirstOrDefaultAsync(c => c.Codigo == codigo, cancelacion);

        try
        {
            if (cliente is null)
            {
                cliente = MapeoMaestros.Crear(dato);
                contexto.Clientes.Add(cliente);
                _creados++;
            }
            else
            {
                // La corrección del documento ya se auditó en el Central; las ventas anteriores conservan el documento con que se emitieron.
                MapeoMaestros.Actualizar(cliente, dato, corregirDocumento: true);
                _actualizados++;
            }
        }
        catch (Exception excepcion) when (excepcion is ArgumentException or InvalidOperationException)
        {
            throw new InvalidOperationException($"Cliente '{codigo}': {excepcion.Message}", excepcion);
        }

        _clientes[codigo] = cliente;
    }

    /// <summary>La categoría del artículo, recordada: si no, cada artículo la vuelve a buscar entre todas las rastreadas.</summary>
    private async Task<Categoria> CategoriaAsync(int categoriaId, CancellationToken cancelacion)
    {
        if (_categorias.TryGetValue(categoriaId, out var categoria))
            return categoria;

        categoria = contexto.Categorias.Local.FirstOrDefault(c => c.Id == categoriaId)
                    ?? await contexto.Categorias.AsNoTracking().FirstAsync(c => c.Id == categoriaId, cancelacion);
        _categorias[categoriaId] = categoria;
        return categoria;
    }

    private static void ValidarRepetidos(PaqueteMaestros paquete)
    {
        var errores = new List<string>();

        void Repetidos<T>(IEnumerable<T> valores, string campo)
        {
            foreach (var repetido in valores.GroupBy(v => v).Where(g => g.Count() > 1).Select(g => g.Key))
                errores.Add($"{campo} repetido en el paquete: {repetido}.");
        }

        Repetidos((paquete.Articulos ?? []).Select(a => a.Codigo.Trim()), "Código de artículo");
        Repetidos((paquete.Promociones ?? []).Select(p => p.Codigo.Trim().ToUpperInvariant()), "Código de promoción");
        Repetidos((paquete.Clientes ?? []).Select(c => c.Codigo.Trim().ToUpperInvariant()), "Código de cliente");
        Repetidos((paquete.Departamentos ?? []).Select(d => d.Codigo), "Código de departamento");
        Repetidos((paquete.Categorias ?? []).Select(d => d.Codigo), "Código de categoría");

        if (errores.Count > 0)
            throw new CargaMaestrosInvalidaExcepcion(errores);
    }

    /// <summary>El código interno, los de barras y los de proveedor identifican a un solo artículo: la caja busca por cualquiera.</summary>
    /// <summary>Cuáles de estos códigos ya están en la caja, preguntando por bloques y solo por los de la tanda.</summary>
    private static async Task<HashSet<string>> CodigosYaEnLaCajaAsync(IQueryable<string> enLaCaja, IEnumerable<string> delPaquete,
        CancellationToken cancelacion)
    {
        var existentes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var bloque in delPaquete.Where(c => !string.IsNullOrWhiteSpace(c)).Distinct(StringComparer.OrdinalIgnoreCase).Chunk(CodigosPorConsulta))
        {
            var codigos = bloque.ToList();
            existentes.UnionWith(await enLaCaja.Where(codigo => codigos.Contains(codigo)).ToListAsync(cancelacion));
        }

        return existentes;
    }

    private async Task ValidarCodigosArticulosAsync(IReadOnlyList<ArticuloCarga> articulos, CancellationToken cancelacion)
    {
        var errores = new List<string>();
        var codigosPorArticulo = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var articulo in articulos)
            foreach (var codigo in (articulo.CodigosBarras ?? []).Concat(articulo.CodigosProveedor ?? []).Select(c => c.Trim()))
            {
                if (codigosPorArticulo.TryGetValue(codigo, out var otro) && otro != articulo.Codigo.Trim())
                    errores.Add($"El código '{codigo}' está en los artículos '{otro}' y '{articulo.Codigo}'.");
                codigosPorArticulo[codigo] = articulo.Codigo.Trim();
            }

        foreach (var bloque in codigosPorArticulo.Keys.Chunk(1000))
        {
            var codigos = bloque.ToList();
            var enUso = await contexto.Set<CodigoArticulo>()
                .Where(c => codigos.Contains(c.Codigo))
                .Join(contexto.Articulos, c => c.ArticuloId, a => a.Id, (c, a) => new { c.Codigo, Articulo = a.Codigo })
                .ToListAsync(cancelacion);

            foreach (var usado in enUso.Where(u => codigosPorArticulo[u.Codigo] != u.Articulo))
                errores.Add($"El código '{usado.Codigo}' ya pertenece al artículo '{usado.Articulo}' en la caja.");
        }

        if (errores.Count > 0)
            throw new CargaMaestrosInvalidaExcepcion(errores);
    }

    /// <summary>Formas de pago, denominaciones y tasas solo pueden usar monedas del maestro (las del paquete o las ya cargadas).</summary>
    private async Task ValidarMonedaAsync(string? codigo, string referencia, CancellationToken cancelacion)
    {
        var normalizado = codigo?.Trim().ToUpperInvariant() ?? string.Empty;
        if (!contexto.Monedas.Local.Any(m => m.Codigo == normalizado) && !await contexto.Monedas.AnyAsync(m => m.Codigo == normalizado, cancelacion))
            throw new InvalidOperationException($"{referencia} usa la moneda '{normalizado}', que no está en el maestro de monedas.");
    }
}
