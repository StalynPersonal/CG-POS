using CgPos.Dominio.Catalogo;
using CgPos.Dominio.Fidelidad;
using CgPos.Dominio.Promociones;

namespace CgPos.Dominio.Pruebas.Catalogo;

/// <summary>Departamento, categoría y marca: la clasificación del artículo y las reglas que se aplican por cualquiera de los tres niveles.</summary>
public class ClasificacionPruebas
{
    private static readonly DateTimeOffset Martes10 = new(2026, 9, 15, 10, 0, 0, TimeSpan.FromHours(-4));
    private static readonly Guid Electrico = Guid.CreateVersion7();
    private static readonly Guid Cables = Guid.CreateVersion7();
    private static readonly Guid Truper = Guid.CreateVersion7();
    private static readonly Guid Articulo = Guid.CreateVersion7();

    [Fact]
    public void La_categoria_pertenece_a_un_departamento_y_la_marca_es_independiente()
    {
        var categoria = Categoria.Crear("CAB", "Cables", Electrico);
        Assert.Equal(Electrico, categoria.DepartamentoId);
        Assert.Throws<ArgumentException>(() => Categoria.Crear("CAB", "Cables", Guid.Empty));

        var marca = Marca.Crear("TRU", "Truper");
        marca.Desactivar();
        Assert.False(marca.Activa);

        // Categoría y marca son opcionales en el artículo.
        var articulo = Dominio.Catalogo.Articulo.Crear("CAB-12", "Cable 12 AWG", Electrico, Guid.CreateVersion7(), Guid.CreateVersion7());
        Assert.Equal((null, null), (articulo.CategoriaId, articulo.MarcaId));
        articulo.Clasificar(categoria.Id, marca.Id);
        Assert.Equal((categoria.Id, marca.Id), (articulo.CategoriaId, articulo.MarcaId));
        articulo.Clasificar(Guid.Empty, null);
        Assert.Equal((null, null), (articulo.CategoriaId, articulo.MarcaId));
    }

    [Fact]
    public void La_oferta_alcanza_al_articulo_por_su_departamento_su_categoria_o_su_marca()
    {
        Promocion Oferta(IEnumerable<Guid>? departamentos = null, IEnumerable<Guid>? categorias = null, IEnumerable<Guid>? marcas = null)
        {
            var promocion = Promocion.Crear("OF", "Oferta", TipoPromocion.Porcentaje, 10m, Martes10.AddDays(-1), Martes10.AddDays(1));
            promocion.AsignarAlcance(null, departamentos, null, categorias, marcas);
            return promocion;
        }

        Assert.True(Oferta(departamentos: [Electrico]).AplicaA(Articulo, Electrico, Cables, Truper));
        Assert.True(Oferta(categorias: [Cables]).AplicaA(Articulo, Electrico, Cables, Truper));
        Assert.True(Oferta(marcas: [Truper]).AplicaA(Articulo, Electrico, Cables, Truper));

        // Sin categoría ni marca en el artículo, una oferta por categoría o marca no lo alcanza.
        Assert.False(Oferta(categorias: [Cables]).AplicaA(Articulo, Electrico));
        Assert.False(Oferta(marcas: [Truper]).AplicaA(Articulo, Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7()));
    }

    [Fact]
    public void El_tope_mas_especifico_gana_articulo_categoria_marca_departamento_general()
    {
        var topes = new[]
        {
            TopeDescuento.Crear(2, 20m, null),
            TopeDescuento.Crear(2, 15m, null, departamentoId: Electrico),
            TopeDescuento.Crear(2, 10m, null, marcaId: Truper),
            TopeDescuento.Crear(2, 5m, null, categoriaId: Cables),
        };

        decimal? Maximo(Guid? categoria, Guid? marca) =>
            ReglasTopeDescuento.Evaluar(topes, 2, Articulo, Electrico, 1m, 1m, categoria, marca).PorcentajeMaximo;

        Assert.Equal(5m, Maximo(Cables, Truper));
        Assert.Equal(10m, Maximo(null, Truper));
        Assert.Equal(15m, Maximo(null, null));
        Assert.Equal(20m, ReglasTopeDescuento.Evaluar(topes, 2, Articulo, Guid.CreateVersion7(), 1m, 1m).PorcentajeMaximo);

        Assert.Throws<ArgumentException>(() => TopeDescuento.Crear(2, 5m, null, departamentoId: Electrico, marcaId: Truper));
    }

    [Fact]
    public void Los_puntos_pueden_premiar_una_categoria_o_una_marca()
    {
        var reglas = new[]
        {
            ReglaAcumulacion.Crear("GEN", "General", TipoReglaAcumulacion.Monto, 100m, 1m, null, null, null, null),
            ReglaAcumulacion.Crear("TRU", "Truper doble", TipoReglaAcumulacion.Marca, 100m, 2m, Truper, null, null, null),
            ReglaAcumulacion.Crear("CAB", "Cables triple", TipoReglaAcumulacion.Categoria, 100m, 3m, Cables, null, null, null),
        };

        Assert.Equal(6, ReglasFidelidad.CalcularPuntos([new LineaPuntuable(Articulo, Electrico, null, 200m, Cables, Truper)], reglas, 1m, 1m, Martes10));
        Assert.Equal(4, ReglasFidelidad.CalcularPuntos([new LineaPuntuable(Articulo, Electrico, null, 200m, null, Truper)], reglas, 1m, 1m, Martes10));
        Assert.Equal(2, ReglasFidelidad.CalcularPuntos([new LineaPuntuable(Articulo, Electrico, null, 200m)], reglas, 1m, 1m, Martes10));
        Assert.Throws<ArgumentException>(() => ReglaAcumulacion.Crear("X", "Sin marca", TipoReglaAcumulacion.Marca, 100m, 1m, null, null, null, null));
    }
}
