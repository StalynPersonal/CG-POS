namespace CgPos.Dominio.Comun;

/// <summary>
/// Los catálogos (departamento, categoría, marca, unidad de medida, sucursal, caja…) se identifican entre el Central y las cajas por un código
/// numérico: el Central sugiere el siguiente, se puede cambiar al crear y después no cambia.
/// </summary>
public static class CodigosCatalogo
{
    public const int Maximo = 999_999;

    /// <summary>Sucursal y caja: dos dígitos cada una, porque forman el número de los documentos (sucursal + caja + tipo + secuencia).</summary>
    public const int MaximoSucursalCaja = 99;

    /// <summary>Los códigos de sucursal y de caja se guardan como texto de dos dígitos (01, 02… 99).</summary>
    public const int LargoSucursalCaja = 2;
}
