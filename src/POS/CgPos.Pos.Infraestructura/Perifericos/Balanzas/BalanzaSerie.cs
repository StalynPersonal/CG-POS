using System.Globalization;
using System.IO.Ports;
using System.Text;
using System.Text.RegularExpressions;
using CgPos.Pos.Aplicacion.Perifericos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CgPos.Pos.Infraestructura.Perifericos.Balanzas;

/// <summary>
/// Balanza conectada por puerto serie (o por USB que se presenta como COM). Todo lo propio del modelo vive en su
/// <see cref="PerfilBalanza"/>: aquí solo se abre el puerto, se pide el peso y se lee la respuesta. Cambiar de balanza
/// es cambiar el perfil; si no responde, la caja sigue operando y el cajero digita el peso.
/// </summary>
internal sealed class BalanzaSerie(IConfiguration configuracion, ILogger<BalanzaSerie> registro) : IBalanza, IDisposable
{
    private readonly SemaphoreSlim _bloqueo = new(1, 1);
    private SerialPort? _puerto;
    private PerfilBalanza? _perfil;

    public async Task<LecturaPeso?> LeerPesoAsync(CancellationToken cancelacion = default)
    {
        var seccion = configuracion.GetSection("Perifericos:Balanza");
        if (seccion["Puerto"] is not { Length: > 0 } nombrePuerto)
        {
            registro.LogWarning("La balanza no tiene puerto configurado (Perifericos:Balanza:Puerto)");
            return null;
        }

        await _bloqueo.WaitAsync(cancelacion);
        try
        {
            var perfil = _perfil ??= PerfilesBalanza.Desde(configuracion);
            var puerto = Abrir(nombrePuerto, perfil);
            if (puerto is null)
                return null;

            // Se descarta lo que haya quedado de una lectura anterior para no leer un peso viejo.
            puerto.DiscardInBuffer();
            if (perfil.Comando is { Length: > 0 })
                puerto.Write(perfil.Comando + (perfil.Terminador ?? string.Empty));

            var respuesta = await LeerRespuestaAsync(puerto, perfil, cancelacion);
            if (respuesta is null)
            {
                registro.LogWarning("La balanza {Modelo} en {Puerto} no respondió", perfil.Nombre, nombrePuerto);
                return null;
            }

            return Interpretar(respuesta, perfil);
        }
        catch (Exception excepcion) when (excepcion is IOException or UnauthorizedAccessException or InvalidOperationException or TimeoutException)
        {
            registro.LogWarning(excepcion, "No se pudo leer la balanza en {Puerto}", nombrePuerto);
            Cerrar();
            return null;
        }
        finally
        {
            _bloqueo.Release();
        }
    }

    /// <summary>Convierte la respuesta del equipo en una lectura: peso, unidad y si el modelo la reporta estable.</summary>
    internal static LecturaPeso? Interpretar(string respuesta, PerfilBalanza perfil)
    {
        ArgumentNullException.ThrowIfNull(perfil);
        var coincidencia = Regex.Match(respuesta, perfil.Patron, RegexOptions.None, TimeSpan.FromSeconds(1));
        if (!coincidencia.Success || !decimal.TryParse(coincidencia.Groups["peso"].Value.Replace(',', '.'), NumberStyles.Number,
                CultureInfo.InvariantCulture, out var peso))
            return null;

        var unidad = coincidencia.Groups["unidad"].Success && coincidencia.Groups["unidad"].Value is { Length: > 0 } leida
            ? leida.ToUpperInvariant()
            : perfil.UnidadPredeterminada;

        // Si el modelo informa estabilidad, un peso inestable no se acepta: el cajero repite la pesada.
        var estable = perfil.Estables.Count == 0
            || (coincidencia.Groups["estado"].Success && perfil.Estables.Contains(coincidencia.Groups["estado"].Value, StringComparer.OrdinalIgnoreCase));

        return new LecturaPeso(estable && peso > 0, peso, unidad);
    }

    private SerialPort? Abrir(string nombrePuerto, PerfilBalanza perfil)
    {
        if (_puerto is { IsOpen: true } abierto && string.Equals(abierto.PortName, nombrePuerto, StringComparison.OrdinalIgnoreCase))
            return abierto;

        Cerrar();
        var puerto = new SerialPort(nombrePuerto, perfil.Baudios, perfil.Paridad, perfil.BitsDatos, perfil.BitsParada)
        {
            ReadTimeout = perfil.MilisegundosEspera,
            WriteTimeout = perfil.MilisegundosEspera,
            Encoding = Encoding.ASCII,
            DtrEnable = true,
            RtsEnable = true,
        };

        puerto.Open();
        registro.LogInformation("Balanza {Modelo} abierta en {Puerto} a {Baudios} baudios", perfil.Nombre, nombrePuerto, perfil.Baudios);
        _puerto = puerto;
        return puerto;
    }

    private static async Task<string?> LeerRespuestaAsync(SerialPort puerto, PerfilBalanza perfil, CancellationToken cancelacion)
    {
        var limite = DateTime.UtcNow.AddMilliseconds(perfil.MilisegundosEspera);
        var texto = new StringBuilder();

        while (DateTime.UtcNow < limite && !cancelacion.IsCancellationRequested)
        {
            if (puerto.BytesToRead > 0)
            {
                texto.Append(puerto.ReadExisting());
                var leido = texto.ToString();

                // Con terminador se espera la línea completa; sin él basta con que haya llegado algo.
                if (perfil.Terminador is not { Length: > 0 } terminador || leido.Contains(terminador, StringComparison.Ordinal))
                    return leido;
            }

            await Task.Delay(50, cancelacion);
        }

        return texto.Length > 0 ? texto.ToString() : null;
    }

    private void Cerrar()
    {
        try
        {
            _puerto?.Dispose();
        }
        catch (IOException)
        {
            // El puerto ya no está disponible (equipo desconectado); no hay nada que cerrar.
        }

        _puerto = null;
    }

    public void Dispose()
    {
        Cerrar();
        _bloqueo.Dispose();
    }
}
