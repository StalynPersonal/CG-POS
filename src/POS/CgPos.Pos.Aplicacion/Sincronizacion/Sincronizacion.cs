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
/// <summary>Cuánto espera la caja entre reintentos cuando el Central no responde.</summary>
/// <param name="Inicial">Espera tras el primer fallo; se duplica con cada intento hasta la máxima.</param>
public sealed record OpcionesEspera(TimeSpan Inicial, TimeSpan Maxima)
{
    /// <summary>
    /// Espera antes del próximo intento. Un rechazo explícito del Central espera el máximo: no se resuelve reintentando
    /// enseguida, porque el mensaje no va a cambiar.
    /// </summary>
    public TimeSpan Para(int intentos, bool rechazado) =>
        rechazado
            ? Maxima
            : TimeSpan.FromSeconds(Math.Min(Inicial.TotalSeconds * Math.Pow(2, Math.Max(0, intentos - 1)), Maxima.TotalSeconds));
}

/// <summary>Configuración de esta caja, tal como está guardada en su base.</summary>
/// <param name="Secreto">La credencial ya descifrada; nula si no se pudo leer en este equipo.</param>
/// <param name="Problema">Por qué no sirve para comunicarse, o nulo si está bien.</param>
public sealed record DatosConfiguracionCaja(
    string SucursalCodigo,
    string CajaCodigo,
    string DireccionIp,
    string UrlCentral,
    string? Secreto,
    DateTimeOffset ConfiguradaEn,
    string? Problema)
{
    /// <summary>Se puede usar para hablar con el Central.</summary>
    public bool Sirve => Problema is null && Secreto is { Length: > 0 };
}

/// <summary>
/// Lo que el técnico escribe en la pantalla de la caja la primera vez: los cinco datos del equipo y, para autorizar, su
/// usuario y contraseña del Central. La contraseña solo viaja para comprobarla; en la caja no se guarda.
/// </summary>
public sealed record SolicitudConfigurarCaja(
    string SucursalCodigo,
    string CajaCodigo,
    string DireccionIp,
    string UrlCentral,
    string Secreto,
    string Usuario,
    string Contrasena);

/// <summary>Le pregunta al Central si acepta estos datos antes de guardarlos, con el usuario que autoriza.</summary>
public interface IValidadorConfiguracionCaja
{
    /// <returns>El motivo por el que el Central no la acepta, o nulo si la aceptó.</returns>
    Task<string?> ValidarAsync(SolicitudConfigurarCaja solicitud, CancellationToken cancelacion = default);
}

/// <summary>Qué caja es este equipo. Se llena desde la propia pantalla de la caja y se guarda en su base.</summary>
public interface IConfiguracionCaja
{
    /// <summary>Nula si la caja todavía no se ha configurado: entonces hay que pedir los datos en pantalla.</summary>
    Task<DatosConfiguracionCaja?> ObtenerAsync(CancellationToken cancelacion = default);

    /// <returns>El motivo por el que no se pudo guardar, o nulo si quedó configurada.</returns>
    Task<string?> GuardarAsync(SolicitudConfigurarCaja solicitud, CancellationToken cancelacion = default);

    /// <summary>
    /// Olvida hasta dónde se bajaron los maestros, para que el Central los vuelva a mandar todos desde cero. Lo pide el
    /// técnico cuando la base de la caja perdió datos: el Central solo manda lo que cambió allá, así que lo borrado aquí
    /// no vuelve por su cuenta.
    /// </summary>
    /// <returns>El motivo por el que no se pudo, o nulo si la caja quedó lista para volver a bajarlo todo.</returns>
    Task<string?> ReaprovisionarAsync(string usuario, string contrasena, CancellationToken cancelacion = default);

    /// <summary>El Central no acepta estos datos: se marca para que la caja avise y pida configurarse de nuevo.</summary>
    Task RechazarAsync(string motivo, CancellationToken cancelacion = default);

    /// <summary>El Central volvió a aceptarla.</summary>
    Task AceptarAsync(CancellationToken cancelacion = default);
}

/// <summary>Cifra la credencial de la caja contra este equipo: copiar la base a otra máquina no la deja legible.</summary>
public interface IProteccionSecreto
{
    string Proteger(string secreto);

    /// <summary>Nulo si el texto no se puede descifrar en este equipo.</summary>
    string? Desproteger(string protegido);
}

/// <summary>
/// Cada cuánto trabaja la caja por dentro. Son reglas del negocio: se configuran en el Central (en general, por sucursal o
/// por caja) y se leen en cada ciclo, así que cambiarlas allá las cambia aquí sin reiniciar nada.
/// </summary>
public interface IRitmosOperacion
{
    Task<TimeSpan> IntervaloSincronizacionAsync(CancellationToken cancelacion = default);

    Task<TimeSpan> IntervaloMaestrosAsync(CancellationToken cancelacion = default);

    Task<TimeSpan> IntervaloMantenimientoAsync(CancellationToken cancelacion = default);

    /// <summary>Mensajes que se envían al Central por ciclo.</summary>
    Task<int> TamanoLoteAsync(CancellationToken cancelacion = default);

    Task<OpcionesEspera> EsperasAsync(CancellationToken cancelacion = default);

    /// <summary>Hora del día desde la que toca el respaldo diario; nula si el negocio no lo activó.</summary>
    Task<int?> HoraRespaldoAsync(CancellationToken cancelacion = default);

    /// <summary>Servidor de hora contra el que se compara el reloj; nulo si no se verifica.</summary>
    Task<string?> ServidorHoraAsync(CancellationToken cancelacion = default);
}

public interface IClienteCentral
{
    bool Configurado { get; }

    Task<ResultadoEnvioCentral> EnviarAsync(MensajeSincronizacion mensaje, CancellationToken cancelacion = default);

    /// <summary>Pide los maestros cambiados desde la versión que la caja ya aplicó (0 = aprovisionamiento completo).</summary>
    /// <param name="conTotales">Pide además cuántas filas hay que bajar en total; se pide una vez, en la primera tanda.</param>
    Task<ResultadoBajadaCentral> DescargarMaestrosAsync(long desde, bool conTotales = false, CancellationToken cancelacion = default);

    /// <summary>Consulta en el Central una nota de crédito que esta caja no tiene, porque se emitió en otra sucursal (RF-43).</summary>
    Task<ResultadoNotaCreditoCentral> ConsultarNotaCreditoAsync(string codigo, CancellationToken cancelacion = default);

    /// <summary>Consulta en el Central una lista de boda por su número (RF-73); las listas no se guardan en la caja.</summary>
    Task<ResultadoListaBodaCentral> ConsultarListaBodaAsync(string numero, CancellationToken cancelacion = default);

    /// <summary>Busca una cotización del Central por su número, para convertirla en factura.</summary>
    Task<ResultadoCotizacionCentral> ConsultarCotizacionAsync(string numero, CancellationToken cancelacion = default);

    /// <summary>
    /// Busca en el Central una factura de cualquier tienda para devolverla, por su número o su e-NCF. El Central es el único que ve
    /// las devoluciones de toda la empresa: en su respuesta viene cuánto se devolvió ya de cada línea.
    /// </summary>
    Task<ResultadoFacturaCentral> ConsultarFacturaAsync(string numeroOEncf, CancellationToken cancelacion = default);

    /// <summary>
    /// Pide al Central retener las líneas de esa factura mientras esta caja emite la nota, para que dos tiendas no devuelvan la misma
    /// mercancía. La reserva vence sola si la nota no llega.
    /// </summary>
    Task<ResultadoReservaFactura> ReservarFacturaAsync(string facturaNumero, IReadOnlyDictionary<int, decimal> lineas,
        CancellationToken cancelacion = default);

    /// <summary>Suelta las líneas retenidas cuando la devolución no llegó a emitirse.</summary>
    Task LiberarReservaFacturaAsync(string facturaNumero, CancellationToken cancelacion = default);

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

/// <summary>Consulta de una cotización en el Central: vive allá, la caja solo la lee para facturarla.</summary>
public sealed record ResultadoCotizacionCentral(DatosCotizacionParaCaja? Cotizacion, bool CentralRespondio, string? Error)
{
    public static ResultadoCotizacionCentral Encontrada(DatosCotizacionParaCaja cotizacion) => new(cotizacion, true, null);

    public static ResultadoCotizacionCentral NoExiste(string error) => new(null, true, error);

    public static ResultadoCotizacionCentral SinConexion(string error) => new(null, false, error);
}

/// <summary>Consulta de una factura en el Central para devolverla: puede ser de cualquier sucursal y no se guarda en la caja.</summary>
public sealed record ResultadoFacturaCentral(DatosFacturaParaCaja? Factura, bool CentralRespondio, string? Error)
{
    public static ResultadoFacturaCentral Encontrada(DatosFacturaParaCaja factura) => new(factura, true, null);

    public static ResultadoFacturaCentral NoExiste(string error) => new(null, true, error);

    public static ResultadoFacturaCentral SinConexion(string error) => new(null, false, error);
}

/// <summary>Reserva de las líneas de una factura mientras se emite la nota de crédito.</summary>
public sealed record ResultadoReservaFactura(bool Exitosa, string? Error, bool CentralRespondio)
{
    public static ResultadoReservaFactura Reservada() => new(true, null, true);

    public static ResultadoReservaFactura Rechazada(string error) => new(false, error, true);

    public static ResultadoReservaFactura SinConexion(string error) => new(false, error, false);
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
/// <param name="Version">La versión de maestros que la caja tiene aplicada al terminar.</param>
/// <param name="Completo">Ya no falta nada por bajar; en <c>false</c> el Central cortó el rango y queda otra página por pedir.</param>
public sealed record ResultadoDescargaMaestros(bool Descargado, long Version, int Creados, int Actualizados, string? Error, bool Completo = true)
{
    /// <summary>La versión a la que llegó esta página, que es desde donde se pide la siguiente.</summary>
    public long Hasta => Version;

    public bool Exitosa => Descargado;
}

/// <summary>
/// Baja del Central la organización, la seguridad y los maestros cambiados y los aplica con las mismas cargas de la caja (RF-269, RF-273).
/// La marca de versión solo avanza si todo se aplicó; una caja nueva se aprovisiona con la primera descarga (RF-281).
/// </summary>
public interface IDescargaMaestros
{
    Task<ResultadoDescargaMaestros> DescargarAsync(CancellationToken cancelacion = default);
}
