using CgPos.Contratos.Catalogo;
using CgPos.Dominio.Catalogo;
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
    bool Anulada);

public sealed record DatosDesgloseImpuesto(decimal Porcentaje, int IndicadorFacturacion, decimal Base, decimal Impuesto, decimal Total);

public sealed record DatosTotalesVenta(
    decimal Subtotal,
    decimal Impuesto,
    decimal Total,
    int CantidadLineas,
    decimal CantidadArticulos,
    IReadOnlyList<DatosDesgloseImpuesto> Desglose);

/// <param name="Lineas">En orden de pantalla: cada reverso aparece justo debajo de la línea que anula.</param>
public sealed record DatosVenta(
    Guid Id,
    string NumeroTransaccion,
    EstadoVenta Estado,
    Guid TurnoId,
    string UsuarioNombre,
    DateTimeOffset IniciadaEn,
    IReadOnlyList<DatosLineaVenta> Lineas,
    DatosTotalesVenta Totales);

/// <param name="Codigo">Código leído; admite "cantidad*código" (ej. "12*7891114119695", RF-14).</param>
public sealed record SolicitudAgregarArticulo(string Codigo, decimal? Cantidad = null);

public sealed record SolicitudCambiarCantidad(decimal Cantidad);

/// <param name="AutorizacionId">Autorización de supervisor obtenida en <c>/api/autorizaciones</c> cuando el usuario no tiene el permiso.</param>
public sealed record SolicitudConAutorizacion(Guid? AutorizacionId = null);

public sealed record SolicitudEliminarPorCodigo(string Codigo, Guid? AutorizacionId = null);

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
