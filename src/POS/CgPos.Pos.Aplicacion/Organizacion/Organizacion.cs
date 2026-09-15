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

public static class ParametrosExtensiones
{
    public static async Task<int> ObtenerEnteroAsync(this IParametros parametros, string clave, Guid? cajaId, int predeterminado, CancellationToken cancelacion = default) =>
        int.TryParse(await parametros.ObtenerAsync(clave, cajaId, cancelacion), NumberStyles.Integer, CultureInfo.InvariantCulture, out var valor)
            ? valor
            : predeterminado;

    public static async Task<decimal> ObtenerDecimalAsync(this IParametros parametros, string clave, Guid? cajaId, decimal predeterminado, CancellationToken cancelacion = default) =>
        decimal.TryParse(await parametros.ObtenerAsync(clave, cajaId, cancelacion), NumberStyles.Number, CultureInfo.InvariantCulture, out var valor)
            ? valor
            : predeterminado;

    public static async Task<bool> ObtenerBooleanoAsync(this IParametros parametros, string clave, Guid? cajaId, bool predeterminado, CancellationToken cancelacion = default) =>
        bool.TryParse(await parametros.ObtenerAsync(clave, cajaId, cancelacion), out var valor) ? valor : predeterminado;
}

/// <summary>Claves de parámetros conocidas. Los valores numéricos usan formato invariante (punto decimal).</summary>
public static class ClavesParametros
{
    public const string IntentosMaximosPin = "Seguridad.IntentosMaximosPin";
    public const string MinutosBloqueo = "Seguridad.MinutosBloqueo";
    public const string FondoPredeterminado = "Caja.FondoPredeterminado";

    /// <summary>Total desde el cual la factura de consumo exige cédula o RNC (RF-26, RF-171). Por defecto 250000.</summary>
    public const string MontoIdentificacionConsumo = "Fiscal.MontoIdentificacionConsumo";

    /// <summary>Múltiplo al que se redondea el total cobrado con efectivo, ej. 1 = al peso (RF-216). Por defecto 0 = sin redondeo.</summary>
    public const string PasoRedondeoEfectivo = "Caja.PasoRedondeoEfectivo";

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
