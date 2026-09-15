using System.Globalization;
using CgPos.Contratos.Seguridad;

namespace CgPos.Pos.Aplicacion.Organizacion;

/// <summary>Identidad de esta terminal: la caja que opera este equipo (configuración <c>Caja:Id</c>).</summary>
public interface IContextoCaja
{
    Guid? CajaId { get; }
}

/// <summary>Lectura de parámetros con precedencia: caja → sucursal de la caja → general.</summary>
public interface IParametros
{
    Task<string?> ObtenerAsync(string clave, Guid? cajaId = null, CancellationToken cancelacion = default);
}

/// <summary>
/// Una regla de negocio sin configurar no se sustituye por un valor fijo en el código: la operación se rechaza con este motivo
/// para que un usuario la configure en el Central.
/// </summary>
public sealed class ParametroNoConfiguradoExcepcion(string clave, string? detalle = null)
    : Exception(detalle is null
        ? $"Falta configurar el parámetro «{clave}». Configúrelo en el Central."
        : $"El parámetro «{clave}» {detalle}. Corríjalo en el Central.")
{
    public string Clave { get; } = clave;
}

public static class ParametrosExtensiones
{
    /// <exception cref="ParametroNoConfiguradoExcepcion">El parámetro no existe o está vacío.</exception>
    public static async Task<string> ObtenerRequeridoAsync(this IParametros parametros, string clave, Guid? cajaId, CancellationToken cancelacion = default) =>
        await parametros.ObtenerAsync(clave, cajaId, cancelacion) is { Length: > 0 } valor ? valor : throw new ParametroNoConfiguradoExcepcion(clave);

    public static async Task<int> ObtenerEnteroAsync(this IParametros parametros, string clave, Guid? cajaId, CancellationToken cancelacion = default) =>
        int.TryParse(await parametros.ObtenerRequeridoAsync(clave, cajaId, cancelacion), NumberStyles.Integer, CultureInfo.InvariantCulture, out var valor)
            ? valor
            : throw new ParametroNoConfiguradoExcepcion(clave, "no es un número entero");

    public static async Task<decimal> ObtenerDecimalAsync(this IParametros parametros, string clave, Guid? cajaId, CancellationToken cancelacion = default) =>
        decimal.TryParse(await parametros.ObtenerRequeridoAsync(clave, cajaId, cancelacion), NumberStyles.Number, CultureInfo.InvariantCulture, out var valor)
            ? valor
            : throw new ParametroNoConfiguradoExcepcion(clave, "no es un número válido (use punto decimal)");

    public static async Task<bool> ObtenerBooleanoAsync(this IParametros parametros, string clave, Guid? cajaId, CancellationToken cancelacion = default) =>
        bool.TryParse(await parametros.ObtenerRequeridoAsync(clave, cajaId, cancelacion), out var valor)
            ? valor
            : throw new ParametroNoConfiguradoExcepcion(clave, "debe ser true o false");

    /// <summary>Para valores que el negocio puede no usar (ej. fondo sugerido): nulo si no está configurado, error si está mal escrito.</summary>
    public static async Task<decimal?> ObtenerDecimalOpcionalAsync(this IParametros parametros, string clave, Guid? cajaId, CancellationToken cancelacion = default) =>
        await parametros.ObtenerAsync(clave, cajaId, cancelacion) is not { Length: > 0 } texto ? null
        : decimal.TryParse(texto, NumberStyles.Number, CultureInfo.InvariantCulture, out var valor) ? valor
        : throw new ParametroNoConfiguradoExcepcion(clave, "no es un número válido (use punto decimal)");
}

/// <summary>Claves de parámetros conocidas. Los valores numéricos usan formato invariante (punto decimal).</summary>
public static class ClavesParametros
{
    /// <summary>Código ISO de la moneda local de la caja; debe existir activa en el maestro de monedas.</summary>
    public const string MonedaLocal = "General.MonedaLocal";

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

    public const string IntentosMaximosPin = "Seguridad.IntentosMaximosPin";
    public const string MinutosBloqueo = "Seguridad.MinutosBloqueo";

    /// <summary>Minutos que tiene el usuario para usar una autorización de supervisor concedida.</summary>
    public const string MinutosVigenciaAutorizacion = "Seguridad.MinutosVigenciaAutorizacion";

    /// <summary>Horas que dura la sesión de un usuario en la caja antes de volver a ingresar.</summary>
    public const string HorasSesion = "Seguridad.HorasSesion";
    public const string FondoPredeterminado = "Caja.FondoPredeterminado";

    /// <summary>Cierre ciego: el cajero declara sin ver lo esperado (RF-9, RN-23). Por defecto true.</summary>
    public const string CierreCiego = "Caja.CierreCiego";

    /// <summary>Si el fondo de caja forma parte del efectivo esperado en el cuadre. Por defecto false: el fondo no se mezcla con el cuadre (RF-4).</summary>
    public const string FondoEnCuadre = "Caja.FondoEnCuadre";

    /// <summary>Total desde el cual la factura de consumo exige cédula o RNC (RF-26, RF-171). Por defecto 250000.</summary>
    public const string MontoIdentificacionConsumo = "Fiscal.MontoIdentificacionConsumo";

    /// <summary>Múltiplo al que se redondea el total cobrado con efectivo, ej. 1 = al peso (RF-216). Por defecto 0 = sin redondeo.</summary>
    public const string PasoRedondeoEfectivo = "Caja.PasoRedondeoEfectivo";

    /// <summary>Días desde la factura tras los cuales la devolución retiene el ITBIS (RF-44). Por defecto 30.</summary>
    public const string DiasRetencionImpuestoDevolucion = "Devoluciones.DiasRetencionImpuesto";

    /// <summary>Meses de vigencia de una nota de crédito para consumirla (RF-39). Por defecto 6.</summary>
    public const string MesesVigenciaNotaCredito = "Devoluciones.MesesVigenciaNotaCredito";

    /// <summary>Tipo de ingresos de los e-CF según la tabla de la DGII (1 a 6, ej. 1 = ingresos por operaciones).</summary>
    public const string TipoIngresos = "Fiscal.TipoIngresos";

    /// <summary>Mensaje de bienvenida de la pantalla del cliente. Opcional.</summary>
    public const string MensajeBienvenidaPantalla = "Pantallas.MensajeBienvenida";

    /// <summary>Mensaje de la pantalla del cliente al cobrar. Opcional.</summary>
    public const string MensajeDespedidaPantalla = "Pantallas.MensajeDespedida";

    /// <summary>Segundos que se muestra cada imagen de publicidad. Opcional: sin él no se rota la publicidad.</summary>
    public const string SegundosPorImagenPantalla = "Pantallas.SegundosPorImagen";

    /// <summary>Mensaje al pie del ticket de venta (ej. agradecimiento). Opcional: si no existe no se imprime.</summary>
    public const string MensajePieTicket = "Tickets.MensajePie";

    /// <summary>Política impresa en la copia del cliente de la nota de crédito (RF-83, RF-163). Opcional.</summary>
    public const string PoliticaNotaCredito = "Devoluciones.PoliticaNotaCredito";

    /// <summary>Texto impreso en la copia de contabilidad de la nota de crédito (RF-163).</summary>
    public const string PoliticaNotaCreditoContabilidad = "Devoluciones.PoliticaNotaCreditoContabilidad";

    /// <summary>Porcentaje restante de una secuencia de e-CF desde el cual se alerta (RF-225). Por defecto 10.</summary>
    public const string PorcentajeAlertaSecuenciaEcf = "Fiscal.PorcentajeAlertaSecuenciaEcf";

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
