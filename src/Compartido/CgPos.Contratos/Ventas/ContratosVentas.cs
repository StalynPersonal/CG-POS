using CgPos.Contratos.Catalogo;
using CgPos.Contratos.Central;
using CgPos.Dominio.Catalogo;
using CgPos.Dominio.Fiscal;
using CgPos.Dominio.Turnos;
using CgPos.Dominio.Ventas;

namespace CgPos.Contratos.Ventas;

public sealed record DatosTurno(
    int Id,
    long Numero,
    DateOnly FechaOperacion,
    int CajaId,
    int UsuarioActualId,
    string UsuarioActualNombre,
    decimal FondoInicial,
    EstadoTurno Estado,
    DateTimeOffset AbiertoEn);

/// <summary>Estado del turno de la caja para la sesión actual, con el fondo sugerido para abrir.</summary>
/// <param name="FondoSugerido">Nulo si el negocio no configuró un fondo sugerido.</param>
/// <param name="EsDeOtroUsuario">La caja tiene un turno abierto por otro usuario (requiere relevo, RF-260).</param>
public sealed record DatosEstadoTurno(DatosTurno? TurnoAbierto, decimal? FondoSugerido, bool PuedeAbrir, bool EsDeOtroUsuario);

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
    int ArticuloId,
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
    bool PermiteDescuentoManual = true,
    bool SerialPendiente = false,
    decimal CantidadEnEntrega = 0m);

public sealed record DatosDesgloseImpuesto(decimal Porcentaje, int IndicadorFacturacion, decimal Base, decimal Impuesto, decimal Total);

/// <param name="Retencion">Retención de la Ley 32-23 en facturas de régimen especial (E44): no la paga el cliente en caja.</param>
public sealed record DatosTotalesVenta(
    decimal Subtotal,
    decimal Impuesto,
    decimal Total,
    int CantidadLineas,
    decimal CantidadArticulos,
    IReadOnlyList<DatosDesgloseImpuesto> Desglose,
    decimal Descuento = 0m,
    decimal Retencion = 0m)
{
    /// <summary>Lo que el cliente paga en caja: el total de la factura menos la retención.</summary>
    public decimal TotalAPagar => Total - Retencion;
}

/// <param name="Lineas">Líneas a las que se limitó; nulo = todas.</param>
/// <param name="Monto">Suma prorrateada que se aplicó en las líneas.</param>
public sealed record DatosDescuentoFactura(TipoDescuento Tipo, decimal Valor, IReadOnlyList<int>? Lineas, string? Motivo, string? AutorizadoPorNombre, decimal Monto);

public sealed record DatosClienteVenta(int? ClienteId, TipoDocumentoIdentidad? TipoDocumento, string? Documento, string Nombre);

/// <param name="Lineas">En orden de pantalla: cada reverso aparece justo debajo de la línea que anula.</param>
/// <param name="RequiereIdentificacion">Factura de consumo desde <paramref name="MontoIdentificacion"/> sin cédula o RNC (RF-26).</param>
public sealed record DatosVenta(
    int Id,
    string NumeroTransaccion,
    EstadoVenta Estado,
    int TurnoId,
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
    string Moneda,
    string SimboloMoneda,
    DatosDescuentoFactura? DescuentoFactura = null,
    IReadOnlyList<DatosPagoVenta>? Pagos = null,
    decimal? TotalCobrado = null,
    decimal Devuelta = 0m,
    decimal RedondeoEfectivo = 0m,
    DateTimeOffset? CobradaEn = null,
    DatosComprobanteElectronico? Comprobante = null,
    DatosFidelidadVenta? Fidelidad = null,
    IReadOnlyList<DatosDestinoEntrega>? DestinosEntrega = null,
    DatosListaBodaVenta? ListaBoda = null,
    /// <summary>La factura va sin ITBIS: régimen especial (E44), o gubernamental con certificación de exención.</summary>
    bool ExentaDeImpuesto = false,
    /// <summary>Certificación de exención que presentó la entidad del Estado, si la hay.</summary>
    string? CertificacionExencion = null,
    /// <summary>Cotización del Central que se está facturando en esta venta.</summary>
    string? CotizacionNumero = null);

/// <summary>Lista de boda del Central contra la que se está comprando (RF-73).</summary>
public sealed record DatosListaBodaVenta(string Numero, string Evento);

/// <summary>Número de la lista de boda; vacío o nulo la quita de la venta.</summary>
public sealed record SolicitudListaBodaVenta(string? Numero);

/// <param name="Lista">La lista con lo pedido y lo que falta por comprar, para mostrársela al cliente.</param>
public sealed record RespuestaListaBoda(CodigoResultadoVenta Resultado, string? Mensaje, DatosVenta? Venta, DatosListaBodaParaCaja? Lista)
{
    public bool Exitosa => Resultado == CodigoResultadoVenta.Correcto;
}

/// <summary>Miembro del programa de fidelidad de la venta y los puntos que acumuló y canjeó al cobrar.</summary>
public sealed record DatosFidelidadVenta(int MiembroId, string Cedula, string Nombre, string? Nivel, int PuntosAcumulados, int PuntosCanjeados);

/// <summary>Referencia con que el cajero nombra la factura que deja en espera («Sra. María», «102»).</summary>
public sealed record SolicitudPonerEnEspera(string Referencia);

/// <param name="ReferenciaActual">Con qué nombre se guarda la venta que está en pantalla, si tiene artículos.</param>
public sealed record SolicitudRetomarVenta(string? ReferenciaActual = null);

/// <summary>Cédula del miembro del programa de fidelidad (ID/PIN, RF-236).</summary>
public sealed record SolicitudAsignarFidelidad(string Cedula);

/// <summary>Resumen de una factura en espera del cajero en su turno (RF-22, RF-197).</summary>
/// <param name="Referencia">Cómo la nombró el cajero al guardarla: todavía no tiene número de factura.</param>
public sealed record DatosVentaEnEspera(
    int Id,
    string Referencia,
    string? ClienteNombre,
    decimal Total,
    int CantidadLineas,
    DateTimeOffset PuestaEnEsperaEn);

/// <param name="Codigo">Código leído; admite "cantidad*código" (ej. "12*7891114119695", RF-14).</param>
/// <param name="Serial">Serial escaneado, obligatorio para artículos serializados (RF-17).</param>
/// <param name="SerialEnDespacho">Serializado sin serial que se marcará para entrega o envío: el serial se captura en el despacho (RN-16).</param>
public sealed record SolicitudAgregarArticulo(string Codigo, decimal? Cantidad = null, string? Serial = null, bool SerialEnDespacho = false);

/// <summary>Agrega un artículo pesado con el peso que reporta la balanza, descontando el de su empaque (RF-19, RF-196).</summary>
public sealed record SolicitudPesarArticulo(string Codigo);

public sealed record SolicitudCambiarCantidad(decimal Cantidad);

/// <param name="AutorizacionId">Autorización de supervisor obtenida en <c>/api/autorizaciones</c> cuando el usuario no tiene el permiso.</param>
public sealed record SolicitudConAutorizacion(Guid? AutorizacionId = null);

public sealed record SolicitudEliminarPorCodigo(string Codigo, Guid? AutorizacionId = null);

/// <param name="Nombre">Solo si el documento no corresponde a un cliente registrado.</param>
public sealed record SolicitudAsignarCliente(string Documento, string? Nombre = null);

public sealed record SolicitudCambiarComprobante(TipoComprobante TipoComprobante, Guid? AutorizacionId = null);

/// <summary>Certificación de exención de ITBIS de una entidad del Estado (E45); vacía la quita y la factura vuelve a llevar ITBIS.</summary>
public sealed record SolicitudCertificacionExencion(string? Certificacion);

/// <param name="Numero">Número de la cotización del Central (COT000123), escaneado o digitado.</param>
public sealed record SolicitudFacturarCotizacion(string? Numero, Guid? AutorizacionId = null);

/// <param name="Cotizacion">La cotización tal como la devolvió el Central, para mostrarla en pantalla.</param>
public sealed record RespuestaCotizacion(CodigoResultadoVenta Resultado, string? Mensaje, DatosVenta? Venta, DatosCotizacionParaCaja? Cotizacion,
    string? PermisoRequerido = null)
{
    public bool Exitosa => Resultado == CodigoResultadoVenta.Correcto;
}

/// <param name="Limite">Nulo para quitar el límite.</param>
public sealed record SolicitudLimiteCompra(decimal? Limite);

/// <param name="Motivo">Si se omite y hubo autorización de supervisor, se usa el motivo de esa autorización.</param>
public sealed record SolicitudAnularVenta(string? Motivo, Guid? AutorizacionId = null);

/// <param name="Motivo">Motivo de la lista configurable (RF-203).</param>
public sealed record SolicitudDescuentoLinea(TipoDescuento Tipo, decimal Valor, string? Motivo, Guid? AutorizacionId = null);

/// <param name="Lineas">Números de línea a los que se limita; vacío o nulo = todas (RF-201).</param>
public sealed record SolicitudDescuentoFactura(TipoDescuento Tipo, decimal Valor, IReadOnlyList<int>? Lineas, string? Motivo, Guid? AutorizacionId = null);

/// <summary>Primeros dígitos de la tarjeta (BIN) para aplicar el descuento del banco (RF-98).</summary>
public sealed record SolicitudDescuentoTarjeta(string Bin);

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
    PagoInvalido,
    PagoInsuficiente,
    DevueltaNoPermitida,
    TerminalRechazo,
    TerminalSinConexion,

    /// <summary>La operación necesita al Central y no respondió (listas de boda, notas de crédito de otra sucursal).</summary>
    SinConexionCentral,
    OperacionTerminalInvalida,
    CertificadoNoCargado,
    ComprobanteNoDisponible,
    EcfInvalido,
    NoInscritoFidelidad,
    EntregaInvalida,
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
/// <param name="UltimoError">Último motivo por el que no se pudo sincronizar, mientras la caja está sin conexión.</param>
/// <param name="Alertas">Tamaño de la base, documentos atrasados, hora desfasada o respaldo fallido.</param>
/// <summary>Lo que la pantalla de la caja necesita saber de su propia configuración. Nunca incluye la credencial.</summary>
/// <param name="Configurada">Ya tiene los datos de qué caja es.</param>
/// <param name="Sirve">Además el Central los acepta: la caja puede comunicarse.</param>
public sealed record DatosConfiguracionPantalla(
    bool Configurada,
    bool Sirve,
    string? SucursalCodigo,
    string? CajaCodigo,
    string? DireccionIp,
    string? UrlCentral,
    string? Problema);

/// <summary>Los datos que el técnico escribe en la pantalla de la caja la primera vez.</summary>
/// <param name="Usuario">Usuario del Central con permiso para configurar cajas; su contraseña solo se usa para comprobarlo.</param>
public sealed record SolicitudConfigurarCajaPantalla(
    string? SucursalCodigo,
    string? CajaCodigo,
    string? DireccionIp,
    string? UrlCentral,
    string? Secreto,
    string? Usuario = null,
    string? Contrasena = null);

public sealed record RespuestaConfiguracion(bool Exitosa, string Mensaje);

/// <param name="CentralConfigurado">La caja tiene su configuración y el Central la acepta.</param>
/// <param name="SinConexionPor">Por qué no hay comunicación: <c>Red</c> o <c>Credenciales</c>; nulo si está en línea.</param>
public sealed record DatosEstadoSincronizacion(
    bool CentralConfigurado,
    bool EnLinea,
    int DocumentosPendientes,
    DateTimeOffset? UltimaSincronizacion,
    string? UltimoError = null,
    IReadOnlyList<string>? Alertas = null,
    DateTimeOffset? UltimoRespaldo = null,
    string? SinConexionPor = null,
    Seguridad.DatosActualizacionCaja? Actualizacion = null);

/// <summary>Por qué la caja no está comunicada. La caja vende igual en los dos casos.</summary>
public static class MotivosSinConexion
{
    /// <summary>No hay comunicación con el Central: se reintenta solo y lo pendiente sube después.</summary>
    public const string Red = "Red";

    /// <summary>El Central no acepta esta caja: credencial revocada, caja eliminada o dirección que no cuadra.</summary>
    public const string Credenciales = "Credenciales";
}
