using CgPos.Central.Web.Seguridad;
using CgPos.Contratos.Catalogo;
using CgPos.Dominio.Catalogo;

namespace CgPos.Central.Web.Maestros;

/// <summary>Familias, unidades e impuestos publicados, para elegir en un artículo.</summary>
public sealed record ReferenciasArticulo(IReadOnlyList<FamiliaCarga> Familias, IReadOnlyList<UnidadMedidaCarga> Unidades, IReadOnlyList<ImpuestoCarga> Impuestos)
{
    public static ReferenciasArticulo Vacias { get; } = new([], [], []);

    public static async Task<ReferenciasArticulo> CargarAsync(ClienteCentral cliente)
    {
        var familias = await cliente.ListarMaestroAsync<FamiliaCarga>("familias");
        var unidades = await cliente.ListarMaestroAsync<UnidadMedidaCarga>("unidades-medida");
        var impuestos = await cliente.ListarMaestroAsync<ImpuestoCarga>("impuestos");
        return new(
            (familias ?? []).Select(f => f.Dato).OrderBy(f => f.Nombre, StringComparer.CurrentCulture).ToList(),
            (unidades ?? []).Select(u => u.Dato).OrderBy(u => u.Nombre, StringComparer.CurrentCulture).ToList(),
            (impuestos ?? []).Select(i => i.Dato).OrderBy(i => i.Porcentaje).ToList());
    }
}

public static class EtiquetasArticulo
{
    public static IReadOnlyList<TipoArticulo> Tipos { get; } = Enum.GetValues<TipoArticulo>();

    public static string Tipo(TipoArticulo tipo) => tipo switch
    {
        TipoArticulo.Pesado => "Pesado (balanza)",
        TipoArticulo.Serializado => "Serializado",
        TipoArticulo.ComboKit => "Combo o kit",
        _ => "Normal",
    };
}
