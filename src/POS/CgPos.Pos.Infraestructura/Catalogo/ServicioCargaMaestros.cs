using System.Text.Json;
using CgPos.Contratos.Catalogo;
using CgPos.Contratos.Serializacion;
using CgPos.Dominio.Catalogo;
using CgPos.Pos.Aplicacion.Abstracciones;
using CgPos.Pos.Aplicacion.Catalogo;
using CgPos.Pos.Infraestructura.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CgPos.Pos.Infraestructura.Catalogo;

/// <summary>
/// Aplica los maestros que envía el Central (o un archivo en desarrollo). Todo se identifica por código: los Id son de esta caja.
/// </summary>
internal sealed class ServicioCargaMaestros(
    ContextoDatosPos contexto,
    IAuditoria auditoria,
    TimeProvider reloj,
    ILogger<ServicioCargaMaestros> registro) : ICargaMaestros
{
    private int _creados;
    private int _actualizados;
    private int _precios;

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

        return await AplicarAsync(paquete ?? throw new CargaMaestrosInvalidaExcepcion(["El archivo está vacío."]), $"Archivo {Path.GetFileName(ruta)}", cancelacion);
    }

    public async Task<ResultadoCargaMaestros> AplicarAsync(PaqueteMaestros paquete, string origen, CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(paquete);
        ArgumentException.ThrowIfNullOrWhiteSpace(origen);
        _creados = 0;
        _actualizados = 0;
        _precios = 0;

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
            var ahora = reloj.GetUtcNow();

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

            foreach (var d in paquete.Articulos ?? [])
                resolutor.RegistrarArticulo(d.Codigo.Trim(), await AplicarArticuloAsync(d, resolutor, origen, ahora, cancelacion));

            foreach (var d in paquete.Clientes ?? [])
                await AplicarAsync(contexto.Clientes.Include(c => c.Direcciones), contexto.Clientes, e => e.Codigo == d.Codigo.Trim().ToUpper(),
                    () => MapeoMaestros.Crear(d),
                    // La corrección del documento ya se auditó en el Central; las ventas anteriores conservan el documento con que se emitieron.
                    e => MapeoMaestros.Actualizar(e, d, corregirDocumento: true), cancelacion);
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
                await AplicarAsync(contexto.SecuenciasEcf, e => e.TipoComprobante == d.TipoComprobante && e.Desde == d.Desde, () => MapeoMaestros.Crear(d, resolutor),
                    e => MapeoMaestros.Actualizar(e, d, resolutor), cancelacion);
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
            foreach (var d in paquete.Almacenes ?? [])
                await AplicarAsync(contexto.Almacenes, e => e.Codigo == d.Codigo.Trim().ToUpper(), () => MapeoMaestros.Crear(d, resolutor),
                    e => MapeoMaestros.Actualizar(e, d, resolutor), cancelacion);
            foreach (var d in paquete.DescuentosTarjeta ?? [])
                await AplicarAsync(contexto.DescuentosTarjeta, e => e.Codigo == d.Codigo.Trim().ToUpper(), () => MapeoMaestros.Crear(d, resolutor),
                    e => MapeoMaestros.Actualizar(e, d, resolutor), cancelacion);

            var resultado = new ResultadoCargaMaestros(_creados, _actualizados, _precios);
            auditoria.Registrar(new EntradaAuditoria("Catalogo.CargaMaestros", "Maestros", Detalle: new { Origen = origen, resultado.Creados, resultado.Actualizados, resultado.PreciosRegistrados }));
            await contexto.SaveChangesAsync(cancelacion);

            registro.LogInformation("Maestros aplicados ({Origen}): {Creados} creados, {Actualizados} actualizados, {Precios} precios registrados",
                origen, resultado.Creados, resultado.Actualizados, resultado.PreciosRegistrados);
            return resultado;
        }
        catch (Exception excepcion) when (excepcion is ArgumentException or InvalidOperationException or DbUpdateException)
        {
            contexto.ChangeTracker.Clear();
            var detalle = excepcion is DbUpdateException { InnerException: { } interna } ? interna.Message : ValidacionMaestros.MensajeError(excepcion);
            throw new CargaMaestrosInvalidaExcepcion([detalle]);
        }
    }

    private Task<Guid> AplicarAsync<T>(DbSet<T> conjunto, System.Linq.Expressions.Expression<Func<T, bool>> llave, Func<T> crear, Action<T> actualizar,
        CancellationToken cancelacion) where T : Dominio.Comun.Entidad =>
        AplicarAsync(conjunto, conjunto, llave, crear, actualizar, cancelacion);

    /// <summary>Busca por la llave (código o llave natural), primero entre lo agregado en este paquete; crea o actualiza.</summary>
    private async Task<Guid> AplicarAsync<T>(IQueryable<T> consulta, DbSet<T> conjunto, System.Linq.Expressions.Expression<Func<T, bool>> llave, Func<T> crear,
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

    private async Task<Guid> AplicarArticuloAsync(ArticuloCarga dato, ResolutorCodigosPos resolutor, string origen, DateTimeOffset ahora, CancellationToken cancelacion)
    {
        var codigo = dato.Codigo.Trim();
        var id = await AplicarAsync(contexto.Articulos.Include(a => a.Codigos), contexto.Articulos, a => a.Codigo == codigo,
            () => MapeoMaestros.Crear(dato, resolutor), e => MapeoMaestros.Actualizar(e, dato, resolutor), cancelacion);

        var articulo = contexto.Articulos.Local.Single(a => a.Id == id);
        var categoria = contexto.Categorias.Local.FirstOrDefault(c => c.Id == articulo.CategoriaId)
                        ?? await contexto.Categorias.AsNoTracking().FirstAsync(c => c.Id == articulo.CategoriaId, cancelacion);
        if (categoria.DepartamentoId != articulo.DepartamentoId)
            throw new InvalidOperationException($"El artículo '{codigo}' tiene una categoría que no es de su departamento.");

        var vigenteDesde = dato.PreciosVigentesDesde ?? ahora;
        if (await RegistroPrecios.RegistrarSiCambiaAsync(contexto, id, ListaPrecio.Detalle, dato.PrecioDetalle, vigenteDesde, ahora, origen, null, null, cancelacion))
            _precios++;
        if (dato.PrecioMayor is { } mayor
            && await RegistroPrecios.RegistrarSiCambiaAsync(contexto, id, ListaPrecio.Mayor, mayor, vigenteDesde, ahora, origen, null, null, cancelacion))
            _precios++;

        return id;
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
