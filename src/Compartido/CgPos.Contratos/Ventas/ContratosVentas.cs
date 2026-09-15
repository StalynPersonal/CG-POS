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
    string? Serial = null);

public sealed record DatosDesgloseImpuesto(decimal Porcentaje, int IndicadorFacturacion, decimal Base, decimal Impuesto, decimal Total);

public sealed record DatosTotalesVenta(
    decimal Subtotal,
    decimal Impuesto,
    decimal Total,
    int CantidadLineas,
    decimal CantidadArticulos,
    IReadOnlyList<DatosDesgloseImpuesto> Desglose);

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
    decimal MontoIdentificacion);

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
}

/// <summary>Resultado de una operación sobre la venta: la venta actualizada o el motivo del rechazo.</summary>
public sealed record RespuestaVenta(
    CodigoResultadoVenta Resultado,
    string? Mensaje,
    DatosVenta? Venta,
    string? PermisoRequerido = null)
{
    public bool Exitosa => Resultado == CodigoResultadoVenta.Correcto;
}

/// <summary>Indicador permanente de conexión con el Central y documentos pendientes (RF-192).</summary>
public sealed record DatosEstadoSincronizacion(
    bool CentralConfigurado,
    bool EnLinea,
    int DocumentosPendientes,
    DateTimeOffset? UltimaSincronizacion);
