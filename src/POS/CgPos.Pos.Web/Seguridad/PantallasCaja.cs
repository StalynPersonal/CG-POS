namespace CgPos.Pos.Web.Seguridad;

/// <summary>Pantalla de trabajo de la caja: ruta, nombre visible e ícono. El ingreso la muestra y vuelve a ella al entrar.</summary>
public sealed record PantallaCaja(string Ruta, string Nombre, string Icono);

public static class PantallasCaja
{
    public static readonly PantallaCaja Caja = new(string.Empty, "CAJA", MudBlazor.Icons.Material.Filled.PointOfSale);
    public static readonly PantallaCaja Devoluciones = new("devoluciones", "DEVOLUCIONES", MudBlazor.Icons.Material.Filled.AssignmentReturn);
    public static readonly PantallaCaja Despacho = new("despacho", "DESPACHO", MudBlazor.Icons.Material.Filled.LocalShipping);

    private static readonly PantallaCaja[] Todas = [Caja, Devoluciones, Despacho];

    /// <summary>Pantalla por su ruta; una ruta desconocida o vacía es la caja (evita redirigir a sitios arbitrarios).</summary>
    public static PantallaCaja PorRuta(string? ruta)
    {
        var limpia = ruta?.Split('?', '#')[0].Trim('/') ?? string.Empty;
        return Todas.FirstOrDefault(p => string.Equals(p.Ruta, limpia, StringComparison.OrdinalIgnoreCase)) ?? Caja;
    }
}
