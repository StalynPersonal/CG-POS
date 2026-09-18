namespace CgPos.Pos.Web.Seguridad;

/// <summary>
/// Pantalla de trabajo de la caja. <paramref name="Clave"/> es como se pide en el ingreso (<c>/ingreso?pantalla=secundaria</c>)
/// y <paramref name="Ruta"/> la dirección de la pantalla; el ingreso muestra su nombre y vuelve a ella al entrar.
/// </summary>
public sealed record PantallaCaja(string Clave, string Ruta, string Nombre, string Icono);

public static class PantallasCaja
{
    /// <summary>Pantalla de ventas principal: la que se usa con lector y teclado.</summary>
    public static readonly PantallaCaja Principal = new("principal", string.Empty, "VENTAS", MudBlazor.Icons.Material.Filled.PointOfSale);

    /// <summary>Pantalla de ventas secundaria: táctil, con el catálogo en mosaicos.</summary>
    public static readonly PantallaCaja Secundaria = new("secundaria", "venta-secundaria", "VENTAS (TÁCTIL)", MudBlazor.Icons.Material.Filled.TouchApp);

    public static readonly PantallaCaja Devoluciones = new("devoluciones", "devoluciones", "DEVOLUCIONES", MudBlazor.Icons.Material.Filled.AssignmentReturn);
    public static readonly PantallaCaja Despacho = new("despacho", "despacho", "DESPACHO", MudBlazor.Icons.Material.Filled.LocalShipping);

    private static readonly PantallaCaja[] Todas = [Principal, Secundaria, Devoluciones, Despacho];

    /// <summary>Pantalla por la clave del ingreso; una clave desconocida o vacía es la de ventas principal.</summary>
    public static PantallaCaja PorClave(string? clave) =>
        Todas.FirstOrDefault(p => string.Equals(p.Clave, clave?.Trim(), StringComparison.OrdinalIgnoreCase)) ?? Principal;

    /// <summary>Pantalla por su dirección; una ruta desconocida es la de ventas principal (así no se redirige a cualquier sitio).</summary>
    public static PantallaCaja PorRuta(string? ruta)
    {
        var limpia = ruta?.Split('?', '#')[0].Trim('/') ?? string.Empty;
        return Todas.FirstOrDefault(p => string.Equals(p.Ruta, limpia, StringComparison.OrdinalIgnoreCase)) ?? Principal;
    }
}
