using System.Security.Cryptography;
using System.Text;
using CgPos.Pos.Aplicacion.Sincronizacion;
using Microsoft.Extensions.Logging;

namespace CgPos.Pos.Infraestructura.Sincronizacion;

/// <summary>
/// Cifra la credencial de la caja con la protección de datos de Windows, atada a este equipo: si alguien copia la base de
/// datos a otra máquina, allá el texto no se puede descifrar y la caja pide configurarse de nuevo.
/// </summary>
internal sealed class ProteccionSecretoEquipo(ILogger<ProteccionSecretoEquipo> registro) : IProteccionSecreto
{
    public string Proteger(string secreto)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secreto);

        if (!OperatingSystem.IsWindows())
        {
            // Fuera de Windows no hay dónde cifrarla atada al equipo; en producción la caja siempre corre sobre Windows.
            registro.LogWarning("Este equipo no puede cifrar la credencial: solo Windows protege datos por máquina.");
            return Etiquetar(Sin, Encoding.UTF8.GetBytes(secreto.Trim()));
        }

        return Etiquetar(Dpapi, ProtectedData.Protect(Encoding.UTF8.GetBytes(secreto.Trim()), null, DataProtectionScope.LocalMachine));
    }

    public string? Desproteger(string protegido)
    {
        if (protegido is not { Length: > 0 })
            return null;

        var partes = protegido.Split(':', 2);
        if (partes is not [var marca, var contenido])
            return null;

        try
        {
            var bytes = Convert.FromBase64String(contenido);
            return marca switch
            {
                Dpapi when OperatingSystem.IsWindows() => Encoding.UTF8.GetString(ProtectedData.Unprotect(bytes, null, DataProtectionScope.LocalMachine)),
                Sin => Encoding.UTF8.GetString(bytes),
                _ => null,
            };
        }
        catch (Exception excepcion) when (excepcion is CryptographicException or FormatException)
        {
            // Viene de otro equipo o está dañada: quien llama lo informa y pide configurar la caja otra vez.
            registro.LogWarning(excepcion, "La credencial guardada no se pudo descifrar en este equipo.");
            return null;
        }
    }

    private const string Dpapi = "dpapi";
    private const string Sin = "plano";

    private static string Etiquetar(string marca, byte[] bytes) => $"{marca}:{Convert.ToBase64String(bytes)}";
}
