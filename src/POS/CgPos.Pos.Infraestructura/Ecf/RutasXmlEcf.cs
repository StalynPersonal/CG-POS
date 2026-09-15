namespace CgPos.Pos.Infraestructura.Ecf;

/// <summary>Carpetas de los XML de e-CF bajo la carpeta base: Pendientes, Enviados y Errores, organizadas por fecha (RF-219, RNF-15).</summary>
internal static class RutasXmlEcf
{
    public const string Pendientes = "Pendientes";
    public const string Enviados = "Enviados";

    /// <summary>La misma ruta relativa del XML, en la carpeta Enviados; nulo si el XML no está en la carpeta Pendientes de la base.</summary>
    public static string? RutaEnviados(string rutaPendiente, string carpetaBase)
    {
        var pendientes = Path.GetFullPath(Path.Combine(carpetaBase, Pendientes));
        var completa = Path.GetFullPath(rutaPendiente);
        if (!completa.StartsWith(pendientes + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            return null;

        return Path.Combine(Path.GetFullPath(Path.Combine(carpetaBase, Enviados)), Path.GetRelativePath(pendientes, completa));
    }
}
