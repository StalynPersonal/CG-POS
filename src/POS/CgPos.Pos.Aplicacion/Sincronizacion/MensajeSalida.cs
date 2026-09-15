using System.Security.Cryptography;
using System.Text;
using CgPos.Contratos.Sincronizacion;
using CgPos.Dominio.Comun;

namespace CgPos.Pos.Aplicacion.Sincronizacion;

/// <summary>
/// Mensaje pendiente de enviar al Central (patrón BandejaSalida). Se guarda en la misma transacción que el documento
/// que lo origina, así nunca hay un documento sin su mensaje ni un mensaje sin su documento.
/// El <see cref="Entidad.Id"/> es la clave de idempotencia: reenviarlo no lo duplica en el Central (RN-25).
/// </summary>
public sealed class MensajeSalida : Entidad
{
    public const int LargoMaximoTipo = 100;
    public const int LargoMaximoError = 2000;

    private MensajeSalida()
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

    public EstadoMensajeSalida Estado { get; private set; }
    public int Intentos { get; private set; }
    public DateTimeOffset CreadoEn { get; private set; }

    /// <summary>Momento a partir del cual el mensaje puede tomarse. Nulo cuando ya está confirmado.</summary>
    public DateTimeOffset? ProximoIntentoEn { get; private set; }

    public DateTimeOffset? EnviadoEn { get; private set; }
    public DateTimeOffset? ConfirmadoEn { get; private set; }
    public string? UltimoError { get; private set; }

    public static MensajeSalida Crear(string tipoMensaje, Guid agregadoId, string contenidoJson, DateTimeOffset ahora)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tipoMensaje);
        ArgumentException.ThrowIfNullOrWhiteSpace(contenidoJson);
        if (tipoMensaje.Length > LargoMaximoTipo)
            throw new ArgumentException($"El tipo de mensaje excede {LargoMaximoTipo} caracteres.", nameof(tipoMensaje));
        if (agregadoId == Guid.Empty)
            throw new ArgumentException("El Id del documento es obligatorio.", nameof(agregadoId));

        return new MensajeSalida
        {
            TipoMensaje = tipoMensaje,
            AgregadoId = agregadoId,
            Contenido = contenidoJson,
            HashContenido = CalcularHash(contenidoJson),
            Estado = EstadoMensajeSalida.Pendiente,
            CreadoEn = ahora,
            ProximoIntentoEn = ahora,
        };
    }

    public static string CalcularHash(string contenido) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(contenido)));

    public void MarcarEnProceso()
    {
        ValidarEstado(nameof(MarcarEnProceso), EstadoMensajeSalida.Pendiente, EstadoMensajeSalida.Error);
        Estado = EstadoMensajeSalida.EnProceso;
        Intentos++;
    }

    public void MarcarEnviado(DateTimeOffset ahora)
    {
        ValidarEstado(nameof(MarcarEnviado), EstadoMensajeSalida.EnProceso);
        Estado = EstadoMensajeSalida.Enviado;
        EnviadoEn = ahora;
        UltimoError = null;
    }

    public void MarcarConfirmado(DateTimeOffset ahora)
    {
        ValidarEstado(nameof(MarcarConfirmado), EstadoMensajeSalida.EnProceso, EstadoMensajeSalida.Enviado);
        Estado = EstadoMensajeSalida.Confirmado;
        EnviadoEn ??= ahora;
        ConfirmadoEn = ahora;
        ProximoIntentoEn = null;
        UltimoError = null;
    }

    public void RegistrarFallo(string error, DateTimeOffset proximoIntento)
    {
        ValidarEstado(nameof(RegistrarFallo), EstadoMensajeSalida.EnProceso, EstadoMensajeSalida.Enviado);
        Estado = EstadoMensajeSalida.Error;
        UltimoError = string.IsNullOrEmpty(error) || error.Length <= LargoMaximoError ? error : error[..LargoMaximoError];
        ProximoIntentoEn = proximoIntento;
    }

    private void ValidarEstado(string operacion, params EstadoMensajeSalida[] permitidos)
    {
        if (!permitidos.Contains(Estado))
            throw new InvalidOperationException(
                $"No se puede ejecutar {operacion} sobre un mensaje en estado {Estado}. Permitidos: {string.Join(", ", permitidos)}.");
    }
}
