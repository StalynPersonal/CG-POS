using CgPos.Contratos.Catalogo;
using CgPos.Dominio.Catalogo;
using CgPos.Dominio.Fiscal;
using CgPos.Dominio.Turnos;
using CgPos.Dominio.Ventas;

namespace CgPos.Contratos.Ventas;

public sealed record DatosTurno(
    Guid Id,
    long Numero,
    DateOnly FechaOperacion,
    Guid CajaId,
    Guid UsuarioActualId,
    string UsuarioActualNombre,
    decimal FondoInicial,
    EstadoTurno Estado,
    DateTimeOffset AbiertoEn);

/// <summary>Estado del turno de la caja para la sesión actual, con el fondo sugerido para abrir.</summary>
/// <param name="EsDeOtroUsuario">La caja tiene un turno abierto por otro usuario (requiere relevo, RF-260).</param>
public sealed record DatosEstadoTurno(DatosTurno? TurnoAbierto, decimal FondoSugerido, bool PuedeAbrir, bool EsDeOtroUsuario);

public sealed record SolicitudAbrirTurno(decimal? FondoInicial);

public enum CodigoResultadoTurno
{
    Correcto,
    SinPermiso,
    YaExisteTurnoAbierto,
    FondoInvalido,
    CajaNoOperativa,
}

public sealed record RespuestaTurno(CodigoResultadoTurno Resultado, string? Mensaje, DatosTurno? Turno)
{
    public bool Exitosa => Resultado == CodigoResultadoTurno.Correcto;
}

public sealed record DatosLineaVenta(
    int NumeroLinea,
    Guid ArticuloId,
    string CodigoInterno,
    string CodigoLeido,
    string Descripcion,
    TipoArticulo TipoArticulo,
    string UnidadMedidaCodigo,
    int DecimalesCantidad,
    decimal Cantidad,
    decimal PrecioUnitario,
    decimal Importe,
    decimal PorcentajeImpuesto,
    ListaPrecio Lista,
    MotivoPrecio MotivoPrecio,
    bool LeidaDeBalanza,
    bool EsReverso,
    int? LineaAnuladaNumero,
    bool Anulada,
    string? Serial = null,
    string? PromocionCodigo = null,
    string? PromocionNombre = null,
    string? PromocionDescripcion = null,
    decimal DescuentoPromocion = 0m,
    bool PromocionDesactivada = false,
    decimal DescuentoManual = 0m,
    TipoDescuento? DescuentoManualTipo = null,
    decimal? DescuentoManualValor = null,
    string? MotivoDescuento = null,
    string? DescuentoAutorizadoPorNombre = null,
    decimal DescuentoFactura = 0m,
    decimal ImporteBruto = 0m,
    bool PermiteDescuentoManual = true);

public sealed record DatosDesgloseImpuesto(decimal Porcentaje, int IndicadorFacturacion, decimal Base, decimal Impuesto, decimal Total);

public sealed record DatosTotalesVenta(
    decimal Subtotal,
    decimal Impuesto,
    decimal Total,
    int CantidadLineas,
    decimal CantidadArticulos,
    IReadOnlyList<DatosDesgloseImpuesto> Desglose,
    decimal Descuento = 0m);

/// <param name="Lineas">Líneas a las que se limitó; nulo = todas.</param>
/// <param name="Monto">Suma prorrateada que se aplicó en las líneas.</param>
public sealed record DatosDescuentoFactura(TipoDescuento Tipo, decimal Valor, IReadOnlyList<int>? Lineas, string? Motivo, string? AutorizadoPorNombre, decimal Monto);

public sealed record DatosClienteVenta(Guid? ClienteId, TipoDocumentoIdentidad? TipoDocumento, string? Documento, string Nombre);

/// <param name="Lineas">En orden de pantalla: cada reverso aparece justo debajo de la línea que anula.</param>
/// <param name="RequiereIdentificacion">Factura de consumo desde <paramref name="MontoIdentificacion"/> sin cédula o RNC (RF-26).</param>
public sealed record DatosVenta(
    Guid Id,
    string NumeroTransaccion,
    EstadoVenta Estado,
    Guid TurnoId,
    string UsuarioNombre,
    DateTimeOffset IniciadaEn,
    IReadOnlyList<DatosLineaVenta> Lineas,
    DatosTotalesVenta Totales,
    TipoComprobante TipoComprobante,
    DatosClienteVenta? Cliente,
    decimal? LimiteCompra,
    bool LimiteCompraExcedido,
    bool RequiereIdentificacion,
    decimal MontoIdentificacion,
    DatosDescuentoFactura? DescuentoFactura = null);

/// <summary>Resumen de una factura en espera del cajero en su turno (RF-22, RF-197).</summary>
public sealed record DatosVentaEnEspera(
    Guid Id,
    string NumeroTransaccion,
    string? ClienteNombre,
    decimal Total,
    int CantidadLineas,
    DateTimeOffset PuestaEnEsperaEn);

/// <param name="Codigo">Código leído; admite "cantidad*código" (ej. "12*7891114119695", RF-14).</param>
/// <param name="Serial">Serial escaneado, obligatorio para artículos serializados (RF-17).</param>
public sealed record SolicitudAgregarArticulo(string Codigo, decimal? Cantidad = null, string? Serial = null);

/// <summary>Agrega un artículo pesado con el peso que reporta la balanza, descontando su tara (RF-19, RF-196).</summary>
public sealed record SolicitudPesarArticulo(string Codigo);

public sealed record SolicitudCambiarCantidad(decimal Cantidad);

/// <param name="AutorizacionId">Autorización de supervisor obtenida en <c>/api/autorizaciones</c> cuando el usuario no tiene el permiso.</param>
public sealed record SolicitudConAutorizacion(Guid? AutorizacionId = null);

public sealed record SolicitudEliminarPorCodigo(string Codigo, Guid? AutorizacionId = null);

/// <param name="Nombre">Solo si el documento no está en el padrón DGII ni registrado como cliente.</param>
public sealed record SolicitudAsignarCliente(string Documento, string? Nombre = null);

public sealed record SolicitudCambiarComprobante(TipoComprobante TipoComprobante, Guid? AutorizacionId = null);

/// <param name="Limite">Nulo para quitar el límite.</param>
public sealed record SolicitudLimiteCompra(decimal? Limite);

/// <param name="Motivo">Si se omite y hubo autorización de supervisor, se usa el motivo de esa autorización.</param>
public sealed record SolicitudAnularVenta(string? Motivo, Guid? AutorizacionId = null);

/// <param name="Motivo">Motivo de la lista configurable (RF-203).</param>
public sealed record SolicitudDescuentoLinea(TipoDescuento Tipo, decimal Valor, string? Motivo, Guid? AutorizacionId = null);

/// <param name="Lineas">Números de línea a los que se limita; vacío o nulo = todas (RF-201).</param>
public sealed record SolicitudDescuentoFactura(TipoDescuento Tipo, decimal Valor, IReadOnlyList<int>? Lineas, string? Motivo, Guid? AutorizacionId = null);

public enum CodigoResultadoVenta
{
    Correcto,
    TurnoNoAbierto,
    TurnoDeOtroUsuario,
    ArticuloNoEncontrado,
    SinPrecio,
    RequiereBalanza,
    CantidadInvalida,
    LineaNoEncontrada,
    VentaNoEditable,
    RequiereAutorizacion,
    AutorizacionInvalida,
    MotivoRequerido,
    DocumentoInvalido,
    NombreRequerido,
    ComprobanteNoPermitido,
    DocumentoRequerido,
    SinLineas,
    RequiereSerial,
    SerialDuplicado,
    BalanzaSinLectura,
    ArticuloEnOferta,
    DescuentoNoPermitido,
    DescuentoInvalido,
    TopeDescuentoExcedido,
}

/// <summary>Resultado de una operación sobre la venta: la venta actualizada o el motivo del rechazo.</summary>
/// <param name="Mensaje">En operaciones correctas puede traer un aviso (ej. líneas excluidas de un descuento).</param>
/// <param name="LineasExcluidas">Líneas que no tomaron el descuento a la factura (RF-204).</param>
public sealed record RespuestaVenta(
    CodigoResultadoVenta Resultado,
    string? Mensaje,
    DatosVenta? Venta,
    string? PermisoRequerido = null,
    IReadOnlyList<int>? LineasExcluidas = null)
{
    public bool Exitosa => Resultado == CodigoResultadoVenta.Correcto;
}

/// <summary>Indicador permanente de conexión con el Central y documentos pendientes (RF-192).</summary>
public sealed record DatosEstadoSincronizacion(
    bool CentralConfigurado,
    bool EnLinea,
    int DocumentosPendientes,
    DateTimeOffset? UltimaSincronizacion);
