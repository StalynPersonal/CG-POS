namespace CgPos.Contratos.Central;

/// <summary>Oferta vigente del artículo, tal como se le muestra al cliente en el chequeador.</summary>
public sealed record DatosOfertaChequeador(string Codigo, string Nombre, string Descripcion, DateOnly VigenteHasta);

/// <summary>
/// Lo que el chequeador de precios le muestra al cliente: qué es el artículo y cuánto cuesta. No lleva costo, existencia ni nada
/// interno del negocio.
/// </summary>
/// <param name="PrecioMayor">Precio por cantidad, con la cantidad mínima que lo activa; nulo si el artículo no tiene.</param>
public sealed record DatosPrecioChequeador(
    string Codigo,
    string Descripcion,
    string UnidadMedida,
    decimal Precio,
    decimal? PrecioMayor,
    decimal? CantidadMinimaMayor,
    string? RutaImagen,
    IReadOnlyList<DatosOfertaChequeador> Ofertas);

/// <param name="Habilitado">El Central tiene encendido el chequeador (`Central.Chequeador.Habilitado`).</param>
public sealed record DatosConfiguracionChequeador(bool Habilitado, IReadOnlyList<DatosSucursalChequeador> Sucursales);

public sealed record DatosSucursalChequeador(int Id, string Nombre);
