using System.Globalization;
using CgPos.Contratos.Seguridad;
using CgPos.Dominio.Organizacion;

namespace CgPos.Pos.Aplicacion.Organizacion;

/// <summary>
/// Identidad de esta terminal: la caja que opera este equipo, configurada por el código de su sucursal y el suyo (<c>Caja:Sucursal</c> y
/// <c>Caja:Codigo</c>). El Id es el de esta base; nulo mientras la caja no exista en ella (antes de la primera carga del Central).
/// </summary>
public interface IContextoCaja
{
    string? SucursalCodigo { get; }
    string? CajaCodigo { get; }
    int? CajaId { get; }
}

/// <summary>Lectura de parámetros con precedencia: caja → sucursal de la caja → general.</summary>
public interface IParametros
{
    Task<string?> ObtenerAsync(string clave, int? cajaId = null, CancellationToken cancelacion = default);
}

public static class ParametrosExtensiones
{
    /// <exception cref="ParametroNoConfiguradoExcepcion">El parámetro no existe o está vacío.</exception>
    public static async Task<string> ObtenerRequeridoAsync(this IParametros parametros, string clave, int? cajaId, CancellationToken cancelacion = default) =>
        await parametros.ObtenerAsync(clave, cajaId, cancelacion) is { Length: > 0 } valor ? valor : throw new ParametroNoConfiguradoExcepcion(clave);

    public static async Task<int> ObtenerEnteroAsync(this IParametros parametros, string clave, int? cajaId, CancellationToken cancelacion = default) =>
        int.TryParse(await parametros.ObtenerRequeridoAsync(clave, cajaId, cancelacion), NumberStyles.Integer, CultureInfo.InvariantCulture, out var valor)
            ? valor
            : throw new ParametroNoConfiguradoExcepcion(clave, "no es un número entero");

    public static async Task<decimal> ObtenerDecimalAsync(this IParametros parametros, string clave, int? cajaId, CancellationToken cancelacion = default) =>
        decimal.TryParse(await parametros.ObtenerRequeridoAsync(clave, cajaId, cancelacion), NumberStyles.Number, CultureInfo.InvariantCulture, out var valor)
            ? valor
            : throw new ParametroNoConfiguradoExcepcion(clave, "no es un número válido (use punto decimal)");

    public static async Task<bool> ObtenerBooleanoAsync(this IParametros parametros, string clave, int? cajaId, CancellationToken cancelacion = default) =>
        bool.TryParse(await parametros.ObtenerRequeridoAsync(clave, cajaId, cancelacion), out var valor)
            ? valor
            : throw new ParametroNoConfiguradoExcepcion(clave, "debe ser true o false");

    /// <summary>Para reglas que el negocio puede no activar: <c>false</c> si no está configurado, error si está mal escrito.</summary>
    public static async Task<bool> ObtenerBooleanoOpcionalAsync(this IParametros parametros, string clave, int? cajaId, CancellationToken cancelacion = default) =>
        await parametros.ObtenerAsync(clave, cajaId, cancelacion) is not { Length: > 0 } texto ? false
        : bool.TryParse(texto, out var valor) ? valor
        : throw new ParametroNoConfiguradoExcepcion(clave, "debe ser true o false");

    /// <summary>Como el decimal opcional, para los que se cuentan de uno en uno (comprobantes que quedan, días…).</summary>
    public static async Task<int?> ObtenerEnteroOpcionalAsync(this IParametros parametros, string clave, int? cajaId, CancellationToken cancelacion = default) =>
        await parametros.ObtenerAsync(clave, cajaId, cancelacion) is not { Length: > 0 } texto ? null
        : int.TryParse(texto, NumberStyles.Integer, CultureInfo.InvariantCulture, out var valor) ? valor
        : throw new ParametroNoConfiguradoExcepcion(clave, "no es un número entero");

    /// <summary>Para valores que el negocio puede no usar (ej. fondo sugerido): nulo si no está configurado, error si está mal escrito.</summary>
    public static async Task<decimal?> ObtenerDecimalOpcionalAsync(this IParametros parametros, string clave, int? cajaId, CancellationToken cancelacion = default) =>
        await parametros.ObtenerAsync(clave, cajaId, cancelacion) is not { Length: > 0 } texto ? null
        : decimal.TryParse(texto, NumberStyles.Number, CultureInfo.InvariantCulture, out var valor) ? valor
        : throw new ParametroNoConfiguradoExcepcion(clave, "no es un número válido (use punto decimal)");
}

/// <summary>Claves de parámetros conocidas. Los valores numéricos usan formato invariante (punto decimal).</summary>
public static class ClavesParametros
{
    /// <summary>Código ISO de la moneda local de la caja; debe existir activa en el maestro de monedas.</summary>
    public const string MonedaLocal = "General.MonedaLocal";

    /// <summary>Segundos entre ciclos de sincronización con el Central. Opcional: sin él, el valor de la instalación.</summary>
    public const string IntervaloSincronizacionSegundos = "Sincronizacion.IntervaloSegundos";

    /// <summary>Segundos entre descargas de maestros del Central. Opcional: sin él, el valor de la instalación.</summary>
    public const string IntervaloMaestrosSegundos = "Sincronizacion.IntervaloMaestrosSegundos";

    /// <summary>Mensajes que la caja envía al Central por ciclo. Opcional: sin él, el valor de la instalación.</summary>
    public const string TamanoLoteSincronizacion = "Sincronizacion.TamanoLote";

    /// <summary>Segundos de espera tras el primer fallo de comunicación; se duplica con cada intento. Opcional.</summary>
    public const string EsperaInicialSegundos = "Sincronizacion.EsperaInicialSegundos";

    /// <summary>Tope de la espera entre reintentos, en segundos. Opcional.</summary>
    public const string EsperaMaximaSegundos = "Sincronizacion.EsperaMaximaSegundos";

    /// <summary>Segundos que la caja espera una respuesta del Central antes de darla por perdida. Opcional.</summary>
    public const string TiempoEsperaCentralSegundos = "Sincronizacion.TiempoEsperaSegundos";

    /// <summary>Minutos entre ciclos de mantenimiento (respaldo, purga y verificación de la hora). Opcional.</summary>
    public const string IntervaloMantenimientoMinutos = "Mantenimiento.IntervaloMinutos";

    /// <summary>Hora del día (0 a 23) a partir de la cual se hace el respaldo diario. Opcional: sin él no se respalda solo.</summary>
    public const string HoraRespaldo = "Respaldo.Hora";

    /// <summary>Servidor de hora contra el que se compara el reloj de la caja. Opcional: sin él no se verifica.</summary>
    public const string ServidorHora = "Reloj.ServidorNtp";

    /// <summary>Días que la caja conserva los XML de e-CF ya confirmados por el Central (carpeta Enviados). Opcional: sin él no se purgan.</summary>
    public const string DiasRetencionXmlEnviados = "Sincronizacion.DiasRetencionXmlEnviados";

    /// <summary>Días que la caja conserva los mensajes ya confirmados de la bandeja de salida. Opcional: sin él no se purgan.</summary>
    public const string DiasRetencionMensajesConfirmados = "Sincronizacion.DiasRetencionMensajesConfirmados";

    /// <summary>Tamaño de la base de datos (MB) desde el que se alerta; SQL Server Express limita cada base a 10 GB. Opcional.</summary>
    public const string AlertaTamanoBaseDatosMb = "Sincronizacion.AlertaTamanoBaseDatosMb";

    /// <summary>Horas sin sincronizar un documento desde las que se alerta. Opcional.</summary>
    public const string HorasAlertaPendientes = "Sincronizacion.HorasAlertaPendientes";

    /// <summary>Días que se conservan los respaldos en su carpeta. Opcional: sin él no se borran.</summary>
    public const string DiasRetencionRespaldos = "Respaldo.DiasRetencion";

    /// <summary>Segundos de diferencia con el servidor de hora desde los que se alerta (los e-CF registran la hora de firma). Opcional.</summary>
    public const string ToleranciaRelojSegundos = "Reloj.ToleranciaSegundos";

    /// <summary>Política impresa en el voucher de pendiente de entrega o envío (RF-249). Opcional.</summary>
    public const string PoliticaPendiente = "Entregas.PoliticaPendiente";

    /// <summary>Valor en moneda local de cada punto de fidelidad al canjearlo (RF-239). Sin él no se canjean puntos.</summary>
    public const string ValorPuntoFidelidad = "Fidelidad.ValorPunto";

    /// <summary>Meses de vigencia de los puntos acumulados (RF-242). Opcional: sin él la caja no les pone vencimiento.</summary>
    public const string MesesVigenciaPuntos = "Fidelidad.MesesVigenciaPuntos";

    /// <summary>Puntos mínimos por canje. Opcional.</summary>
    public const string MinimoPuntosCanje = "Fidelidad.MinimoPuntosCanje";

    /// <summary>Máximo de puntos que se canjean por transacción mientras la caja no confirma el saldo con el Central (RF-243). Opcional.</summary>
    public const string MaximoPuntosCanjeSinConexion = "Fidelidad.MaximoPuntosCanjeSinConexion";

    public const string IntentosMaximosClave = "Seguridad.IntentosMaximosClave";
    public const string MinutosBloqueo = "Seguridad.MinutosBloqueo";

    /// <summary>Minutos que tiene el usuario para usar una autorización de supervisor concedida.</summary>
    public const string MinutosVigenciaAutorizacion = "Seguridad.MinutosVigenciaAutorizacion";

    /// <summary>Horas que dura la sesión de un usuario en la caja antes de volver a ingresar.</summary>
    public const string HorasSesion = "Seguridad.HorasSesion";
    public const string FondoPredeterminado = "Caja.FondoPredeterminado";

    /// <summary>Hasta cuántas unidades puede digitar el cajero de un artículo de unidad entera; más, pasándolo por el lector.</summary>
    public const string CantidadMaximaDigitada = CatalogoParametros.CantidadMaximaDigitada;

    /// <summary>Si el fondo de caja forma parte del efectivo esperado en el cuadre. Por defecto false: el fondo no se mezcla con el cuadre (RF-4).</summary>
    public const string FondoEnCuadre = "Caja.FondoEnCuadre";

    /// <summary>Con un turno abierto de un día anterior la caja no vende ni cobra hasta cerrarlo. Por defecto true.</summary>
    public const string BloquearVentaTurnoDiaAnterior = "Caja.BloquearVentaTurnoDiaAnterior";

    /// <summary>Total desde el cual la factura de consumo exige cédula o RNC (RF-26, RF-171). Por defecto 250000.</summary>
    public const string MontoIdentificacionConsumo = "Fiscal.MontoIdentificacionConsumo";

    /// <summary>
    /// Porcentaje de retención de la Ley 32-23 que se le descuenta a un cliente de régimen especial (E44) de lo que paga en caja.
    /// Por defecto 0 = sin retención.
    /// </summary>
    public const string PorcentajeRetencionLey3223 = CatalogoParametros.PorcentajeRetencionLey3223;

    /// <summary>Múltiplo al que se redondea el total cobrado con efectivo, ej. 1 = al peso (RF-216). Por defecto 0 = sin redondeo.</summary>
    public const string PasoRedondeoEfectivo = "Caja.PasoRedondeoEfectivo";

    /// <summary>Días desde la factura tras los cuales la devolución retiene el ITBIS (RF-44). Por defecto 30.</summary>
    public const string DiasRetencionImpuestoDevolucion = "Devoluciones.DiasRetencionImpuesto";

    /// <summary>Días desde la emisión en que se puede consumir una nota de crédito (RF-39, RF-40); se evalúa al usarla.</summary>
    public const string DiasVigenciaNotaCredito = CatalogoParametros.DiasVigenciaNotaCredito;

    /// <summary>Tipo de ingresos de los e-CF según la tabla de la DGII (1 a 6, ej. 1 = ingresos por operaciones).</summary>
    public const string TipoIngresos = "Fiscal.TipoIngresos";

    /// <summary>
    /// Suspender la caja pide autorización de un supervisor. Opcional: sin él, no la pide. Apagado es lo normal: es la
    /// única manera de dejar la caja sola, y si hubiera que llamar a un supervisor para ir al baño nadie la usaría y el
    /// reporte de caja parada no mediría nada.
    /// </summary>
    public const string SuspenderRequiereAutorizacion = "Caja.SuspenderRequiereAutorizacion";

    /// <summary>Mensaje de bienvenida de la pantalla del cliente. Opcional.</summary>
    public const string MensajeBienvenidaPantalla = "Pantallas.MensajeBienvenida";

    /// <summary>Mensaje de la pantalla del cliente al cobrar. Opcional.</summary>
    public const string MensajeDespedidaPantalla = "Pantallas.MensajeDespedida";

    /// <summary>Segundos que se muestra cada imagen de publicidad. Opcional: sin él no se rota la publicidad.</summary>
    public const string SegundosPorImagenPantalla = "Pantallas.SegundosPorImagen";

    /// <summary>
    /// Segundos que un aviso se queda en la pantalla de la caja antes de quitarse solo. Opcional: sin él valen 6. Con 0 el
    /// aviso se queda hasta que el cajero lo cierre, que es lo que hacía falta cuando el aviso no se debe perder de vista.
    /// </summary>
    public const string SegundosAvisoPantalla = "Pantallas.SegundosAviso";

    /// <summary>Mensaje al pie del ticket de venta (ej. agradecimiento). Opcional: si no existe no se imprime.</summary>
    public const string MensajePieTicket = "Tickets.MensajePie";

    /// <summary>Política impresa en la copia del cliente de la nota de crédito (RF-83, RF-163). Opcional.</summary>
    public const string PoliticaNotaCredito = "Devoluciones.PoliticaNotaCredito";

    /// <summary>Texto impreso en la copia de contabilidad de la nota de crédito (RF-163).</summary>
    public const string PoliticaNotaCreditoContabilidad = "Devoluciones.PoliticaNotaCreditoContabilidad";


    /// <summary>Consulta completa del timbre en la DGII (código QR del ticket); su dirección define el ambiente.</summary>
    public const string UrlConsultaTimbre = "Ecf.UrlConsultaTimbre";

    /// <summary>Consulta simplificada del timbre de las facturas de consumo menores (servicio de facturas de consumo de la DGII).</summary>
    public const string UrlConsultaTimbreConsumo = "Ecf.UrlConsultaTimbreConsumo";


    /// <summary>Permite devolver el dinero en efectivo de la gaveta (RF-123). Opcional: sin él solo queda saldo en la nota.</summary>
    public const string ReembolsoEfectivo = "Devoluciones.ReembolsoEfectivo";

    /// <summary>Monto máximo que se devuelve en efectivo en una devolución.</summary>
    public const string MontoMaximoReembolsoEfectivo = "Devoluciones.MontoMaximoReembolsoEfectivo";

    /// <summary>Permite devolver el dinero a la tarjeta con la que se pagó, por el terminal.</summary>
    public const string ReembolsoTarjeta = "Devoluciones.ReembolsoTarjeta";

    /// <summary>Permite registrar la devolución para pagarla con cheque.</summary>
    public const string ReembolsoCheque = "Devoluciones.ReembolsoCheque";

    /// <summary>Porcentaje restante de una secuencia de e-CF desde el cual se alerta (RF-225). Por defecto 10.</summary>
    public const string PorcentajeAlertaSecuenciaEcf = "Fiscal.PorcentajeAlertaSecuenciaEcf";

    /// <summary>Cuántos comprobantes tienen que quedar para avisar; alerta el que se cumpla primero, este o el porcentaje.</summary>
    public const string ComprobantesAlertaSecuenciaEcf = CatalogoParametros.ComprobantesAlertaSecuenciaEcf;

    /// <summary>Días antes del vencimiento del certificado para alertar (RF-230). Por defecto 30.</summary>
    public const string DiasAlertaCertificado = "Fiscal.DiasAlertaCertificado";

    // Etiquetas de balanza (RF-180)
    public const string BalanzaPrefijoPeso = "Balanza.PrefijoPeso";
    public const string BalanzaPrefijoPrecio = "Balanza.PrefijoPrecio";
    public const string BalanzaDigitosCodigoArticulo = "Balanza.DigitosCodigoArticulo";
    public const string BalanzaDigitosValor = "Balanza.DigitosValor";
    public const string BalanzaDecimalesPeso = "Balanza.DecimalesPeso";
    public const string BalanzaDecimalesPrecio = "Balanza.DecimalesPrecio";
}

public interface IEstadoCaja
{
    /// <summary>Estado de esta terminal: configurada, existente, habilitada y con sucursal activa.</summary>
    Task<DatosEstadoCaja> ObtenerAsync(CancellationToken cancelacion = default);
}
