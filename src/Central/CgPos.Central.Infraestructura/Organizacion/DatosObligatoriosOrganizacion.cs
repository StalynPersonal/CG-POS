namespace CgPos.Central.Infraestructura.Organizacion;

/// <summary>
/// En el Central todos los datos de la empresa y de las sucursales son obligatorios: salen en los e-CF, los tickets y los reportes.
/// Se valida al guardar desde el Manager y al aplicar la carga inicial.
/// </summary>
internal static class DatosObligatoriosOrganizacion
{
    /// <returns>El mensaje con los campos que faltan, o <c>null</c> si están todos.</returns>
    public static string? Empresa(string? razonSocial, string? nombreComercial, string? direccion, string? telefono) =>
        Faltantes("la empresa", ("razón social", razonSocial), ("nombre comercial", nombreComercial), ("dirección", direccion), ("teléfono", telefono));

    /// <returns>El mensaje con los campos que faltan, o <c>null</c> si están todos.</returns>
    public static string? Sucursal(string? codigo, string? nombre, string? direccion, string? telefono) =>
        Faltantes("la sucursal", ("código", codigo), ("nombre", nombre), ("dirección", direccion), ("teléfono", telefono));

    private static string? Faltantes(string entidad, params (string Campo, string? Valor)[] campos)
    {
        var faltan = campos.Where(c => string.IsNullOrWhiteSpace(c.Valor)).Select(c => c.Campo).ToList();
        return faltan.Count == 0 ? null : $"Complete {(faltan.Count == 1 ? "el dato" : "los datos")} de {entidad}: {string.Join(", ", faltan)}.";
    }
}
