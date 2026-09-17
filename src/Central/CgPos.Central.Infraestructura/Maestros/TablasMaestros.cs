using System.Text.Json;
using CgPos.Central.Infraestructura.Persistencia;
using CgPos.Central.Infraestructura.Sincronizacion;
using CgPos.Contratos.Catalogo;
using CgPos.Contratos.Serializacion;
using CgPos.Dominio.Catalogo;
using CgPos.Dominio.Comun;
using CgPos.Dominio.Sincronizacion;
using Microsoft.EntityFrameworkCore;

namespace CgPos.Central.Infraestructura.Maestros;

/// <summary>
/// Maestro que el Central guarda en su propia tabla (con llaves, índices y tipos) en lugar de la tabla JSON <see cref="MaestroCentral"/>.
/// Hacia afuera se ve igual que un maestro JSON: el publicador, la bajada a las cajas y el Manager siguen trabajando con el formato de carga,
/// así cada grupo de maestros pasa a su tabla sin tocar las cajas.
/// </summary>
internal abstract class TablaMaestro
{
    /// <summary>Cuándo y quién cambió el registro por última vez (columnas sombra de cada tabla de maestros).</summary>
    public const string ColumnaModificadoEn = "ModificadoEn";
    public const string ColumnaModificadoPor = "ModificadoPor";

    public abstract TipoMaestro Tipo { get; }

    /// <summary>Registros en formato de fila publicada: todos, los de ciertos Ids o los cambiados en un rango de versión.</summary>
    public abstract Task<List<MaestroCentral>> FilasAsync(ContextoDatosCentral contexto, IReadOnlyCollection<Guid>? ids, (long Desde, long Hasta)? versiones,
        CancellationToken cancelacion);

    public abstract Task<HashSet<Guid>> IdsAsync(ContextoDatosCentral contexto, CancellationToken cancelacion);

    /// <summary>Crea o actualiza el registro con las reglas del dominio.</summary>
    /// <returns><c>true</c> si algo cambió: solo entonces recibe una versión nueva y baja otra vez a las cajas.</returns>
    public abstract Task<bool> AplicarAsync(ContextoDatosCentral contexto, object dato, DateTimeOffset ahora, string usuario, CancellationToken cancelacion);

    /// <summary>Pasa a la tabla un registro que estaba en la tabla JSON, conservando cuándo y quién lo cambió.</summary>
    public Task<bool> MigrarAsync(ContextoDatosCentral contexto, MaestroCentral fila, CancellationToken cancelacion) =>
        AplicarAsync(contexto, Leer(fila), fila.ModificadoEn, fila.ModificadoPor, cancelacion);

    protected abstract object Leer(MaestroCentral fila);

    /// <summary>Los Ids en bloques para no superar el límite de parámetros de SQL Server; un único bloque nulo = sin filtro.</summary>
    public static IEnumerable<List<Guid>?> Bloques(IReadOnlyCollection<Guid>? ids) =>
        ids is null ? [(List<Guid>?)null] : ids.Distinct().Chunk(1000).Select(bloque => (List<Guid>?)bloque.ToList());
}

internal sealed class TablaMaestro<TEntidad, TCarga>(
    TipoMaestro tipo,
    Func<ContextoDatosCentral, DbSet<TEntidad>> conjunto,
    Func<TCarga, Guid> id,
    Func<TEntidad, TCarga> aCarga,
    Func<TCarga, TEntidad> crear,
    Action<TEntidad, TCarga> actualizar,
    Func<TCarga, PaqueteMaestros> envolver) : TablaMaestro
    where TEntidad : Entidad
    where TCarga : class
{
    public override TipoMaestro Tipo => tipo;

    public override async Task<List<MaestroCentral>> FilasAsync(ContextoDatosCentral contexto, IReadOnlyCollection<Guid>? ids, (long Desde, long Hasta)? versiones,
        CancellationToken cancelacion)
    {
        var consulta = conjunto(contexto).AsNoTracking();
        if (versiones is var (desde, hasta))
            consulta = consulta.Where(e => EF.Property<long>(e, ContextoDatosCentral.ColumnaVersion) > desde
                && EF.Property<long>(e, ContextoDatosCentral.ColumnaVersion) <= hasta);

        var filas = new List<MaestroCentral>();
        foreach (var bloque in Bloques(ids))
        {
            var parcial = bloque is null ? consulta : consulta.Where(e => bloque.Contains(e.Id));
            var registros = await parcial
                .Select(e => new
                {
                    Entidad = e,
                    En = EF.Property<DateTimeOffset>(e, ColumnaModificadoEn),
                    Por = EF.Property<string>(e, ColumnaModificadoPor),
                })
                .ToListAsync(cancelacion);

            foreach (var registro in registros)
            {
                var fila = FormatoMaestros.Desglosar(envolver(aCarga(registro.Entidad))).Single();
                filas.Add(MaestroCentral.Publicar(fila.Tipo, fila.Id, fila.Codigo, fila.CajaId, fila.Contenido(), registro.En, registro.Por, fila.TextoBusqueda()));
            }
        }

        return filas;
    }

    public override async Task<HashSet<Guid>> IdsAsync(ContextoDatosCentral contexto, CancellationToken cancelacion) =>
        (await conjunto(contexto).AsNoTracking().Select(e => e.Id).ToListAsync(cancelacion)).ToHashSet();

    public override async Task<bool> AplicarAsync(ContextoDatosCentral contexto, object dato, DateTimeOffset ahora, string usuario, CancellationToken cancelacion)
    {
        var carga = (TCarga)dato;
        var entidad = await conjunto(contexto).FindAsync([id(carga)], cancelacion);
        if (entidad is null)
        {
            entidad = crear(carga);
            conjunto(contexto).Add(entidad);
            Marcar(contexto, entidad, ahora, usuario);
            return true;
        }

        var antes = Serializar(aCarga(entidad));
        actualizar(entidad, carga);
        if (Serializar(aCarga(entidad)) == antes)
            return false;

        Marcar(contexto, entidad, ahora, usuario);
        return true;
    }

    protected override object Leer(MaestroCentral fila) => FormatoMaestros.Leer<TCarga>(fila);

    private static void Marcar(ContextoDatosCentral contexto, TEntidad entidad, DateTimeOffset ahora, string usuario)
    {
        var entrada = contexto.Entry(entidad);
        entrada.Property(ColumnaModificadoEn).CurrentValue = ahora;
        entrada.Property(ColumnaModificadoPor).CurrentValue = usuario.Length > MaestroCentral.LargoMaximoUsuario ? usuario[..MaestroCentral.LargoMaximoUsuario] : usuario;
    }

    private static string Serializar(TCarga carga) => JsonSerializer.Serialize(carga, OpcionesJson.Predeterminadas);
}

/// <summary>Maestros que ya tienen su tabla, en el orden en que se aplican (lo referido antes que lo que lo refiere).</summary>
internal static class TablasMaestros
{
    public static IReadOnlyList<TablaMaestro> Todas { get; } =
    [
        new TablaMaestro<Departamento, DepartamentoCarga>(
            TipoMaestro.Departamento, c => c.Departamentos, d => d.Id,
            e => new DepartamentoCarga(e.Id, e.Codigo, e.Nombre, e.PermiteDescuentoManual, e.EsNoCodificada, e.Activa),
            d => Activar(Departamento.Crear(d.Codigo, d.Nombre, d.PermiteDescuentoManual, d.EsNoCodificada, d.Id), d.Activa),
            (e, d) =>
            {
                ExigirMismoCodigo(e.Codigo, d.Codigo, "del departamento");
                e.Actualizar(d.Nombre, d.PermiteDescuentoManual, d.EsNoCodificada);
                Activar(e, d.Activa);
            },
            d => new PaqueteMaestros(Departamentos: [d])),

        new TablaMaestro<Categoria, CategoriaCarga>(
            TipoMaestro.Categoria, c => c.Categorias, d => d.Id,
            e => new CategoriaCarga(e.Id, e.Codigo, e.Nombre, e.DepartamentoId, e.Activa),
            d => Activar(Categoria.Crear(d.Codigo, d.Nombre, d.DepartamentoId, d.Id), d.Activa),
            (e, d) =>
            {
                ExigirMismoCodigo(e.Codigo, d.Codigo, "de la categoría");
                e.Actualizar(d.Nombre, d.DepartamentoId);
                Activar(e, d.Activa);
            },
            d => new PaqueteMaestros(Categorias: [d])),

        new TablaMaestro<Marca, MarcaCarga>(
            TipoMaestro.Marca, c => c.Marcas, d => d.Id,
            e => new MarcaCarga(e.Id, e.Codigo, e.Nombre, e.Activa),
            d => Activar(Marca.Crear(d.Codigo, d.Nombre, d.Id), d.Activa),
            (e, d) =>
            {
                ExigirMismoCodigo(e.Codigo, d.Codigo, "de la marca");
                e.CambiarNombre(d.Nombre);
                Activar(e, d.Activa);
            },
            d => new PaqueteMaestros(Marcas: [d])),

        new TablaMaestro<UnidadMedida, UnidadMedidaCarga>(
            TipoMaestro.UnidadMedida, c => c.UnidadesMedida, d => d.Id,
            e => new UnidadMedidaCarga(e.Id, e.Codigo, e.Nombre, e.PermiteDecimales, e.Decimales),
            d => UnidadMedida.Crear(d.Codigo, d.Nombre, d.PermiteDecimales, d.Decimales, d.Id),
            (e, d) =>
            {
                ExigirMismoCodigo(e.Codigo, d.Codigo, "de la unidad de medida");
                e.Actualizar(d.Nombre, d.PermiteDecimales, d.Decimales);
            },
            d => new PaqueteMaestros(UnidadesMedida: [d])),

        new TablaMaestro<Impuesto, ImpuestoCarga>(
            TipoMaestro.Impuesto, c => c.Impuestos, d => d.Id,
            e => new ImpuestoCarga(e.Id, e.Codigo, e.Nombre, e.Porcentaje, e.IndicadorFacturacion, e.Activo),
            d => Activar(Impuesto.Crear(d.Codigo, d.Nombre, d.Porcentaje, d.IndicadorFacturacion, d.Id), d.Activo),
            (e, d) =>
            {
                ExigirMismoCodigo(e.Codigo, d.Codigo, "del impuesto");
                e.Actualizar(d.Nombre, d.Porcentaje, d.IndicadorFacturacion);
                Activar(e, d.Activo);
            },
            d => new PaqueteMaestros(Impuestos: [d])),
    ];

    private static readonly Dictionary<TipoMaestro, TablaMaestro> PorTipo = Todas.ToDictionary(t => t.Tipo);

    public static TablaMaestro? Buscar(TipoMaestro tipo) => PorTipo.GetValueOrDefault(tipo);

    public static bool TieneTabla(TipoMaestro tipo) => PorTipo.ContainsKey(tipo);

    private static void ExigirMismoCodigo(string actual, string nuevo, string entidad)
    {
        if (!string.Equals(actual, nuevo.Trim(), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"No se puede cambiar el código {entidad} '{actual}'; cree uno nuevo.");
    }

    private static T Activar<T>(T entidad, bool activa) where T : Entidad
    {
        switch (entidad)
        {
            case Departamento d: if (activa) d.Activar(); else d.Desactivar(); break;
            case Categoria c: if (activa) c.Activar(); else c.Desactivar(); break;
            case Marca m: if (activa) m.Activar(); else m.Desactivar(); break;
            case Impuesto i: if (activa) i.Activar(); else i.Desactivar(); break;
        }

        return entidad;
    }
}

/// <summary>
/// Lectura de maestros publicados sin importar dónde se guardan: su tabla si ya la tienen, o la tabla JSON mientras llega su grupo.
/// </summary>
internal static class LecturaMaestros
{
    /// <param name="ids">Solo esos registros; nulo para todos los del tipo.</param>
    public static async Task<List<MaestroCentral>> MaestrosAsync(this ContextoDatosCentral contexto, TipoMaestro tipo, CancellationToken cancelacion,
        IReadOnlyCollection<Guid>? ids = null)
    {
        if (TablasMaestros.Buscar(tipo) is { } tabla)
            return await tabla.FilasAsync(contexto, ids, null, cancelacion);

        var filas = new List<MaestroCentral>();
        foreach (var bloque in TablaMaestro.Bloques(ids))
            filas.AddRange(await contexto.MaestrosCentral.AsNoTracking()
                .Where(m => m.Tipo == tipo && (bloque == null || bloque.Contains(m.Id)))
                .ToListAsync(cancelacion));
        return filas;
    }

    public static async Task<HashSet<Guid>> IdsMaestrosAsync(this ContextoDatosCentral contexto, TipoMaestro tipo, CancellationToken cancelacion) =>
        TablasMaestros.Buscar(tipo) is { } tabla
            ? await tabla.IdsAsync(contexto, cancelacion)
            : (await contexto.MaestrosCentral.AsNoTracking().Where(m => m.Tipo == tipo).Select(m => m.Id).ToListAsync(cancelacion)).ToHashSet();

    /// <summary>Ids de todos los maestros publicados, en su tabla o en la tabla JSON.</summary>
    public static async Task<HashSet<Guid>> IdsTodosLosMaestrosAsync(this ContextoDatosCentral contexto, CancellationToken cancelacion)
    {
        var ids = (await contexto.MaestrosCentral.AsNoTracking().Select(m => m.Id).ToListAsync(cancelacion)).ToHashSet();
        foreach (var tabla in TablasMaestros.Todas)
            ids.UnionWith(await tabla.IdsAsync(contexto, cancelacion));
        return ids;
    }
}
