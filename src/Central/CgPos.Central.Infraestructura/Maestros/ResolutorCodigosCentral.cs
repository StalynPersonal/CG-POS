using CgPos.Central.Infraestructura.Persistencia;
using CgPos.Contratos.Catalogo;
using Microsoft.EntityFrameworkCore;

namespace CgPos.Central.Infraestructura.Maestros;

/// <summary>
/// Códigos ↔ Id del Central. Hacia un lado traduce lo que llega por código (Manager, importación, archivos) a los Id de sus tablas; hacia el otro
/// arma las cargas por código que bajan a las cajas. Los catálogos se cargan completos una vez; los artículos solo los que se piden.
/// </summary>
internal sealed class ResolutorCodigosCentral(ContextoDatosCentral contexto) : IResolutorCodigos
{
    private readonly Mapa<int> _departamentos = new("el departamento");
    private readonly Mapa<int> _categorias = new("la categoría");
    private readonly Mapa<int> _marcas = new("la marca");
    private readonly Mapa<int> _unidades = new("la unidad de medida");
    private readonly Mapa<string> _impuestos = new("el impuesto", StringComparer.OrdinalIgnoreCase);
    private readonly Mapa<string> _articulos = new("el artículo", StringComparer.Ordinal);
    private readonly Mapa<string> _sucursales = new("la sucursal", StringComparer.Ordinal);
    private readonly Mapa<(string, string)> _cajas = new("la caja");
    private readonly Mapa<string> _bancos = new("el banco", StringComparer.OrdinalIgnoreCase);
    private readonly Mapa<int> _niveles = new("el nivel de fidelidad");
    private readonly Mapa<string> _promociones = new("la promoción", StringComparer.OrdinalIgnoreCase);
    private readonly Mapa<string> _roles = new("el rol", StringComparer.OrdinalIgnoreCase);
    private bool _preparado;

    public async Task PrepararAsync(CancellationToken cancelacion)
    {
        if (_preparado)
            return;

        _departamentos.Cargar(await contexto.Departamentos.AsNoTracking().Select(e => new { e.Codigo, e.Id }).ToListAsync(cancelacion), e => (e.Codigo, e.Id));
        _categorias.Cargar(await contexto.Categorias.AsNoTracking().Select(e => new { e.Codigo, e.Id }).ToListAsync(cancelacion), e => (e.Codigo, e.Id));
        _marcas.Cargar(await contexto.Marcas.AsNoTracking().Select(e => new { e.Codigo, e.Id }).ToListAsync(cancelacion), e => (e.Codigo, e.Id));
        _unidades.Cargar(await contexto.UnidadesMedida.AsNoTracking().Select(e => new { e.Codigo, e.Id }).ToListAsync(cancelacion), e => (e.Codigo, e.Id));
        _impuestos.Cargar(await contexto.Impuestos.AsNoTracking().Select(e => new { e.Codigo, e.Id }).ToListAsync(cancelacion), e => (e.Codigo, e.Id));
        _sucursales.Cargar(await contexto.Sucursales.AsNoTracking().Select(e => new { e.Codigo, e.Id }).ToListAsync(cancelacion), e => (e.Codigo, e.Id));
        _cajas.Cargar(await contexto.Cajas.AsNoTracking().Join(contexto.Sucursales, c => c.SucursalId, s => s.Id, (c, s) => new { Sucursal = s.Codigo, c.Codigo, c.Id })
            .ToListAsync(cancelacion), e => ((e.Sucursal, e.Codigo), e.Id));
        _bancos.Cargar(await contexto.Bancos.AsNoTracking().Select(e => new { e.Codigo, e.Id }).ToListAsync(cancelacion), e => (e.Codigo, e.Id));
        _niveles.Cargar(await contexto.NivelesFidelidad.AsNoTracking().Select(e => new { e.Codigo, e.Id }).ToListAsync(cancelacion), e => (e.Codigo, e.Id));
        _promociones.Cargar(await contexto.Promociones.AsNoTracking().Select(e => new { e.Codigo, e.Id }).ToListAsync(cancelacion), e => (e.Codigo, e.Id));
        _roles.Cargar(await contexto.RolesCaja.AsNoTracking().Select(e => new { e.Codigo, e.Id }).ToListAsync(cancelacion), e => (e.Codigo, e.Id));
        _preparado = true;
    }

    /// <summary>Carga los artículos pedidos por código o por Id (los que falten), en bloques.</summary>
    public async Task CargarArticulosAsync(IEnumerable<string>? codigos, IEnumerable<int>? ids, CancellationToken cancelacion)
    {
        foreach (var bloque in (codigos ?? []).Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c.Trim()).Where(c => !_articulos.TieneCodigo(c)).Distinct().Chunk(1000))
        {
            var lista = bloque.ToList();
            foreach (var articulo in await contexto.Articulos.AsNoTracking().Where(a => lista.Contains(a.Codigo)).Select(a => new { a.Codigo, a.Id }).ToListAsync(cancelacion))
                _articulos.Registrar(articulo.Codigo, articulo.Id);
        }

        foreach (var bloque in (ids ?? []).Where(id => id > 0 && !_articulos.TieneId(id)).Distinct().Chunk(1000))
        {
            var lista = bloque.ToList();
            foreach (var articulo in await contexto.Articulos.AsNoTracking().Where(a => lista.Contains(a.Id)).Select(a => new { a.Codigo, a.Id }).ToListAsync(cancelacion))
                _articulos.Registrar(articulo.Codigo, articulo.Id);
        }
    }

    public void RegistrarDepartamento(int codigo, int id) => _departamentos.Registrar(codigo, id);
    public void RegistrarCategoria(int codigo, int id) => _categorias.Registrar(codigo, id);
    public void RegistrarMarca(int codigo, int id) => _marcas.Registrar(codigo, id);
    public void RegistrarUnidad(int codigo, int id) => _unidades.Registrar(codigo, id);
    public void RegistrarImpuesto(string codigo, int id) => _impuestos.Registrar(codigo, id);
    public void RegistrarArticulo(string codigo, int id) => _articulos.Registrar(codigo, id);
    public void RegistrarBanco(string codigo, int id) => _bancos.Registrar(codigo, id);
    public void RegistrarNivel(int codigo, int id) => _niveles.Registrar(codigo, id);
    public void RegistrarPromocion(string codigo, int id) => _promociones.Registrar(codigo, id);
    public void RegistrarRol(string codigo, int id) => _roles.Registrar(codigo, id);

    // Código -> Id
    public int Departamento(int codigo) => _departamentos.Id(codigo);
    public int Categoria(int codigo) => _categorias.Id(codigo);
    public int Marca(int codigo) => _marcas.Id(codigo);
    public int UnidadMedida(int codigo) => _unidades.Id(codigo);
    public int Impuesto(string codigo) => _impuestos.Id(codigo?.Trim() ?? string.Empty);
    public int Articulo(string codigo) => _articulos.Id(codigo?.Trim() ?? string.Empty);
    public int Sucursal(string codigo) => _sucursales.Id(codigo?.Trim() ?? string.Empty);
    public int Caja(string sucursalCodigo, string cajaCodigo) => _cajas.Id((sucursalCodigo?.Trim() ?? string.Empty, cajaCodigo?.Trim() ?? string.Empty));
    public int Banco(string codigo) => _bancos.Id(codigo?.Trim() ?? string.Empty);
    public int NivelFidelidad(int codigo) => _niveles.Id(codigo);
    public int Promocion(string codigo) => _promociones.Id(codigo?.Trim() ?? string.Empty);
    public int Rol(string codigo) => _roles.Id(codigo?.Trim() ?? string.Empty);

    // Id -> código
    public int CodigoDepartamento(int id) => _departamentos.Codigo(id);
    public int CodigoCategoria(int id) => _categorias.Codigo(id);
    public int CodigoMarca(int id) => _marcas.Codigo(id);
    public int CodigoUnidad(int id) => _unidades.Codigo(id);
    public string CodigoImpuesto(int id) => _impuestos.Codigo(id);
    public string CodigoArticulo(int id) => _articulos.Codigo(id);
    public string CodigoSucursal(int id) => _sucursales.Codigo(id);
    public (string Sucursal, string Caja) CodigoCaja(int id) => _cajas.Codigo(id);
    public string CodigoBanco(int id) => _bancos.Codigo(id);
    public int CodigoNivel(int id) => _niveles.Codigo(id);
    public string CodigoPromocion(int id) => _promociones.Codigo(id);
    public string CodigoRol(int id) => _roles.Codigo(id);

    /// <summary>Código de la referencia de una regla de acumulación, según su tipo.</summary>
    public string? ReferenciaRegla(Dominio.Fidelidad.TipoReglaAcumulacion tipo, int? id) => id is not { } valor
        ? null
        : tipo switch
        {
            Dominio.Fidelidad.TipoReglaAcumulacion.Departamento => CodigoDepartamento(valor).ToString(System.Globalization.CultureInfo.InvariantCulture),
            Dominio.Fidelidad.TipoReglaAcumulacion.Categoria => CodigoCategoria(valor).ToString(System.Globalization.CultureInfo.InvariantCulture),
            Dominio.Fidelidad.TipoReglaAcumulacion.Marca => CodigoMarca(valor).ToString(System.Globalization.CultureInfo.InvariantCulture),
            Dominio.Fidelidad.TipoReglaAcumulacion.Articulo => CodigoArticulo(valor),
            Dominio.Fidelidad.TipoReglaAcumulacion.Promocion => CodigoPromocion(valor),
            _ => null,
        };

    private sealed class Mapa<T>(string nombre, IEqualityComparer<T>? comparador = null) where T : notnull
    {
        private readonly Dictionary<T, int> _ids = new(comparador);
        private readonly Dictionary<int, T> _codigos = [];

        public void Cargar<TFila>(IEnumerable<TFila> filas, Func<TFila, (T Codigo, int Id)> par)
        {
            foreach (var (codigo, id) in filas.Select(par))
                Registrar(codigo, id);
        }

        public void Registrar(T codigo, int id)
        {
            _ids[codigo] = id;
            _codigos[id] = codigo;
        }

        public bool TieneCodigo(T codigo) => _ids.ContainsKey(codigo);

        public bool TieneId(int id) => _codigos.ContainsKey(id);

        public int Id(T codigo) =>
            _ids.TryGetValue(codigo, out var id) ? id : throw new InvalidOperationException($"No existe {nombre} con código '{Texto(codigo)}'.");

        public T Codigo(int id) =>
            _codigos.TryGetValue(id, out var codigo) ? codigo : throw new InvalidOperationException($"No existe {nombre} con Id {id}.");

        private static string Texto(T codigo) => codigo is ValueTuple<int, int> (var s, var c) ? $"{s:00}-{c:00}" : codigo.ToString() ?? string.Empty;
    }
}
