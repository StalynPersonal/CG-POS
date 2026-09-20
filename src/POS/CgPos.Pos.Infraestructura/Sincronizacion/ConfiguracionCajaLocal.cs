using CgPos.Dominio.Comun;
using CgPos.Dominio.Organizacion;
using CgPos.Pos.Aplicacion.Sincronizacion;
using CgPos.Pos.Infraestructura.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CgPos.Pos.Infraestructura.Sincronizacion;

/// <summary>
/// Guarda y entrega la configuración de esta caja: qué caja es, su dirección, dónde está el Central y su credencial. Se
/// consulta en cada comunicación, así que se recuerda en memoria y solo se vuelve a leer cuando cambia.
/// </summary>
internal sealed class ConfiguracionCajaLocal : IConfiguracionCaja
{
    private readonly IServiceScopeFactory _ambitos;
    private readonly IProteccionSecreto _proteccion;
    private readonly IValidadorConfiguracionCaja _validador;
    private readonly TimeProvider _reloj;
    private readonly ILogger<ConfiguracionCajaLocal> _registro;
    private readonly bool _exigirIpDelEquipo;
    private readonly Lock _candado = new();
    private DatosConfiguracionCaja? _recordada;
    private bool _leida;

    public ConfiguracionCajaLocal(IServiceScopeFactory ambitos, IProteccionSecreto proteccion, IValidadorConfiguracionCaja validador,
        IConfiguration configuracion, TimeProvider reloj, ILogger<ConfiguracionCajaLocal> registro)
    {
        _ambitos = ambitos;
        _proteccion = proteccion;
        _validador = validador;
        _reloj = reloj;
        _registro = registro;

        // Con 1, la caja además comprueba que su dirección configurada sea de verdad una de este equipo: así, una
        // configuración copiada a otra máquina no se comunica aunque declare la dirección correcta. Con 0, no se mira.
        _exigirIpDelEquipo = configuracion[ClavesConfiguracionCaja.ValidarIpDelEquipo] is { Length: > 0 } valor
            && (valor.Trim() == "1" || bool.TryParse(valor, out var activo) && activo);
    }

    public async Task<DatosConfiguracionCaja?> ObtenerAsync(CancellationToken cancelacion = default)
    {
        lock (_candado)
        {
            if (_leida)
                return _recordada;
        }

        await using var ambito = _ambitos.CreateAsyncScope();
        var contexto = ambito.ServiceProvider.GetRequiredService<ContextoDatosPos>();
        var guardada = await contexto.ConfiguracionCaja.AsNoTracking().OrderBy(c => c.Id).FirstOrDefaultAsync(cancelacion);

        var datos = guardada is null ? null : Armar(guardada);
        lock (_candado)
        {
            _recordada = datos;
            _leida = true;
        }

        return datos;
    }

    public async Task<string?> GuardarAsync(SolicitudConfigurarCaja solicitud, CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(solicitud);

        if (_exigirIpDelEquipo && !EsDelEquipo(solicitud.DireccionIp))
            return $"La dirección {solicitud.DireccionIp} no es de este equipo. Corríjala o apague la comprobación con {ClavesConfiguracionCaja.ValidarIpDelEquipo} = 0.";

        if (solicitud.Usuario is not { Length: > 0 } || solicitud.Contrasena is not { Length: > 0 })
            return "Escriba el usuario y la contraseña del Central que autorizan esta configuración.";

        ConfiguracionCaja configuracion;
        try
        {
            configuracion = ConfiguracionCaja.Crear(solicitud.SucursalCodigo, solicitud.CajaCodigo, solicitud.DireccionIp, solicitud.UrlCentral,
                _proteccion.Proteger(solicitud.Secreto), _reloj.Ahora(), solicitud.Usuario.Trim());
        }
        catch (ArgumentException excepcion)
        {
            return excepcion.Message;
        }

        // El Central tiene la última palabra: comprueba a la vez quién autoriza y que la caja, su dirección y su credencial
        // sean las que él tiene registradas. Nada se guarda si no la acepta.
        if (await _validador.ValidarAsync(solicitud, cancelacion) is { Length: > 0 } rechazo)
            return rechazo;

        await using var ambito = _ambitos.CreateAsyncScope();
        var contexto = ambito.ServiceProvider.GetRequiredService<ContextoDatosPos>();

        // Una caja es una sola: configurar de nuevo reemplaza lo anterior, no lo acumula.
        contexto.ConfiguracionCaja.RemoveRange(await contexto.ConfiguracionCaja.ToListAsync(cancelacion));
        contexto.ConfiguracionCaja.Add(configuracion);
        await contexto.SaveChangesAsync(cancelacion);

        Olvidar();
        _registro.LogInformation("Caja configurada como {Sucursal}-{Caja} contra {Central} por {Usuario}.",
            configuracion.SucursalCodigo, configuracion.CajaCodigo, configuracion.UrlCentral, configuracion.ConfiguradaPor);
        return null;
    }

    public async Task RechazarAsync(string motivo, CancellationToken cancelacion = default)
    {
        await using var ambito = _ambitos.CreateAsyncScope();
        var contexto = ambito.ServiceProvider.GetRequiredService<ContextoDatosPos>();
        var guardada = await contexto.ConfiguracionCaja.OrderBy(c => c.Id).FirstOrDefaultAsync(cancelacion);
        if (guardada is null || !guardada.Valida)
            return;

        guardada.Rechazar(motivo, _reloj.Ahora());
        await contexto.SaveChangesAsync(cancelacion);
        Olvidar();
        _registro.LogWarning("El Central no acepta la configuración de esta caja: {Motivo}. Vuelva a configurarla desde la pantalla.", motivo);
    }

    public async Task AceptarAsync(CancellationToken cancelacion = default)
    {
        await using var ambito = _ambitos.CreateAsyncScope();
        var contexto = ambito.ServiceProvider.GetRequiredService<ContextoDatosPos>();
        var guardada = await contexto.ConfiguracionCaja.OrderBy(c => c.Id).FirstOrDefaultAsync(cancelacion);
        if (guardada is null || guardada.Valida)
            return;

        guardada.Aceptar();
        await contexto.SaveChangesAsync(cancelacion);
        Olvidar();
        _registro.LogInformation("El Central volvió a aceptar la configuración de esta caja.");
    }

    private void Olvidar()
    {
        lock (_candado)
        {
            _recordada = null;
            _leida = false;
        }
    }

    private DatosConfiguracionCaja Armar(ConfiguracionCaja guardada)
    {
        var secreto = _proteccion.Desproteger(guardada.SecretoCifrado);
        var problema = guardada.Valida ? null : guardada.MotivoRechazo ?? "El Central no acepta esta configuración.";

        // Si la credencial no se puede descifrar, el archivo o la base vienen de otro equipo: hay que configurarla de nuevo.
        if (secreto is null)
            problema ??= "La credencial guardada no se puede leer en este equipo. Vuelva a configurar la caja.";

        // La comprobación de la dirección del propio equipo se hace aquí, en cada lectura: una caja que cambió de IP
        // (o una configuración copiada) se detiene sola en vez de hablarle al Central con datos que no son suyos.
        if (problema is null && _exigirIpDelEquipo && !EsDelEquipo(guardada.DireccionIp))
            problema = $"La dirección {guardada.DireccionIp} ya no es de este equipo. Vuelva a configurar la caja.";

        return new DatosConfiguracionCaja(
            guardada.SucursalCodigo,
            guardada.CajaCodigo,
            guardada.DireccionIp,
            guardada.UrlCentral,
            secreto,
            guardada.ConfiguradaEn,
            problema);
    }

    /// <summary>La dirección es una de las que tiene este equipo en sus tarjetas de red.</summary>
    private bool EsDelEquipo(string direccionIp)
    {
        try
        {
            return System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces()
                .SelectMany(tarjeta => tarjeta.GetIPProperties().UnicastAddresses)
                .Any(direccion => string.Equals(direccion.Address.ToString(), direccionIp?.Trim(), StringComparison.OrdinalIgnoreCase));
        }
        catch (System.Net.NetworkInformation.NetworkInformationException excepcion)
        {
            // Sin poder leer las tarjetas no se puede afirmar que la dirección no sea suya: se deja pasar y queda el aviso.
            _registro.LogWarning(excepcion, "No se pudieron leer las direcciones de red de este equipo.");
            return true;
        }
    }
}

/// <summary>Configuración que solo vive en memoria, para pruebas: lo que el técnico habría escrito en la pantalla.</summary>
internal sealed class ConfiguracionCajaEnMemoria(string? secreto, string urlCentral = "https://central.prueba/") : IConfiguracionCaja
{
    /// <summary>Por qué el Central la rechazó, si lo hizo.</summary>
    public string? Rechazo { get; private set; }

    public Task<DatosConfiguracionCaja?> ObtenerAsync(CancellationToken cancelacion = default) =>
        Task.FromResult(secreto is { Length: > 0 }
            ? new DatosConfiguracionCaja("01", "01", "10.12.1.101", urlCentral, secreto, DateTimeOffset.UnixEpoch, Rechazo)
            : null);

    public Task<string?> GuardarAsync(SolicitudConfigurarCaja solicitud, CancellationToken cancelacion = default) =>
        Task.FromResult<string?>(null);

    public Task RechazarAsync(string motivo, CancellationToken cancelacion = default)
    {
        Rechazo = motivo;
        return Task.CompletedTask;
    }

    public Task AceptarAsync(CancellationToken cancelacion = default)
    {
        Rechazo = null;
        return Task.CompletedTask;
    }
}

internal static class ClavesConfiguracionCaja
{
    /// <summary>1 exige que la dirección configurada sea una de este equipo; 0 solo compara contra el Central.</summary>
    public const string ValidarIpDelEquipo = "Caja:ValidarIpDelEquipo";
}
