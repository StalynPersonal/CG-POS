using CgPos.Dominio.Entregas;

namespace CgPos.Contratos.Ventas;

public sealed record DatosLineaDestinoEntrega(int NumeroLinea, decimal Cantidad);

/// <summary>Destino de entrega marcado en la venta en curso (RF-246 a RF-248).</summary>
public sealed record DatosDestinoEntrega(
    int Numero,
    MetodoEntrega Metodo,
    Guid? AlmacenId,
    string? AlmacenNombre,
    string? Direccion,
    string? Sector,
    string? Ciudad,
    string? Referencia,
    string? Telefono,
    string? Transportista,
    decimal? CostoEnvio,
    DateOnly? FechaComprometida,
    string? Comentario,
    string? AutorizadoPorNombre,
    IReadOnlyList<DatosLineaDestinoEntrega> Lineas);

/// <param name="AlmacenId">Obligatorio para retiro en almacén o sucursal.</param>
/// <param name="Envio">Obligatorio para envío a dirección (RF-250).</param>
/// <param name="AutorizacionId">Marcar mercancía como pendiente requiere autorización de supervisor (RF-53, RN-15).</param>
public sealed record SolicitudMarcarEntrega(
    MetodoEntrega Metodo,
    Guid? AlmacenId,
    DatosEnvio? Envio,
    DateOnly? FechaComprometida,
    string? Comentario,
    IReadOnlyList<CantidadEntrega>? Lineas,
    Guid? AutorizacionId = null);

/// <param name="EsDeLaSucursal">Almacén de la sucursal de la caja: se propone por defecto (RF-138).</param>
public sealed record DatosAlmacen(Guid Id, string Codigo, string Nombre, Guid SucursalId, string? Direccion, bool EsDeLaSucursal);

public sealed record DatosLineaPendiente(
    int NumeroLineaVenta,
    string Codigo,
    string Descripcion,
    string UnidadMedidaCodigo,
    int DecimalesCantidad,
    bool Serializado,
    decimal Cantidad,
    decimal CantidadEntregada,
    string? Serial);

public sealed record DatosLineaEntregaPendiente(int NumeroLineaVenta, string Descripcion, decimal Cantidad, string? Serial);

/// <summary>Entrega registrada de un pendiente con quien recibió (RF-254).</summary>
public sealed record DatosEntregaPendiente(int Numero, string RecibeNombre, string RecibeCedula, string UsuarioNombre, DateTimeOffset Fecha,
    IReadOnlyList<DatosLineaEntregaPendiente> Lineas);

/// <summary>Documento de pendiente de entrega o envío (RF-249, RF-252); también es el mensaje para el Central.</summary>
public sealed record DatosPendienteEntrega(
    Guid Id,
    string Numero,
    Guid VentaId,
    string VentaNumero,
    Guid SucursalId,
    Guid CajaId,
    MetodoEntrega Metodo,
    EstadoPendiente Estado,
    Guid? AlmacenId,
    string? AlmacenNombre,
    string? Direccion,
    string? Sector,
    string? Ciudad,
    string? Referencia,
    string? Telefono,
    string? Transportista,
    decimal? CostoEnvio,
    DateOnly? FechaComprometida,
    string? Comentario,
    string? ClienteDocumento,
    string? ClienteNombre,
    string VendidoPorNombre,
    string? AutorizadoPorNombre,
    DateTimeOffset CreadoEn,
    DateTimeOffset ActualizadoEn,
    string ActualizadoPorNombre,
    string? MotivoAnulacion,
    IReadOnlyList<DatosLineaPendiente> Lineas,
    IReadOnlyList<DatosEntregaPendiente> Entregas);
