using System.Linq.Expressions;
using System.Text.Json;
using CgPos.Central.Infraestructura.Persistencia;
using CgPos.Central.Infraestructura.Persistencia.Configuraciones;
using CgPos.Contratos.CargaInicial;
using CgPos.Contratos.Catalogo;
using CgPos.Contratos.Central;
using CgPos.Contratos.Serializacion;
using CgPos.Dominio.Catalogo;
using CgPos.Dominio.Clientes;
using CgPos.Dominio.Comun;
using CgPos.Dominio.Devoluciones;
using CgPos.Dominio.Entregas;
using CgPos.Dominio.Fidelidad;
using CgPos.Dominio.Fiscal;
using CgPos.Dominio.Pagos;
using CgPos.Dominio.Promociones;
using CgPos.Dominio.Seguridad;
using CgPos.Dominio.Sincronizacion;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace CgPos.Central.Infraestructura.Maestros;

/// <summary>Cómo se aplica una publicación: cuándo, quién y si admite corregir el documento de un cliente.</summary>
internal sealed record OpcionesPublicacion(DateTimeOffset Ahora, string Usuario, bool CorregirDocumentoCliente = false);

/// <summary>Precios publicados de un artículo (con impuesto) y desde cuándo rigen; se guardan junto al artículo.</summary>
internal sealed record PreciosPublicados(decimal PrecioDetalle, decimal? PrecioMayor, DateTimeOffset? VigentesDesde);

/// <summary>Maestro guardado en su tabla del Central, visto como las cargas por código que bajan a las cajas.</summary>
internal abstract class TablaMaestro
{
    public abstract TipoMaestro Tipo { get; }

    /// <summary>Crea o actualiza el registro por su código (o llave natural).</summary>
    /// <returns><c>true</c> si algo cambió: solo entonces recibe una versión nueva y baja otra vez a las cajas.</returns>
    public abstract Task<bool> AplicarAsync(ContextoDatosCentral contexto, object dato, ResolutorCodigosCentral resolutor, OpcionesPublicacion opciones,
        CancellationToken cancelacion);

    /// <summary>Cargas de los registros con versión en el rango (bajada a las cajas).</summary>
    public abstract Task<IReadOnlyList<object>> CambiosAsync(ContextoDatosCentral contexto, ResolutorCodigosCentral resolutor, long desde, long hasta,
        CancellationToken cancelacion);

    protected static void Marcar(ContextoDatosCentral contexto, object entidad, OpcionesPublicacion opciones) =>
        ColumnasMaestro.Marcar(contexto, entidad, opciones.Ahora, opciones.Usuario);

    protected static string Serializar(object carga) => JsonSerializer.Serialize(carga, carga.GetType(), OpcionesJson.Predeterminadas);
}

/// <summary>Operaciones de una tabla de maestros sin conocer su entidad: lo que usan el Manager y los servicios del Central.</summary>
internal interface ITablaCarga<TCarga> where TCarga : class
{
    TipoMaestro Tipo { get; }

    /// <summary>Id en el Central del registro con la llave de la carga; nulo si no existe.</summary>
    Task<int?> IdAsync(ContextoDatosCentral contexto, TCarga carga, CancellationToken cancelacion);

    Task<PaginaMaestros<TCarga>> PaginaAsync(ContextoDatosCentral contexto, ResolutorCodigosCentral resolutor, string? texto, int pagina, int tamano,
        CancellationToken cancelacion);

    Task<IReadOnlyList<DatosMaestroCentral<TCarga>>> TodosAsync(ContextoDatosCentral contexto, ResolutorCodigosCentral resolutor, CancellationToken cancelacion);

    /// <summary>Carga del registro con ese Id en el Central; nulo si no existe.</summary>
    Task<DatosMaestroCentral<TCarga>?> PorIdAsync(ContextoDatosCentral contexto, ResolutorCodigosCentral resolutor, int id, CancellationToken cancelacion);
}

/// <param name="Incluir">Colecciones que forman parte del registro (códigos del artículo, direcciones del cliente…).</param>
/// <param name="Llave">Registro de la tabla que corresponde a la carga (mismo código o llave natural).</param>
/// <param name="ACarga">Carga por código; los precios del artículo llegan aparte.</param>
/// <param name="AlGuardar">Después de crear o actualizar: registra el código en el resolutor y guarda lo que va en columnas propias del Central.</param>
/// <param name="Orden">Orden de listado en el Manager.</param>
/// <param name="Filtro">Búsqueda del Manager por texto; nulo si el maestro se lista completo.</param>
/// <param name="AntesDeLeer">Lo que el resolutor necesita para armar las cargas (ej. los artículos de las promociones).</param>
internal sealed class TablaMaestro<TEntidad, TCarga>(
    TipoMaestro tipo,
    Func<ContextoDatosCentral, DbSet<TEntidad>> conjunto,
    Func<TCarga, Expression<Func<TEntidad, bool>>> llave,
    Func<TCarga, ResolutorCodigosCentral, OpcionesPublicacion, TEntidad> crear,
    Action<TEntidad, TCarga, ResolutorCodigosCentral, OpcionesPublicacion> actualizar,
    Func<TEntidad, ResolutorCodigosCentral, PreciosPublicados?, TCarga> aCarga,
    Func<IQueryable<TEntidad>, IOrderedQueryable<TEntidad>> orden,
    Func<IQueryable<TEntidad>, IQueryable<TEntidad>>? incluir = null,
    Action<ContextoDatosCentral, TEntidad, TCarga, ResolutorCodigosCentral, OpcionesPublicacion>? alGuardar = null,
    Func<string, Expression<Func<TEntidad, bool>>>? filtro = null,
    Func<ContextoDatosCentral, IReadOnlyList<TEntidad>, ResolutorCodigosCentral, CancellationToken, Task>? antesDeLeer = null) : TablaMaestro, ITablaCarga<TCarga>
    where TEntidad : Entidad
    where TCarga : class
{
    private readonly Func<IQueryable<TEntidad>, IQueryable<TEntidad>> _incluir = incluir ?? (consulta => consulta);

    public override TipoMaestro Tipo => tipo;

    public bool SeBusca => filtro is not null;

    public IQueryable<TEntidad> Consulta(ContextoDatosCentral contexto) => _incluir(conjunto(contexto));

    public override async Task<bool> AplicarAsync(ContextoDatosCentral contexto, object dato, ResolutorCodigosCentral resolutor, OpcionesPublicacion opciones,
        CancellationToken cancelacion)
    {
        var carga = (TCarga)dato;
        var entidad = await BuscarAsync(contexto, carga, cancelacion);
        if (entidad is null)
        {
            entidad = crear(carga, resolutor, opciones);
            conjunto(contexto).Add(entidad);
            alGuardar?.Invoke(contexto, entidad, carga, resolutor, opciones);
            Marcar(contexto, entidad, opciones);
            return true;
        }

        var antes = Serializar(aCarga(entidad, resolutor, Precios(contexto.Entry(entidad))));
        actualizar(entidad, carga, resolutor, opciones);
        alGuardar?.Invoke(contexto, entidad, carga, resolutor, opciones);
        if (Serializar(aCarga(entidad, resolutor, Precios(contexto.Entry(entidad)))) == antes)
            return false;

        Marcar(contexto, entidad, opciones);
        return true;
    }

    public async Task<int?> IdAsync(ContextoDatosCentral contexto, TCarga carga, CancellationToken cancelacion) =>
        await Consulta(contexto).AsNoTracking().Where(llave(carga)).Select(e => (int?)e.Id).FirstOrDefaultAsync(cancelacion);

    public Task<IReadOnlyList<DatosMaestroCentral<TCarga>>> TodosAsync(ContextoDatosCentral contexto, ResolutorCodigosCentral resolutor, CancellationToken cancelacion) =>
        ListarAsync(contexto, resolutor, cancelacion);

    public async Task<DatosMaestroCentral<TCarga>?> PorIdAsync(ContextoDatosCentral contexto, ResolutorCodigosCentral resolutor, int id, CancellationToken cancelacion) =>
        (await ListarAsync(contexto, resolutor, cancelacion, e => e.Id == id)).SingleOrDefault();

    /// <summary>El registro con la llave de la carga: primero lo agregado en esta misma publicación, luego la base.</summary>
    public async Task<TEntidad?> BuscarAsync(ContextoDatosCentral contexto, TCarga carga, CancellationToken cancelacion)
    {
        var condicion = llave(carga);
        return conjunto(contexto).Local.AsQueryable().FirstOrDefault(condicion)
            ?? await Consulta(contexto).FirstOrDefaultAsync(condicion, cancelacion);
    }

    public override async Task<IReadOnlyList<object>> CambiosAsync(ContextoDatosCentral contexto, ResolutorCodigosCentral resolutor, long desde, long hasta,
        CancellationToken cancelacion)
    {
        var entidades = await Consulta(contexto).AsNoTracking()
            .Where(e => EF.Property<long>(e, ContextoDatosCentral.ColumnaVersion) > desde && EF.Property<long>(e, ContextoDatosCentral.ColumnaVersion) <= hasta)
            .ToListAsync(cancelacion);
        return (await CargasAsync(contexto, resolutor, entidades, cancelacion)).Select(d => (object)d.Dato).ToList();
    }

    /// <summary>Página del Manager ordenada por código, con cuándo y quién cambió cada registro.</summary>
    public async Task<PaginaMaestros<TCarga>> PaginaAsync(ContextoDatosCentral contexto, ResolutorCodigosCentral resolutor, string? texto, int pagina, int tamano,
        CancellationToken cancelacion)
    {
        var consulta = Consulta(contexto).AsNoTracking();
        if (filtro is not null && !string.IsNullOrWhiteSpace(texto))
            consulta = consulta.Where(filtro(texto.Trim()));

        var total = await consulta.CountAsync(cancelacion);
        var entidades = await orden(consulta).Skip(pagina * tamano).Take(tamano).ToListAsync(cancelacion);
        return new PaginaMaestros<TCarga>(await CargasAsync(contexto, resolutor, entidades, cancelacion), total);
    }

    /// <summary>Todos los registros (maestros cortos) o los que cumplan la condición.</summary>
    public async Task<IReadOnlyList<DatosMaestroCentral<TCarga>>> ListarAsync(ContextoDatosCentral contexto, ResolutorCodigosCentral resolutor,
        CancellationToken cancelacion, Expression<Func<TEntidad, bool>>? condicion = null)
    {
        var consulta = Consulta(contexto).AsNoTracking();
        if (condicion is not null)
            consulta = consulta.Where(condicion);
        return await CargasAsync(contexto, resolutor, await orden(consulta).ToListAsync(cancelacion), cancelacion);
    }

    private async Task<IReadOnlyList<DatosMaestroCentral<TCarga>>> CargasAsync(ContextoDatosCentral contexto, ResolutorCodigosCentral resolutor,
        IReadOnlyList<TEntidad> entidades, CancellationToken cancelacion)
    {
        if (entidades.Count == 0)
            return [];

        await resolutor.PrepararAsync(cancelacion);
        if (antesDeLeer is not null)
            await antesDeLeer(contexto, entidades, resolutor, cancelacion);

        // Cuándo y quién (y los precios del artículo) están en columnas del Central, fuera de la entidad del dominio.
        var modificado = new Dictionary<int, (DateTimeOffset En, string Por)>();
        var precios = new Dictionary<int, PreciosPublicados>();
        foreach (var bloque in entidades.Select(e => e.Id).Chunk(1000))
        {
            var ids = bloque.ToList();
            foreach (var fila in await conjunto(contexto).AsNoTracking().Where(e => ids.Contains(e.Id))
                         .Select(e => new { e.Id, En = EF.Property<DateTimeOffset>(e, ColumnasMaestro.ModificadoEn), Por = EF.Property<string>(e, ColumnasMaestro.ModificadoPor) })
                         .ToListAsync(cancelacion))
                modificado[fila.Id] = (fila.En, fila.Por);

            if (typeof(TEntidad) == typeof(Articulo))
                foreach (var fila in await contexto.Articulos.AsNoTracking().Where(e => ids.Contains(e.Id))
                             .Select(e => new
                             {
                                 e.Id,
                                 Detalle = EF.Property<decimal>(e, ArticuloConfiguracion.PrecioDetalle),
                                 Mayor = EF.Property<decimal?>(e, ArticuloConfiguracion.PrecioMayor),
                                 Desde = EF.Property<DateTimeOffset?>(e, ArticuloConfiguracion.PreciosVigentesDesde),
                             })
                             .ToListAsync(cancelacion))
                    precios[fila.Id] = new PreciosPublicados(fila.Detalle, fila.Mayor, fila.Desde);
        }

        return entidades.Select(e =>
        {
            var (en, por) = modificado[e.Id];
            return new DatosMaestroCentral<TCarga>(aCarga(e, resolutor, precios.GetValueOrDefault(e.Id)), en, por);
        }).ToList();
    }

    private static PreciosPublicados? Precios(EntityEntry<TEntidad> entrada) =>
        entrada.Entity is Articulo
            ? new PreciosPublicados(
                (decimal?)entrada.Property(ArticuloConfiguracion.PrecioDetalle).CurrentValue ?? 0m,
                (decimal?)entrada.Property(ArticuloConfiguracion.PrecioMayor).CurrentValue,
                (DateTimeOffset?)entrada.Property(ArticuloConfiguracion.PreciosVigentesDesde).CurrentValue)
            : null;
}

/// <summary>Los maestros del Central, en el orden en que se aplican (lo referido antes que lo que lo refiere).</summary>
internal static class TablasMaestros
{
    public static TablaMaestro<Moneda, MonedaCarga> Monedas { get; } = new(
        TipoMaestro.Moneda, c => c.Monedas,
        d => e => e.Codigo == d.Codigo.Trim().ToUpper(),
        (d, r, o) => MapeoMaestros.Crear(d),
        (e, d, r, o) => MapeoMaestros.Actualizar(e, d),
        (e, r, p) => new MonedaCarga(e.Codigo, e.Nombre, e.Simbolo, e.Activa),
        q => q.OrderBy(e => e.Codigo));

    public static TablaMaestro<Departamento, DepartamentoCarga> Departamentos { get; } = new(
        TipoMaestro.Departamento, c => c.Departamentos,
        d => e => e.Codigo == d.Codigo,
        (d, r, o) => MapeoMaestros.Crear(d),
        (e, d, r, o) => MapeoMaestros.Actualizar(e, d),
        (e, r, p) => new DepartamentoCarga(e.Codigo, e.Nombre, e.PermiteDescuentoManual, e.EsNoCodificada, e.Activa),
        q => q.OrderBy(e => e.Codigo),
        alGuardar: (c, e, d, r, o) => r.RegistrarDepartamento(e.Codigo, e.Id));

    public static TablaMaestro<Categoria, CategoriaCarga> Categorias { get; } = new(
        TipoMaestro.Categoria, c => c.Categorias,
        d => e => e.Codigo == d.Codigo,
        (d, r, o) => MapeoMaestros.Crear(d, r),
        (e, d, r, o) => MapeoMaestros.Actualizar(e, d, r),
        (e, r, p) => new CategoriaCarga(e.Codigo, e.Nombre, r.CodigoDepartamento(e.DepartamentoId), e.Activa),
        q => q.OrderBy(e => e.Codigo),
        alGuardar: (c, e, d, r, o) => r.RegistrarCategoria(e.Codigo, e.Id));

    public static TablaMaestro<Marca, MarcaCarga> Marcas { get; } = new(
        TipoMaestro.Marca, c => c.Marcas,
        d => e => e.Codigo == d.Codigo,
        (d, r, o) => MapeoMaestros.Crear(d),
        (e, d, r, o) => MapeoMaestros.Actualizar(e, d),
        (e, r, p) => new MarcaCarga(e.Codigo, e.Nombre, e.Activa),
        q => q.OrderBy(e => e.Codigo),
        alGuardar: (c, e, d, r, o) => r.RegistrarMarca(e.Codigo, e.Id));

    public static TablaMaestro<UnidadMedida, UnidadMedidaCarga> UnidadesMedida { get; } = new(
        TipoMaestro.UnidadMedida, c => c.UnidadesMedida,
        d => e => e.Codigo == d.Codigo,
        (d, r, o) => MapeoMaestros.Crear(d),
        (e, d, r, o) => MapeoMaestros.Actualizar(e, d),
        (e, r, p) => new UnidadMedidaCarga(e.Codigo, e.Abreviatura, e.Nombre, e.PermiteDecimales, e.Decimales),
        q => q.OrderBy(e => e.Codigo),
        alGuardar: (c, e, d, r, o) => r.RegistrarUnidad(e.Codigo, e.Id));

    public static TablaMaestro<Impuesto, ImpuestoCarga> Impuestos { get; } = new(
        TipoMaestro.Impuesto, c => c.Impuestos,
        d => e => e.Codigo == d.Codigo.Trim().ToUpper(),
        (d, r, o) => MapeoMaestros.Crear(d),
        (e, d, r, o) => MapeoMaestros.Actualizar(e, d),
        (e, r, p) => new ImpuestoCarga(e.Codigo, e.Nombre, e.Porcentaje, e.IndicadorFacturacion, e.Activo),
        q => q.OrderBy(e => e.Codigo),
        alGuardar: (c, e, d, r, o) => r.RegistrarImpuesto(e.Codigo, e.Id));

    public static TablaMaestro<Articulo, ArticuloCarga> Articulos { get; } = new(
        TipoMaestro.Articulo, c => c.Articulos,
        d => e => e.Codigo == d.Codigo.Trim(),
        (d, r, o) => MapeoMaestros.Crear(d, r),
        (e, d, r, o) => MapeoMaestros.Actualizar(e, d, r),
        (e, r, p) => new ArticuloCarga(
            e.Codigo, e.Descripcion, r.CodigoDepartamento(e.DepartamentoId), r.CodigoUnidad(e.UnidadMedidaId), r.CodigoImpuesto(e.ImpuestoId),
            p?.PrecioDetalle ?? 0m, p?.PrecioMayor, e.Tipo, e.Referencia, e.Costo, e.PrecioMinimo, e.CantidadMinimaMayor,
            e.Codigos.Where(c => c.Tipo == TipoCodigoArticulo.Barras).Select(c => c.Codigo).OrderBy(c => c, StringComparer.Ordinal).ToList(),
            e.Codigos.Where(c => c.Tipo == TipoCodigoArticulo.Proveedor).Select(c => c.Codigo).OrderBy(c => c, StringComparer.Ordinal).ToList(),
            e.RutaImagen, e.MostrarEnCatalogo, e.VentaEnPos, e.Activo, p?.VigentesDesde, e.PesoEmpaque, e.EsServicio,
            e.CategoriaId is { } categoria ? r.CodigoCategoria(categoria) : null,
            e.MarcaId is { } marca ? r.CodigoMarca(marca) : null),
        q => q.OrderBy(e => e.Codigo),
        incluir: q => q.Include(e => e.Codigos),
        alGuardar: (c, e, d, r, o) =>
        {
            r.RegistrarArticulo(e.Codigo, e.Id);

            // Un precio nuevo rige desde lo indicado (o desde ahora); si no cambió, conserva su vigencia.
            var entrada = c.Entry(e);
            var detalle = (decimal?)entrada.Property(ArticuloConfiguracion.PrecioDetalle).CurrentValue;
            var mayor = (decimal?)entrada.Property(ArticuloConfiguracion.PrecioMayor).CurrentValue;
            var desde = (DateTimeOffset?)entrada.Property(ArticuloConfiguracion.PreciosVigentesDesde).CurrentValue;
            if (entrada.State == EntityState.Added || detalle != d.PrecioDetalle || mayor != d.PrecioMayor || (d.PreciosVigentesDesde is { } pedido && pedido != desde))
            {
                entrada.Property(ArticuloConfiguracion.PrecioDetalle).CurrentValue = d.PrecioDetalle;
                entrada.Property(ArticuloConfiguracion.PrecioMayor).CurrentValue = d.PrecioMayor;
                entrada.Property(ArticuloConfiguracion.PreciosVigentesDesde).CurrentValue = d.PreciosVigentesDesde ?? o.Ahora;
            }
        },
        filtro: texto => e => e.Codigo.Contains(texto) || e.Descripcion.Contains(texto) || (e.Referencia != null && e.Referencia.Contains(texto))
                              || e.Codigos.Any(c => c.Codigo.Contains(texto)));

    public static TablaMaestro<Cliente, ClienteCarga> Clientes { get; } = new(
        TipoMaestro.Cliente, c => c.Clientes,
        d => e => e.Codigo == d.Codigo.Trim().ToUpper(),
        (d, r, o) => MapeoMaestros.Crear(d),
        (e, d, r, o) => MapeoMaestros.Actualizar(e, d, o.CorregirDocumentoCliente),
        (e, r, p) => new ClienteCarga(e.Codigo, e.TipoDocumento, e.Documento, e.Nombre, e.TipoComprobantePredeterminado, e.ExoneradoItbis, e.AplicaRetencion,
            e.ListaPrecioPredeterminada, e.Telefono, e.Correo,
            e.Direcciones.OrderBy(x => x.Alias, StringComparer.OrdinalIgnoreCase)
                .Select(x => new DireccionClienteCarga(x.Alias, x.Direccion, x.Sector, x.Ciudad, x.Referencia, x.Telefono, x.EsPrincipal)).ToList(),
            e.Activo, e.Contacto, e.TelefonoAlterno),
        q => q.OrderBy(e => e.Codigo),
        incluir: q => q.Include(e => e.Direcciones),
        filtro: texto => e => e.Codigo.Contains(texto) || e.Documento.Contains(texto) || e.Nombre.Contains(texto)
                              || (e.Telefono != null && e.Telefono.Contains(texto)) || (e.Correo != null && e.Correo.Contains(texto)));

    public static TablaMaestro<FormaPago, FormaPagoCarga> FormasPago { get; } = new(
        TipoMaestro.FormaPago, c => c.FormasPago,
        d => e => e.Codigo == d.Codigo.Trim().ToUpper(),
        (d, r, o) => MapeoMaestros.Crear(d),
        (e, d, r, o) => MapeoMaestros.Actualizar(e, d),
        (e, r, p) => new FormaPagoCarga(e.Codigo, e.Nombre, e.Tipo, e.Orden, e.Moneda, e.AbreGaveta, e.PermiteDevuelta, e.RequiereReferencia, e.RequiereBanco,
            e.PermiteComprobanteFiscal, e.Activa),
        q => q.OrderBy(e => e.Orden).ThenBy(e => e.Codigo));

    public static TablaMaestro<Banco, BancoCarga> Bancos { get; } = new(
        TipoMaestro.Banco, c => c.Bancos,
        d => e => e.Codigo == d.Codigo.Trim().ToUpper(),
        (d, r, o) => MapeoMaestros.Crear(d),
        (e, d, r, o) => MapeoMaestros.Actualizar(e, d),
        (e, r, p) => new BancoCarga(e.Codigo, e.Nombre, e.RutaLogo, e.Activo),
        q => q.OrderBy(e => e.Codigo),
        alGuardar: (c, e, d, r, o) => r.RegistrarBanco(e.Codigo, e.Id));

    public static TablaMaestro<TipoTarjeta, TipoTarjetaCarga> TiposTarjeta { get; } = new(
        TipoMaestro.TipoTarjeta, c => c.TiposTarjeta,
        d => e => e.Codigo == d.Codigo,
        (d, r, o) => MapeoMaestros.Crear(d),
        (e, d, r, o) => MapeoMaestros.Actualizar(e, d),
        (e, r, p) => new TipoTarjetaCarga(e.Codigo, e.Nombre, e.Activo),
        q => q.OrderBy(e => e.Codigo));

    public static TablaMaestro<Denominacion, DenominacionCarga> Denominaciones { get; } = new(
        TipoMaestro.Denominacion, c => c.Denominaciones,
        d => e => e.Moneda == d.Moneda.Trim().ToUpper() && e.Valor == d.Valor && e.Tipo == d.Tipo,
        (d, r, o) => MapeoMaestros.Crear(d),
        (e, d, r, o) => MapeoMaestros.Actualizar(e, d),
        (e, r, p) => new DenominacionCarga(e.Moneda, e.Valor, e.Tipo, e.Activa),
        q => q.OrderBy(e => e.Moneda).ThenByDescending(e => e.Valor));

    public static TablaMaestro<Promocion, PromocionCarga> Promociones { get; } = new(
        TipoMaestro.Promocion, c => c.Promociones,
        d => e => e.Codigo == d.Codigo.Trim().ToUpper(),
        (d, r, o) => MapeoMaestros.Crear(d, r),
        (e, d, r, o) => MapeoMaestros.Actualizar(e, d, r),
        (e, r, p) => new PromocionCarga(e.Codigo, e.Nombre, e.Tipo, e.Valor, e.VigenteDesde, e.VigenteHasta,
            e.Articulos.Select(r.CodigoArticulo).ToList(), e.Departamentos.Select(r.CodigoDepartamento).ToList(), e.Sucursales.Select(r.CodigoSucursal).ToList(),
            e.CantidadLleva, e.CantidadPaga, e.CantidadMinima, e.LimitePorCliente, e.Dias, e.HoraDesde, e.HoraHasta, e.SoloFidelidad, e.Activa,
            e.Categorias.Select(r.CodigoCategoria).ToList(), e.Marcas.Select(r.CodigoMarca).ToList()),
        q => q.OrderBy(e => e.Codigo),
        alGuardar: (c, e, d, r, o) => r.RegistrarPromocion(e.Codigo, e.Id),
        filtro: texto => e => e.Codigo.Contains(texto) || e.Nombre.Contains(texto),
        antesDeLeer: (c, entidades, r, cancelacion) => r.CargarArticulosAsync(null, entidades.SelectMany(e => e.Articulos), cancelacion));

    public static TablaMaestro<MotivoDescuento, MotivoDescuentoCarga> MotivosDescuento { get; } = new(
        TipoMaestro.MotivoDescuento, c => c.MotivosDescuento,
        d => e => e.Codigo == d.Codigo,
        (d, r, o) => MapeoMaestros.Crear(d),
        (e, d, r, o) => MapeoMaestros.Actualizar(e, d),
        (e, r, p) => new MotivoDescuentoCarga(e.Codigo, e.Nombre, e.Activo),
        q => q.OrderBy(e => e.Codigo));

    public static TablaMaestro<TopeDescuento, TopeDescuentoCarga> TopesDescuento { get; } = new(
        TipoMaestro.TopeDescuento, c => c.TopesDescuento,
        d => e => e.Codigo == d.Codigo,
        (d, r, o) => MapeoMaestros.Crear(d, r),
        (e, d, r, o) => MapeoMaestros.Actualizar(e, d, r),
        (e, r, p) => new TopeDescuentoCarga(e.Codigo, e.Nivel, e.PorcentajeMaximo, e.MontoMaximo,
            e.DepartamentoId is { } departamento ? r.CodigoDepartamento(departamento) : null,
            e.ArticuloId is { } articulo ? r.CodigoArticulo(articulo) : null,
            e.CategoriaId is { } categoria ? r.CodigoCategoria(categoria) : null,
            e.MarcaId is { } marca ? r.CodigoMarca(marca) : null),
        q => q.OrderBy(e => e.Codigo),
        antesDeLeer: (c, entidades, r, cancelacion) => r.CargarArticulosAsync(null, entidades.Select(e => e.ArticuloId).OfType<int>(), cancelacion));

    public static TablaMaestro<TasaCambio, TasaCambioCarga> TasasCambio { get; } = new(
        TipoMaestro.TasaCambio, c => c.TasasCambio,
        d => e => e.Moneda == d.Moneda.Trim().ToUpper() && e.VigenteDesde == d.VigenteDesde,
        (d, r, o) => MapeoMaestros.Crear(d),
        (e, d, r, o) => MapeoMaestros.Actualizar(e, d),
        (e, r, p) => new TasaCambioCarga(e.Moneda, e.Tasa, e.VigenteDesde),
        q => q.OrderByDescending(e => e.VigenteDesde).ThenBy(e => e.Moneda));

    public static TablaMaestro<SecuenciaEcf, SecuenciaEcfCarga> SecuenciasEcf { get; } = new(
        TipoMaestro.SecuenciaEcf, c => c.SecuenciasEcf,
        d => e => e.TipoComprobante == d.TipoComprobante && e.Desde == d.Desde,
        (d, r, o) => MapeoMaestros.Crear(d, r),
        (e, d, r, o) => MapeoMaestros.Actualizar(e, d, r),
        (e, r, p) =>
        {
            var (sucursal, caja) = r.CodigoCaja(e.CajaId);
            return new SecuenciaEcfCarga(sucursal, caja, e.TipoComprobante, e.Desde, e.Hasta, e.VenceEn, e.Activa, Serie: e.Serie);
        },
        q => q.OrderBy(e => e.TipoComprobante).ThenBy(e => e.Desde));

    public static TablaMaestro<MotivoDevolucion, MotivoDevolucionCarga> MotivosDevolucion { get; } = new(
        TipoMaestro.MotivoDevolucion, c => c.MotivosDevolucion,
        d => e => e.Codigo == d.Codigo,
        (d, r, o) => MapeoMaestros.Crear(d),
        (e, d, r, o) => MapeoMaestros.Actualizar(e, d),
        (e, r, p) => new MotivoDevolucionCarga(e.Codigo, e.Nombre, e.Activo),
        q => q.OrderBy(e => e.Codigo));

    public static TablaMaestro<NivelFidelidad, NivelFidelidadCarga> NivelesFidelidad { get; } = new(
        TipoMaestro.NivelFidelidad, c => c.NivelesFidelidad,
        d => e => e.Codigo == d.Codigo,
        (d, r, o) => MapeoMaestros.Crear(d),
        (e, d, r, o) => MapeoMaestros.Actualizar(e, d),
        (e, r, p) => new NivelFidelidadCarga(e.Codigo, e.Nombre, e.Orden, e.FactorAcumulacion, e.Activo),
        q => q.OrderBy(e => e.Orden).ThenBy(e => e.Codigo),
        alGuardar: (c, e, d, r, o) => r.RegistrarNivel(e.Codigo, e.Id));

    public static TablaMaestro<ReglaAcumulacion, ReglaAcumulacionCarga> ReglasAcumulacion { get; } = new(
        TipoMaestro.ReglaAcumulacion, c => c.ReglasAcumulacion,
        d => e => e.Codigo == d.Codigo,
        (d, r, o) => MapeoMaestros.Crear(d, r),
        (e, d, r, o) => MapeoMaestros.Actualizar(e, d, r),
        (e, r, p) => new ReglaAcumulacionCarga(e.Codigo, e.Nombre, e.Tipo, e.MontoBase, e.Puntos, r.ReferenciaRegla(e.Tipo, e.ReferenciaId), e.DiaSemana,
            e.VigenteDesde, e.VigenteHasta, e.Activa),
        q => q.OrderBy(e => e.Codigo),
        antesDeLeer: (c, entidades, r, cancelacion) =>
            r.CargarArticulosAsync(null, entidades.Where(e => e.Tipo == TipoReglaAcumulacion.Articulo).Select(e => e.ReferenciaId).OfType<int>(), cancelacion));

    public static TablaMaestro<MiembroFidelidad, MiembroFidelidadCarga> MiembrosFidelidad { get; } = new(
        TipoMaestro.MiembroFidelidad, c => c.MiembrosFidelidad,
        d => e => e.Cedula == d.Cedula.Trim(),
        (d, r, o) => MapeoMaestros.Crear(d, r, o.Ahora),
        (e, d, r, o) => MapeoMaestros.Actualizar(e, d, r),
        (e, r, p) => new MiembroFidelidadCarga(e.Cedula, e.Nombre, e.Telefono, e.Correo, e.NivelId is { } nivel ? r.CodigoNivel(nivel) : null,
            e.SaldoSincronizado, e.SaldoSincronizadoEn, e.PuntosPorVencer, e.ProximoVencimiento, e.InscritoEn, e.Activo),
        q => q.OrderBy(e => e.Cedula),
        filtro: texto => e => e.Cedula.Contains(texto) || e.Nombre.Contains(texto) || (e.Telefono != null && e.Telefono.Contains(texto))
                              || (e.Correo != null && e.Correo.Contains(texto)));

    public static TablaMaestro<Almacen, AlmacenCarga> Almacenes { get; } = new(
        TipoMaestro.Almacen, c => c.Almacenes,
        d => e => e.Codigo == d.Codigo.Trim().ToUpper(),
        (d, r, o) => MapeoMaestros.Crear(d, r),
        (e, d, r, o) => MapeoMaestros.Actualizar(e, d, r),
        (e, r, p) => new AlmacenCarga(e.Codigo, e.Nombre, r.CodigoSucursal(e.SucursalId), e.Direccion, e.Activo),
        q => q.OrderBy(e => e.Codigo));

    public static TablaMaestro<DescuentoTarjeta, DescuentoTarjetaCarga> DescuentosTarjeta { get; } = new(
        TipoMaestro.DescuentoTarjeta, c => c.DescuentosTarjeta,
        d => e => e.Codigo == d.Codigo.Trim().ToUpper(),
        (d, r, o) => MapeoMaestros.Crear(d, r),
        (e, d, r, o) => MapeoMaestros.Actualizar(e, d, r),
        (e, r, p) => new DescuentoTarjetaCarga(e.Codigo, e.Nombre, e.Bines, e.Tipo, e.Valor, e.VigenteDesde, e.VigenteHasta, e.MontoMinimo, e.MontoMaximo,
            e.BancoId is { } banco ? r.CodigoBanco(banco) : null, e.Dias, e.Activo),
        q => q.OrderBy(e => e.Codigo));

    /// <summary>Roles de caja. Los permisos se guardan uno por fila; <c>"*"</c> en la carga asigna todos.</summary>
    public static TablaMaestro<Rol, RolCarga> RolesCaja { get; } = new(
        TipoMaestro.RolCaja, c => c.RolesCaja,
        d => e => e.Codigo == d.Codigo.Trim(),
        (d, r, o) =>
        {
            var rol = Rol.Crear(d.Codigo, d.Nombre, d.Nivel);
            AsignarPermisos(rol, d);
            return rol;
        },
        (e, d, r, o) =>
        {
            e.Actualizar(d.Nombre, d.Nivel);
            AsignarPermisos(e, d);
        },
        (e, r, p) => new RolCarga(e.Codigo, e.Nombre, e.Nivel, e.PermisosAsignados.Select(x => x.PermisoCodigo).OrderBy(x => x, StringComparer.Ordinal).ToList(), e.Activo),
        q => q.OrderBy(e => e.Nivel).ThenBy(e => e.Codigo),
        incluir: q => q.Include(e => e.PermisosAsignados),
        alGuardar: (c, e, d, r, o) => r.RegistrarRol(e.Codigo, e.Id));

    /// <summary>Usuarios de caja: la clave ya llega como hash (la calcula el publicador) y baja así a las cajas.</summary>
    public static TablaMaestro<Usuario, UsuarioCarga> UsuariosCaja { get; } = new(
        TipoMaestro.UsuarioCaja, c => c.UsuariosCaja,
        d => e => e.Codigo == d.Codigo.Trim(),
        (d, r, o) =>
        {
            var usuario = Usuario.Crear(d.Codigo, d.Nombre, r.Rol(d.RolCodigo));
            ActualizarUsuario(usuario, d, r);
            return usuario;
        },
        (e, d, r, o) =>
        {
            e.CambiarNombre(d.Nombre);
            e.CambiarRol(r.Rol(d.RolCodigo));
            ActualizarUsuario(e, d, r);
        },
        (e, r, p) => new UsuarioCarga(e.Codigo, e.Nombre, r.CodigoRol(e.RolId),
            e.CajasAsignadas.Select(x => r.CodigoCaja(x.CajaId)).OrderBy(x => x.Sucursal).ThenBy(x => x.Caja).Select(x => new CajaReferencia(x.Sucursal, x.Caja)).ToList(),
            null, e.ClaveHash, e.Activo),
        q => q.OrderBy(e => e.Codigo),
        incluir: q => q.Include(e => e.CajasAsignadas));

    /// <summary>En orden de aplicación.</summary>
    public static IReadOnlyList<TablaMaestro> Todas { get; } =
    [
        Monedas, Departamentos, Categorias, Marcas, UnidadesMedida, Impuestos, Articulos, Clientes, FormasPago, Bancos, TiposTarjeta, Denominaciones,
        Promociones, MotivosDescuento, TopesDescuento, TasasCambio, SecuenciasEcf, MotivosDevolucion, NivelesFidelidad, ReglasAcumulacion,
        MiembrosFidelidad, Almacenes, DescuentosTarjeta, RolesCaja, UsuariosCaja,
    ];

    /// <summary>La tabla de un tipo de carga (ej. <see cref="ArticuloCarga"/>).</summary>
    public static ITablaCarga<T> De<T>() where T : class =>
        Todas.OfType<ITablaCarga<T>>().SingleOrDefault() ?? throw new InvalidOperationException($"No hay tabla de maestros para {typeof(T).Name}.");

    private static void AsignarPermisos(Rol rol, RolCarga dato)
    {
        var permisos = dato.Permisos ?? [];
        var deseados = permisos.Contains("*") ? CatalogoPermisos.Todos.Select(p => p.Codigo).ToHashSet() : permisos.ToHashSet();
        foreach (var sobrante in rol.PermisosAsignados.Select(p => p.PermisoCodigo).Where(c => !deseados.Contains(c)).ToList())
            rol.QuitarPermiso(sobrante);
        foreach (var permiso in deseados)
            rol.AsignarPermiso(permiso);
        if (dato.Activo) rol.Activar(); else rol.Desactivar();
    }

    private static void ActualizarUsuario(Usuario usuario, UsuarioCarga dato, ResolutorCodigosCentral resolutor)
    {
        if (dato.ClaveHash is not null && usuario.ClaveHash != dato.ClaveHash)
            usuario.EstablecerClaveHash(dato.ClaveHash);

        var deseadas = (dato.Cajas ?? []).Select(c => resolutor.Caja(c.SucursalCodigo, c.CajaCodigo)).ToHashSet();
        foreach (var sobrante in usuario.CajasAsignadas.Select(c => c.CajaId).Where(id => !deseadas.Contains(id)).ToList())
            usuario.QuitarCaja(sobrante);
        foreach (var cajaId in deseadas)
            usuario.AsignarCaja(cajaId);
        if (dato.Activo) usuario.Activar(); else usuario.Desactivar();
    }
}
