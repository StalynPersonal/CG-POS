using CgPos.Central.Web.Seguridad;
using CgPos.Contratos.Catalogo;
using CgPos.Dominio.Catalogo;

namespace CgPos.Central.Web.Maestros;

/// <summary>Departamentos, categorías, marcas, unidades e impuestos publicados, para elegir en un artículo.</summary>
public sealed record ReferenciasArticulo(IReadOnlyList<DepartamentoCarga> Departamentos, IReadOnlyList<UnidadMedidaCarga> Unidades, IReadOnlyList<ImpuestoCarga> Impuestos,
    IReadOnlyList<CategoriaCarga> Categorias, IReadOnlyList<MarcaCarga> Marcas)
{
    public static ReferenciasArticulo Vacias { get; } = new([], [], [], [], []);

    public static async Task<ReferenciasArticulo> CargarAsync(ClienteCentral cliente)
    {
        var departamentos = await cliente.ListarMaestroAsync<DepartamentoCarga>("departamentos");
        var unidades = await cliente.ListarMaestroAsync<UnidadMedidaCarga>("unidades-medida");
        var impuestos = await cliente.ListarMaestroAsync<ImpuestoCarga>("impuestos");
        var categorias = await cliente.ListarMaestroAsync<CategoriaCarga>("categorias");
        var marcas = await cliente.ListarMaestroAsync<MarcaCarga>("marcas");
        return new(
            (departamentos ?? []).Select(f => f.Dato).OrderBy(f => f.Nombre, StringComparer.CurrentCulture).ToList(),
            (unidades ?? []).Select(u => u.Dato).OrderBy(u => u.Nombre, StringComparer.CurrentCulture).ToList(),
            (impuestos ?? []).Select(i => i.Dato).OrderBy(i => i.Porcentaje).ToList(),
            (categorias ?? []).Select(c => c.Dato).OrderBy(c => c.Nombre, StringComparer.CurrentCulture).ToList(),
            (marcas ?? []).Select(m => m.Dato).OrderBy(m => m.Nombre, StringComparer.CurrentCulture).ToList());
    }
}

public static class EtiquetasArticulo
{
    public static IReadOnlyList<TipoArticulo> Tipos { get; } = Enum.GetValues<TipoArticulo>();

    public static string Tipo(TipoArticulo tipo) => tipo switch
    {
        TipoArticulo.Pesado => "Pesado",
        TipoArticulo.Serializado => "Serializado",
        TipoArticulo.ComboKit => "Combo o kit",
        _ => "Normal",
    };
}
