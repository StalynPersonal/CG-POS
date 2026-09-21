using CgPos.Dominio.Entregas;

namespace CgPos.Contratos.Ventas;

public sealed record DatosLineaDestinoEntrega(int NumeroLinea, decimal Cantidad);

/// <summary>Destino de entrega marcado en la venta en curso (RF-246 a RF-248).</summary>
public sealed record DatosDestinoEntrega(
    int Numero,
    MetodoEntrega Metodo,
    int? SucursalRetiroId,
    string? SucursalRetiroNombre,
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

/// <param name="SucursalRetiroId">Obligatorio para retiro: la sucursal donde el cliente pasa a buscar.</param>
/// <param name="Envio">Obligatorio para envío a dirección (RF-250).</param>
/// <param name="AutorizacionId">Marcar mercancía como pendiente requiere autorización de supervisor (RF-53, RN-15).</param>
public sealed record SolicitudMarcarEntrega(
    MetodoEntrega Metodo,
    int? SucursalRetiroId,
    DatosEnvio? Envio,
    DateOnly? FechaComprometida,
    string? Comentario,
    IReadOnlyList<CantidadEntrega>? Lineas,
    Guid? AutorizacionId = null);

/// <summary>Sucursal donde se puede dejar mercancía para que el cliente la retire (RF-138, RF-140).</summary>
/// <param name="EsDeLaCaja">La sucursal de esta caja: se propone por defecto.</param>
public sealed record DatosSucursalRetiro(int Id, string Codigo, string Nombre, string? Direccion, bool EsDeLaCaja);

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

/// <summary>Avanza la preparación del pendiente: en preparación, preparado o despachado (RF-252).</summary>
public sealed record SolicitudEstadoPendiente(EstadoPendiente Estado);

/// <summary>Entrega total o parcial con quien recibe (RF-253, RF-254); los serializados llevan su serial (RN-16).</summary>
public sealed record SolicitudEntregaPendiente(IReadOnlyList<CantidadEntregada>? Lineas, string? RecibeNombre, string? RecibeCedula);

/// <param name="AutorizacionId">Anular requiere permiso o clave de supervisor (RF-255).</param>
public sealed record SolicitudAnularPendiente(string? Motivo, Guid? AutorizacionId = null);

public enum CodigoResultadoPendiente
{
    Correcto,
    NoEncontrado,
    SinPermiso,
    RequiereAutorizacion,
    AutorizacionInvalida,
    OperacionInvalida,
}

public sealed record RespuestaPendiente(CodigoResultadoPendiente Resultado, string? Mensaje, DatosPendienteEntrega? Pendiente, string? PermisoRequerido = null)
{
    public bool Exitosa => Resultado == CodigoResultadoPendiente.Correcto;
}

/// <summary>Pendientes que corresponden a un voucher o a una factura escaneada (RF-251).</summary>
public sealed record RespuestaBusquedaPendientes(CodigoResultadoPendiente Resultado, string? Mensaje, IReadOnlyList<DatosPendienteEntrega> Pendientes)
{
    public bool Exitosa => Resultado == CodigoResultadoPendiente.Correcto;
}

/// <summary>Documento de pendiente de entrega o envío (RF-249, RF-252) en la caja.</summary>
public sealed record DatosPendienteEntrega(
    int Id,
    string Numero,
    int VentaId,
    string VentaNumero,
    int SucursalId,
    int CajaId,
    MetodoEntrega Metodo,
    EstadoPendiente Estado,
    int? SucursalRetiroId,
    string? SucursalRetiroNombre,
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
