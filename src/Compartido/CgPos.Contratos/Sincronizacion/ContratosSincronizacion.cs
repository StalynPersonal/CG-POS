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
    public const string CierreReabierto = "Caja.CierreReabierto";
    public const string RetiroEfectivo = "Caja.RetiroEfectivo";
    public const string RelevoCajero = "Caja.RelevoCajero";
    public const string PendienteCreado = "Entregas.PendienteCreado";
    public const string PendienteActualizado = "Entregas.PendienteActualizado";
    public const string MovimientoPuntos = "Fidelidad.MovimientoPuntos";
    public const string InscripcionFidelidad = "Fidelidad.Inscripcion";

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
/// Mensaje de la bandeja de salida tal como viaja al Central. <paramref name="Id"/> es la clave de idempotencia (RN-25) y
/// <paramref name="HashContenido"/> el SHA-256 del contenido, que el Central verifica antes de confirmar.
/// </summary>
public sealed record MensajeSincronizacion(
    Guid Id,
    string TipoMensaje,
    Guid AgregadoId,
    string Contenido,
    string HashContenido,
    Guid CajaId,
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
public sealed record PaqueteBajadaMaestros(long Desde, long Hasta, CargaInicial.PaqueteCargaInicial? Organizacion, Catalogo.PaqueteMaestros? Maestros)
{
    public bool SinCambios => Organizacion is null && Maestros is null;
}

/// <summary>Respuesta del Central a la recepción de un mensaje.</summary>
public sealed record RespuestaRecepcionCentral(EstadoRecepcion Estado, string? Error = null);
