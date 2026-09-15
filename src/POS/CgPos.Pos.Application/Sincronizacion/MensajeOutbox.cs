using System.Security.Cryptography;
using System.Text;
using CgPos.Contracts.Sincronizacion;
using CgPos.Domain.Comun;

namespace CgPos.Pos.Application.Sincronizacion;

/// <summary>
/// Mensaje pendiente de enviar al Central (patrón Outbox). Se guarda en la misma transacción que el documento
/// que lo origina, así nunca hay un documento sin su mensaje ni un mensaje sin su documento.
/// El <see cref="Entidad.Id"/> es la clave de idempotencia: reenviarlo no lo duplica en el Central (RN-25).
/// </summary>
public sealed class MensajeOutbox : Entidad
{
    public const int LargoMaximoTipo = 100;
    public const int LargoMaximoError = 2000;

    private MensajeOutbox()
    {
    }

    /// <summary>Tipo del mensaje, ej. "Factura.Emitida".</summary>
    public string TipoMensaje { get; private set; } = string.Empty;

    /// <summary>Id del documento que originó el mensaje (factura, nota de crédito, cierre…).</summary>
    public Guid AgregadoId { get; private set; }

    /// <summary>Contenido serializado en JSON.</summary>
    public string Contenido { get; private set; } = string.Empty;

    /// <summary>SHA-256 del contenido (hex, mayúsculas). El Central lo valida antes de confirmar.</summary>
    public string HashContenido { get; private set; } = string.Empty;

    public EstadoMensajeOutbox Estado { get; private set; }
    public int Intentos { get; private set; }
    public DateTimeOffset CreadoEn { get; private set; }

    /// <summary>Momento a partir del cual el mensaje puede tomarse. Nulo cuando ya está confirmado.</summary>
    public DateTimeOffset? ProximoIntentoEn { get; private set; }

    public DateTimeOffset? EnviadoEn { get; private set; }
    public DateTimeOffset? ConfirmadoEn { get; private set; }
    public string? UltimoError { get; private set; }

    public static MensajeOutbox Crear(string tipoMensaje, Guid agregadoId, string contenidoJson, DateTimeOffset ahora)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tipoMensaje);
        ArgumentException.ThrowIfNullOrWhiteSpace(contenidoJson);
        if (tipoMensaje.Length > LargoMaximoTipo)
            throw new ArgumentException($"El tipo de mensaje excede {LargoMaximoTipo} caracteres.", nameof(tipoMensaje));
        if (agregadoId == Guid.Empty)
            throw new ArgumentException("El Id del documento es obligatorio.", nameof(agregadoId));

        return new MensajeOutbox
        {
            TipoMensaje = tipoMensaje,
            AgregadoId = agregadoId,
            Contenido = contenidoJson,
            HashContenido = CalcularHash(contenidoJson),
            Estado = EstadoMensajeOutbox.Pendiente,
            CreadoEn = ahora,
            ProximoIntentoEn = ahora,
        };
    }

    public static string CalcularHash(string contenido) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(contenido)));

    public void MarcarEnProceso()
    {
        ValidarEstado(nameof(MarcarEnProceso), EstadoMensajeOutbox.Pendiente, EstadoMensajeOutbox.Error);
        Estado = EstadoMensajeOutbox.EnProceso;
        Intentos++;
    }

    public void MarcarEnviado(DateTimeOffset ahora)
    {
        ValidarEstado(nameof(MarcarEnviado), EstadoMensajeOutbox.EnProceso);
        Estado = EstadoMensajeOutbox.Enviado;
        EnviadoEn = ahora;
        UltimoError = null;
    }

    public void MarcarConfirmado(DateTimeOffset ahora)
    {
        ValidarEstado(nameof(MarcarConfirmado), EstadoMensajeOutbox.EnProceso, EstadoMensajeOutbox.Enviado);
        Estado = EstadoMensajeOutbox.Confirmado;
        EnviadoEn ??= ahora;
        ConfirmadoEn = ahora;
        ProximoIntentoEn = null;
        UltimoError = null;
    }

    public void RegistrarFallo(string error, DateTimeOffset proximoIntento)
    {
        ValidarEstado(nameof(RegistrarFallo), EstadoMensajeOutbox.EnProceso, EstadoMensajeOutbox.Enviado);
        Estado = EstadoMensajeOutbox.Error;
        UltimoError = string.IsNullOrEmpty(error) || error.Length <= LargoMaximoError ? error : error[..LargoMaximoError];
        ProximoIntentoEn = proximoIntento;
    }

    private void ValidarEstado(string operacion, params EstadoMensajeOutbox[] permitidos)
    {
        if (!permitidos.Contains(Estado))
            throw new InvalidOperationException(
                $"No se puede ejecutar {operacion} sobre un mensaje en estado {Estado}. Permitidos: {string.Join(", ", permitidos)}.");
    }
}
