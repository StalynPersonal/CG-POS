using System.Security.Cryptography;
using System.Text;

namespace CgPos.Contratos.Sincronizacion;

/// <summary>Tipos de mensaje que la caja envía al Central.</summary>
public static class TiposMensaje
{
    public const string VentaCobrada = "Venta.Cobrada";
    public const string NotaCreditoEmitida = "Devolucion.NotaCreditoEmitida";
    public const string NotaCreditoConsumida = "NotaCredito.Consumida";
    public const string TurnoCerrado = "Caja.TurnoCerrado";
    public const string RetiroEfectivo = "Caja.RetiroEfectivo";
    public const string RelevoCajero = "Caja.RelevoCajero";
    public const string PendienteCreado = "Entregas.PendienteCreado";
    public const string PendienteActualizado = "Entregas.PendienteActualizado";
    public const string MovimientoPuntos = "Fidelidad.MovimientoPuntos";
    public const string InscripcionFidelidad = "Fidelidad.Inscripcion";
    public const string IngresoUsuario = "Seguridad.IngresoUsuario";

    /// <summary>La caja informa por dónde va un rango de e-CF: el Central no lleva esa cuenta, la lleva quien emite.</summary>
    public const string ConsumoSecuenciaEcf = "Fiscal.ConsumoSecuenciaEcf";

    /// <summary>Documentos que llevan su e-CF firmado en la propiedad <c>ecf</c> (RF-276).</summary>
    public static IReadOnlySet<string> ConEcf { get; } = new HashSet<string>(StringComparer.Ordinal) { VentaCobrada, NotaCreditoEmitida };
}

/// <summary>SHA-256 (hex, mayúsculas) con el que caja y Central verifican la integridad del contenido y del XML.</summary>
public static class HashSincronizacion
{
    public static string Calcular(string contenido) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(contenido)));

    public static bool Coincide(string contenido, string? hash) =>
        hash is { Length: 64 } && CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(Calcular(contenido)), Encoding.ASCII.GetBytes(hash.ToUpperInvariant()));
}

/// <summary>
/// Mensaje de la bandeja de salida tal como viaja al Central. <paramref name="Id"/> es la clave de idempotencia (RN-25),
/// <paramref name="Referencia"/> el número del documento (o su llave natural, como la cédula de una inscripción) y
/// <paramref name="HashContenido"/> el SHA-256 del contenido, que el Central verifica antes de confirmar. La caja se identifica por el código de su
/// sucursal y el suyo, que deben ser los de la credencial con que se autenticó.
/// </summary>
public sealed record MensajeSincronizacion(
    Guid Id,
    string TipoMensaje,
    string Referencia,
    string Contenido,
    string HashContenido,
    string SucursalCodigo,
    string CajaCodigo,
    DateTimeOffset CreadoEn);

public enum EstadoRecepcion
{
    Recibido,

    /// <summary>Ya se había recibido con el mismo contenido: se confirma sin duplicar.</summary>
    Duplicado,

    Rechazado,
}

/// <summary>
/// Maestros que bajan a una caja (RF-269, RF-273): todo lo que cambió en el Central con versión mayor a <paramref name="Desde"/> y hasta
/// <paramref name="Hasta"/>, que la caja guarda como su nueva marca al aplicarlo. Con <c>Desde = 0</c> es el aprovisionamiento completo (RF-281).
/// </summary>
/// <param name="Organizacion">Empresa, sucursales, cajas, parámetros, roles y usuarios de caja; nulo si nada de eso cambió.</param>
/// <param name="Maestros">Catálogo, precios, promociones, fidelidad, rangos de e-CF…; nulo si nada cambió.</param>
/// <param name="EstadosDgii">Resultados de la DGII de los e-CF de esa caja que cambiaron; nulo si ninguno cambió.</param>
/// <param name="ParametrosVigentes">Todos los parámetros que hoy aplican a esa caja: los que la caja tenga y no estén aquí se borraron en el Central.</param>
public sealed record PaqueteBajadaMaestros(
    long Desde,
    long Hasta,
    CargaInicial.PaqueteCargaInicial? Organizacion,
    Catalogo.PaqueteMaestros? Maestros,
    IReadOnlyList<EstadoDgiiCarga>? EstadosDgii = null,
    IReadOnlyList<CargaInicial.ParametroReferencia>? ParametrosVigentes = null)
{
    public bool SinCambios => Organizacion is null && Maestros is null && EstadosDgii is not { Count: > 0 };
}

/// <summary>Respuesta del Central a la recepción de un mensaje.</summary>
public sealed record RespuestaRecepcionCentral(EstadoRecepcion Estado, string? Error = null);

/// <summary>Resultado de la DGII de un e-CF emitido por una caja (RF-223): el Central lo envía en la bajada y la caja lo aplica a su documento.</summary>
public sealed record EstadoDgiiCarga(
    string Encf,
    CgPos.Dominio.Sincronizacion.EstadoEnvioDgii Estado,
    DateTimeOffset? EstadoEn,
    string? Mensaje,
    string? TrackId);
