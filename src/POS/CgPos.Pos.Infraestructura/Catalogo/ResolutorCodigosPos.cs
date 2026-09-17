using CgPos.Contratos.Catalogo;
using CgPos.Pos.Infraestructura.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace CgPos.Pos.Infraestructura.Catalogo;

/// <summary>
/// Traduce los códigos que envía el Central a los Id de esta caja. Carga los catálogos una vez por paquete y ve también lo creado en el mismo
/// paquete (lo agregado al contexto todavía sin guardar).
/// </summary>
internal sealed class ResolutorCodigosPos(ContextoDatosPos contexto) : IResolutorCodigos
{
    private Dictionary<int, int> _departamentos = [];
    private Dictionary<int, int> _categorias = [];
    private Dictionary<int, int> _marcas = [];
    private Dictionary<int, int> _unidades = [];
    private Dictionary<string, int> _impuestos = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, int> _articulos = new(StringComparer.Ordinal);
    private Dictionary<int, int> _sucursales = [];
    private Dictionary<(int, int), int> _cajas = [];
    private Dictionary<string, int> _bancos = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<int, int> _niveles = [];
    private Dictionary<string, int> _promociones = new(StringComparer.OrdinalIgnoreCase);

    /// <param name="articulos">Códigos de artículo que el paquete referencia (promociones, topes, reglas): no se cargan los 150 mil.</param>
    public async Task PrepararAsync(IEnumerable<string> articulos, CancellationToken cancelacion)
    {
        _departamentos = await contexto.Departamentos.AsNoTracking().ToDictionaryAsync(e => e.Codigo, e => e.Id, cancelacion);
        _categorias = await contexto.Categorias.AsNoTracking().ToDictionaryAsync(e => e.Codigo, e => e.Id, cancelacion);
        _marcas = await contexto.Marcas.AsNoTracking().ToDictionaryAsync(e => e.Codigo, e => e.Id, cancelacion);
        _unidades = await contexto.UnidadesMedida.AsNoTracking().ToDictionaryAsync(e => e.Codigo, e => e.Id, cancelacion);
        _impuestos = await contexto.Impuestos.AsNoTracking().ToDictionaryAsync(e => e.Codigo, e => e.Id, StringComparer.OrdinalIgnoreCase, cancelacion);
        _sucursales = await contexto.Sucursales.AsNoTracking().ToDictionaryAsync(e => e.Codigo, e => e.Id, cancelacion);
        _cajas = (await contexto.Cajas.AsNoTracking()
                .Join(contexto.Sucursales, c => c.SucursalId, s => s.Id, (c, s) => new { Sucursal = s.Codigo, c.Codigo, c.Id })
                .ToListAsync(cancelacion))
            .ToDictionary(c => (c.Sucursal, c.Codigo), c => c.Id);
        _bancos = await contexto.Bancos.AsNoTracking().ToDictionaryAsync(e => e.Codigo, e => e.Id, StringComparer.OrdinalIgnoreCase, cancelacion);
        _niveles = await contexto.NivelesFidelidad.AsNoTracking().ToDictionaryAsync(e => e.Codigo, e => e.Id, cancelacion);
        _promociones = await contexto.Promociones.AsNoTracking().ToDictionaryAsync(e => e.Codigo, e => e.Id, StringComparer.OrdinalIgnoreCase, cancelacion);

        _articulos = new(StringComparer.Ordinal);
        foreach (var bloque in articulos.Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c.Trim()).Distinct().Chunk(1000))
        {
            var codigos = bloque.ToList();
            foreach (var articulo in await contexto.Articulos.AsNoTracking().Where(a => codigos.Contains(a.Codigo)).Select(a => new { a.Codigo, a.Id })
                         .ToListAsync(cancelacion))
                _articulos[articulo.Codigo] = articulo.Id;
        }
    }

    /// <summary>Id del artículo en esta caja, o nulo si todavía no existe.</summary>
    public async Task<int?> ArticuloExistenteAsync(string codigo, CancellationToken cancelacion)
    {
        var limpio = codigo.Trim();
        if (_articulos.TryGetValue(limpio, out var id))
            return id;

        return await contexto.Articulos.AsNoTracking().Where(a => a.Codigo == limpio).Select(a => (int?)a.Id).SingleOrDefaultAsync(cancelacion);
    }

    public void RegistrarDepartamento(int codigo, int id) => _departamentos[codigo] = id;
    public void RegistrarCategoria(int codigo, int id) => _categorias[codigo] = id;
    public void RegistrarMarca(int codigo, int id) => _marcas[codigo] = id;
    public void RegistrarUnidad(int codigo, int id) => _unidades[codigo] = id;
    public void RegistrarImpuesto(string codigo, int id) => _impuestos[codigo] = id;
    public void RegistrarArticulo(string codigo, int id) => _articulos[codigo] = id;
    public void RegistrarBanco(string codigo, int id) => _bancos[codigo] = id;
    public void RegistrarNivel(int codigo, int id) => _niveles[codigo] = id;
    public void RegistrarPromocion(string codigo, int id) => _promociones[codigo] = id;

    public int Departamento(int codigo) => Buscar(_departamentos, codigo, "el departamento");
    public int Categoria(int codigo) => Buscar(_categorias, codigo, "la categoría");
    public int Marca(int codigo) => Buscar(_marcas, codigo, "la marca");
    public int UnidadMedida(int codigo) => Buscar(_unidades, codigo, "la unidad de medida");
    public int Impuesto(string codigo) => Buscar(_impuestos, codigo?.Trim() ?? string.Empty, "el impuesto");
    public int Articulo(string codigo) => Buscar(_articulos, codigo?.Trim() ?? string.Empty, "el artículo");
    public int Sucursal(int codigo) => Buscar(_sucursales, codigo, "la sucursal");
    public int Banco(string codigo) => Buscar(_bancos, codigo?.Trim() ?? string.Empty, "el banco");
    public int NivelFidelidad(int codigo) => Buscar(_niveles, codigo, "el nivel de fidelidad");
    public int Promocion(string codigo) => Buscar(_promociones, codigo?.Trim() ?? string.Empty, "la promoción");

    public int Caja(int sucursalCodigo, int cajaCodigo) =>
        _cajas.TryGetValue((sucursalCodigo, cajaCodigo), out var id)
            ? id
            : throw new InvalidOperationException($"No existe en la caja la caja {cajaCodigo:00} de la sucursal {sucursalCodigo:00}.");

    private static int Buscar<T>(Dictionary<T, int> mapa, T codigo, string nombre) where T : notnull =>
        mapa.TryGetValue(codigo, out var id) ? id : throw new InvalidOperationException($"No existe en la caja {nombre} con código '{codigo}'.");
}
