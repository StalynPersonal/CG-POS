using CgPos.Contratos.Central;
using CgPos.Contratos.Sincronizacion;

namespace CgPos.Pos.Aplicacion.Sincronizacion;

/// <summary>Claves de configuración de la conexión con el Central (por instalación, en appsettings).</summary>
public static class ClavesSincronizacion
{
    /// <summary>"Simulado" usa el Central simulado en carpeta local; vacío usa <see cref="UrlCentral"/> si está configurada.</summary>
    public const string ModoCentral = "Central:Modo";

    public const string UrlCentral = "Central:Url";

    /// <summary>Secreto de la credencial de dispositivo que el Central emitió para esta caja (se configura al instalarla).</summary>
    public const string SecretoCaja = "Central:Secreto";

    /// <summary>Código de la sucursal de esta caja; con el código de la caja y el secreto forma la credencial ante el Central.</summary>
    public const string CajaSucursal = "Caja:Sucursal";

    /// <summary>Código de esta caja dentro de su sucursal.</summary>
    public const string CajaCodigo = "Caja:Codigo";

    /// <summary>Carpeta donde el Central simulado guarda los mensajes recibidos.</summary>
    public const string CarpetaSimulada = "Central:CarpetaSimulada";

    public const string IntervaloSegundos = "Sincronizacion:IntervaloSegundos";

    /// <summary>Cada cuánto la caja pide al Central los maestros cambiados (RF-269, RF-273).</summary>
    public const string IntervaloMaestrosSegundos = "Sincronizacion:IntervaloMaestrosSegundos";
    public const string TamanoLote = "Sincronizacion:TamanoLote";
    public const string EsperaInicialSegundos = "Sincronizacion:EsperaInicialSegundos";
    public const string EsperaMaximaSegundos = "Sincronizacion:EsperaMaximaSegundos";
    public const string TiempoEsperaSegundos = "Sincronizacion:TiempoEsperaSegundos";

    public const string ModoSimulado = "Simulado";
}

/// <summary>Resultado de enviar un mensaje al Central.</summary>
/// <param name="CentralRespondio">Falso si no hubo comunicación (red, tiempo de espera, error del servidor): se reintenta con espera progresiva.</param>
public sealed record ResultadoEnvioCentral(bool Confirmado, bool CentralRespondio, string? Error)
{
    public static ResultadoEnvioCentral Recibido() => new(true, true, null);

    public static ResultadoEnvioCentral Rechazado(string error) => new(false, true, error);

    public static ResultadoEnvioCentral SinConexion(string error) => new(false, false, error);
}

/// <summary>Canal hacia el Central: HTTP en producción o simulado mientras no exista (Fase C11, H2).</summary>
/// <summary>En qué punto está la caja para poder hablar con el Central.</summary>
/// <param name="Lista">Tiene credencial: puede sincronizar.</param>
/// <param name="Mensaje">Qué falta, en palabras, para el registro y la pantalla de estado.</param>
public sealed record EstadoCredencialCaja(bool Lista, string? Mensaje = null);

public interface IClienteCentral
{
    bool Configurado { get; }

    /// <summary>
    /// Si la caja todavía no tiene credencial, se anuncia al Central y queda esperando que la acepten. No se escribe ningún
    /// secreto a mano: la caja lo recibe cuando alguien aprueba su solicitud y lo guarda cifrada en el equipo.
    /// </summary>
    Task<EstadoCredencialCaja> AsegurarCredencialAsync(CancellationToken cancelacion = default);

    Task<ResultadoEnvioCentral> EnviarAsync(MensajeSincronizacion mensaje, CancellationToken cancelacion = default);

    /// <summary>Pide los maestros cambiados desde la versión que la caja ya aplicó (0 = aprovisionamiento completo).</summary>
    Task<ResultadoBajadaCentral> DescargarMaestrosAsync(long desde, CancellationToken cancelacion = default);

    /// <summary>Consulta en el Central una nota de crédito que esta caja no tiene, porque se emitió en otra sucursal (RF-43).</summary>
    Task<ResultadoNotaCreditoCentral> ConsultarNotaCreditoAsync(string codigo, CancellationToken cancelacion = default);

    /// <summary>Consulta en el Central una lista de boda por su número (RF-73); las listas no se guardan en la caja.</summary>
    Task<ResultadoListaBodaCentral> ConsultarListaBodaAsync(string numero, CancellationToken cancelacion = default);

    /// <summary>
    /// Pide al Central retener saldo de esa nota para la factura <paramref name="ventaNumero"/> mientras la caja cobra; la reserva vence sola si no
    /// se confirma. Pedirla otra vez para la misma factura la reemplaza.
    /// </summary>
    Task<ResultadoReservaNotaCredito> ReservarNotaCreditoAsync(string notaCreditoNumero, string ventaNumero, decimal monto, CancellationToken cancelacion = default);

    /// <summary>Devuelve el saldo retenido para esa factura cuando no llegó a cobrarse.</summary>
    Task LiberarReservaNotaCreditoAsync(string notaCreditoNumero, string ventaNumero, CancellationToken cancelacion = default);
}


/// <summary>Resultado de pedir maestros al Central.</summary>
/// <param name="CentralRespondio">Falso si no hubo comunicación: se reintenta en el próximo ciclo.</param>
public sealed record ResultadoBajadaCentral(PaqueteBajadaMaestros? Paquete, bool CentralRespondio, string? Error)
{
    public static ResultadoBajadaCentral Recibido(PaqueteBajadaMaestros paquete) => new(paquete, true, null);

    public static ResultadoBajadaCentral Rechazado(string error) => new(null, true, error);

    public static ResultadoBajadaCentral SinConexion(string error) => new(null, false, error);
}

/// <summary>Resultado de consultar una nota de crédito en el Central.</summary>
/// <param name="CentralRespondio">Falso si no hubo comunicación: la caja no puede validar notas de otras sucursales sin el Central.</param>
public sealed record ResultadoNotaCreditoCentral(DatosNotaCreditoParaCaja? Nota, bool CentralRespondio, string? Error)
{
    public static ResultadoNotaCreditoCentral Encontrada(DatosNotaCreditoParaCaja nota) => new(nota, true, null);

    public static ResultadoNotaCreditoCentral NoExiste(string error) => new(null, true, error);

    public static ResultadoNotaCreditoCentral SinConexion(string error) => new(null, false, error);
}

/// <summary>Consulta de una lista de boda en el Central: la lista vive allá, la caja solo la lee al vender (RF-73).</summary>
public sealed record ResultadoListaBodaCentral(DatosListaBodaParaCaja? Lista, bool CentralRespondio, string? Error)
{
    public static ResultadoListaBodaCentral Encontrada(DatosListaBodaParaCaja lista) => new(lista, true, null);

    public static ResultadoListaBodaCentral NoExiste(string error) => new(null, true, error);

    public static ResultadoListaBodaCentral SinConexion(string error) => new(null, false, error);
}

/// <param name="Monto">Lo que el Central retuvo, que puede ser menos de lo pedido.</param>
public sealed record ResultadoReservaNotaCredito(bool Exitosa, decimal Monto, string? Error, bool CentralRespondio)
{
    public static ResultadoReservaNotaCredito Reservada(decimal monto) => new(true, monto, null, true);

    public static ResultadoReservaNotaCredito Rechazada(string error) => new(false, 0m, error, true);

    public static ResultadoReservaNotaCredito SinConexion(string error) => new(false, 0m, error, false);
}

/// <summary>Último contacto con el Central, para el indicador de conexión (RF-192).</summary>
public interface IEstadoConexionCentral
{
    DateTimeOffset? UltimoContacto { get; }
    DateTimeOffset? UltimoFallo { get; }
    string? UltimoError { get; }

    void RegistrarContacto(DateTimeOffset momento);

    void RegistrarFallo(DateTimeOffset momento, string? error);
}

public sealed record ResultadoProcesoBandeja(int Tomados, int Confirmados, int Fallidos);

/// <summary>Envía al Central los mensajes pendientes de la bandeja de salida con reintentos y espera progresiva (RF-270).</summary>
public interface IProcesadorBandejaSalida
{
    Task<ResultadoProcesoBandeja> ProcesarAsync(CancellationToken cancelacion = default);
}

/// <param name="Descargado">Se recibió y aplicó un paquete (aunque viniera sin cambios).</param>
/// <param name="Version">Versión de maestros que la caja tiene aplicada después de la descarga.</param>
public sealed record ResultadoDescargaMaestros(bool Descargado, long Version, int Creados, int Actualizados, string? Error);

/// <summary>
/// Baja del Central la organización, la seguridad y los maestros cambiados y los aplica con las mismas cargas de la caja (RF-269, RF-273).
/// La marca de versión solo avanza si todo se aplicó; una caja nueva se aprovisiona con la primera descarga (RF-281).
/// </summary>
public interface IDescargaMaestros
{
    Task<ResultadoDescargaMaestros> DescargarAsync(CancellationToken cancelacion = default);
}
