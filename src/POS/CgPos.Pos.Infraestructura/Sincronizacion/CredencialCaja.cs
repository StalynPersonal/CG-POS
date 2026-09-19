using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CgPos.Contratos.Serializacion;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CgPos.Pos.Infraestructura.Sincronizacion;

/// <summary>Lo que la caja guarda de su credencial. El secreto solo existe aquí y cifrado; nunca en un archivo de configuración.</summary>
/// <param name="Instalacion">Identificador que esta instalación se dio a sí misma; junto con el equipo forma la huella.</param>
/// <param name="TokenSolicitud">Secreto propio de la solicitud de enrolamiento: sin él, nadie más recoge la credencial aprobada.</param>
internal sealed record DatosCredencialCaja(string Instalacion, string TokenSolicitud, string? Secreto);

/// <summary>Credencial con la que la caja se identifica ante el Central, y la huella del equipo donde está instalada.</summary>
internal interface ICredencialCaja
{
    /// <summary>Identifica al equipo. El Central ata la credencial a este valor: la misma credencial no sirve en otra máquina.</summary>
    string HuellaEquipo { get; }

    string NombreEquipo { get; }

    /// <summary>Token de la solicitud de enrolamiento, propio de esta instalación.</summary>
    string TokenSolicitud { get; }

    /// <summary>Nulo mientras la caja no tenga credencial: entonces hay que enrolarla.</summary>
    string? Secreto { get; }

    void Guardar(string secreto);

    /// <summary>El Central ya no la acepta (revocada, o el equipo cambió): se descarta para volver a pedir uno nuevo.</summary>
    void Olvidar();
}

/// <summary>
/// Guarda la credencial en un archivo cifrado con DPAPI, atado a este equipo: copiarlo a otra máquina no sirve de nada, porque
/// allá no se puede descifrar. La carpeta la protege además el propio Windows con los permisos de la cuenta del servicio.
/// </summary>
/// <remarks>
/// La huella del equipo combina lo que identifica a la máquina (su nombre y el identificador que le puso Windows) con un
/// identificador que esta instalación genera la primera vez. Así, dos cajas en el mismo equipo (desarrollo o pruebas) no se
/// confunden, y restaurar el respaldo de una caja en otra máquina no la suplanta, porque el pedazo del equipo cambia.
/// </remarks>
internal sealed class CredencialCajaProtegida : ICredencialCaja
{
    private const string ClaveWindows = @"SOFTWARE\Microsoft\Cryptography";
    private const string ValorWindows = "MachineGuid";

    private readonly string _archivo;
    private readonly ILogger<CredencialCajaProtegida> _registro;
    private readonly Lock _candado = new();
    private DatosCredencialCaja _datos;

    public CredencialCajaProtegida(IConfiguration configuracion, ILogger<CredencialCajaProtegida> registro)
    {
        _registro = registro;
        _archivo = configuracion[ClavesCredencialCaja.Archivo] is { Length: > 0 } ruta
            ? Path.GetFullPath(ruta)
            : ClavesCredencialCaja.ArchivoPredeterminado;

        NombreEquipo = Environment.MachineName;
        _datos = Leer() ?? Crear();

        // Una instalación que venía con el secreto en la configuración lo pasa al archivo cifrado y deja de necesitarlo ahí.
        if (_datos.Secreto is null && configuracion[Aplicacion.Sincronizacion.ClavesSincronizacion.SecretoCaja] is { Length: > 0 } configurado)
        {
            Guardar(configurado.Trim());
            _registro.LogInformation("La credencial de la caja se guardó cifrada en {Archivo}: ya puede quitarla de la configuración.", _archivo);
        }

        HuellaEquipo = CalcularHuella(_datos.Instalacion);
    }

    public string HuellaEquipo { get; private set; }

    public string NombreEquipo { get; }

    public string TokenSolicitud => _datos.TokenSolicitud;

    public string? Secreto => _datos.Secreto;

    public void Guardar(string secreto)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secreto);
        Escribir(_datos with { Secreto = secreto.Trim() });
    }

    public void Olvidar()
    {
        // El token también se renueva: la solicitud anterior quedó cerrada y la próxima debe entrar como nueva.
        Escribir(_datos with { TokenSolicitud = Aleatorio(), Secreto = null });
    }

    /// <summary>Datos de una caja que todavía no tiene credencial. No se escriben aún: el archivo nace cuando hay algo que guardar.</summary>
    private static DatosCredencialCaja Crear() => new(Aleatorio(), Aleatorio(), null);

    private void Escribir(DatosCredencialCaja datos)
    {
        lock (_candado)
        {
            _datos = datos;
            try
            {
                if (!OperatingSystem.IsWindows())
                {
                    // Sin DPAPI no hay dónde guardarla a salvo, y guardarla en claro sería peor: se queda en memoria y la
                    // caja volverá a pedir su credencial al reiniciar. En producción la caja siempre corre sobre Windows.
                    _registro.LogWarning("La credencial de la caja no se guarda: el cifrado del equipo solo existe en Windows.");
                    return;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(_archivo)!);
                var claro = JsonSerializer.SerializeToUtf8Bytes(datos, OpcionesJson.Predeterminadas);
                File.WriteAllBytes(_archivo, ProtectedData.Protect(claro, null, DataProtectionScope.LocalMachine));
            }
            catch (Exception excepcion) when (excepcion is IOException or UnauthorizedAccessException or CryptographicException)
            {
                // La caja sigue funcionando con lo que tiene en memoria; lo que se pierde es recordarlo al reiniciar.
                _registro.LogError(excepcion, "No se pudo guardar la credencial de la caja en {Archivo}", _archivo);
            }
        }
    }

    private DatosCredencialCaja? Leer()
    {
        if (!OperatingSystem.IsWindows() || !File.Exists(_archivo))
            return null;

        try
        {
            var claro = ProtectedData.Unprotect(File.ReadAllBytes(_archivo), null, DataProtectionScope.LocalMachine);
            return JsonSerializer.Deserialize<DatosCredencialCaja>(claro, OpcionesJson.Predeterminadas) is { Instalacion.Length: > 0 } datos ? datos : null;
        }
        catch (Exception excepcion) when (excepcion is IOException or UnauthorizedAccessException or CryptographicException or JsonException)
        {
            // Archivo de otro equipo, dañado o de otra cuenta: se empieza de cero y la caja pedirá enrolarse otra vez.
            _registro.LogWarning(excepcion, "No se pudo leer la credencial guardada en {Archivo}: la caja volverá a pedir su credencial.", _archivo);
            return null;
        }
    }

    private string CalcularHuella(string instalacion)
    {
        var equipo = $"{IdentificadorDeWindows()}|{NombreEquipo}|{instalacion}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(equipo)));
    }

    /// <summary>Identificador que Windows le pone al equipo al instalarse. Si no se puede leer, queda el nombre de la máquina.</summary>
    private static string IdentificadorDeWindows()
    {
        if (!OperatingSystem.IsWindows())
            return Environment.MachineName;

        try
        {
            using var clave = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(ClaveWindows);
            return clave?.GetValue(ValorWindows)?.ToString() is { Length: > 0 } valor ? valor : Environment.MachineName;
        }
        catch (Exception excepcion) when (excepcion is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            return Environment.MachineName;
        }
    }

    private static string Aleatorio() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
}

/// <summary>Credencial que solo vive en memoria: para pruebas y para el Central simulado, sin tocar el equipo.</summary>
internal sealed class CredencialCajaEnMemoria(string? secreto = null, string? huellaEquipo = null) : ICredencialCaja
{
    public string HuellaEquipo { get; } = huellaEquipo ?? Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("credencial-en-memoria")));

    public string NombreEquipo => Environment.MachineName;

    public string TokenSolicitud { get; } = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

    public string? Secreto { get; private set; } = secreto;

    public void Guardar(string secreto) => Secreto = secreto;

    public void Olvidar() => Secreto = null;
}

internal static class ClavesCredencialCaja
{
    /// <summary>Archivo donde la caja guarda su credencial cifrada.</summary>
    public const string Archivo = "Central:ArchivoCredencial";

    public static readonly string ArchivoPredeterminado = Path.Combine(@"C:\CGPOS", "credencial-caja.dat");
}
