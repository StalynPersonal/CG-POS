using System.Net;
using System.Net.Mail;
using CgPos.Central.Aplicacion.Notificaciones;
using CgPos.Central.Aplicacion.Organizacion;
using Microsoft.Extensions.Logging;

namespace CgPos.Central.Infraestructura.Notificaciones;

/// <summary>Contraseña del buzón desde el que sale el correo. Va en la configuración del servidor (user-secrets), no en los parámetros.</summary>
public sealed record OpcionesCorreo(string? Contrasena);

internal sealed class ServicioCorreoSmtp(IParametrosCentral parametros, OpcionesCorreo opciones, ILogger<ServicioCorreoSmtp> registro) : IServicioCorreo
{
    public async Task<bool> ConfiguradoAsync(CancellationToken cancelacion = default) => await LeerAsync(cancelacion) is not null;

    public async Task<ResultadoCorreo> EnviarAsync(MensajeCorreo mensaje, CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(mensaje);
        if (string.IsNullOrWhiteSpace(mensaje.Destinatario) || !mensaje.Destinatario.Contains('@'))
            return ResultadoCorreo.Fallo("El destinatario no es un correo válido.");

        if (await LeerAsync(cancelacion) is not { } configuracion)
            return ResultadoCorreo.Fallo("El Central no tiene servidor de correo configurado.");

        try
        {
            using var cliente = new SmtpClient(configuracion.Servidor, configuracion.Puerto)
            {
                EnableSsl = configuracion.UsarTls,
                Credentials = configuracion.Usuario is { Length: > 0 } usuario
                    ? new NetworkCredential(usuario, opciones.Contrasena ?? string.Empty)
                    : CredentialCache.DefaultNetworkCredentials,
            };

            using var correo = new MailMessage(new MailAddress(configuracion.Remitente, configuracion.NombreRemitente), new MailAddress(mensaje.Destinatario))
            {
                Subject = mensaje.Asunto,
                Body = mensaje.Cuerpo,
                IsBodyHtml = false,
            };

            await cliente.SendMailAsync(correo, cancelacion);
            return ResultadoCorreo.Correcto();
        }
        catch (Exception excepcion) when (excepcion is SmtpException or InvalidOperationException or FormatException or IOException)
        {
            registro.LogWarning(excepcion, "No se pudo enviar el correo a {Destinatario}", mensaje.Destinatario);
            return ResultadoCorreo.Fallo(excepcion.Message);
        }
    }

    private async Task<Configuracion?> LeerAsync(CancellationToken cancelacion)
    {
        var servidor = await parametros.ObtenerAsync(ClavesParametrosCentral.CorreoServidor, cancelacion);
        var remitente = await parametros.ObtenerAsync(ClavesParametrosCentral.CorreoRemitente, cancelacion);
        if (servidor is not { Length: > 0 } || remitente is not { Length: > 0 })
            return null;

        var puerto = int.TryParse(await parametros.ObtenerAsync(ClavesParametrosCentral.CorreoPuerto, cancelacion), out var valor) ? valor : 587;
        return new Configuracion(
            servidor,
            puerto,
            await parametros.ObtenerBooleanoOpcionalAsync(ClavesParametrosCentral.CorreoUsarTls, cancelacion),
            await parametros.ObtenerAsync(ClavesParametrosCentral.CorreoUsuario, cancelacion),
            remitente,
            await parametros.ObtenerAsync(ClavesParametrosCentral.CorreoNombreRemitente, cancelacion) ?? "CG-POS");
    }

    private sealed record Configuracion(string Servidor, int Puerto, bool UsarTls, string? Usuario, string Remitente, string NombreRemitente);
}
