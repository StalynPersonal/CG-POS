using System.Collections.Frozen;
using System.Globalization;

namespace CgPos.Dominio.Organizacion;

public enum TipoValorParametro
{
    Texto,
    Entero,
    Decimal,
    Booleano,
}

public enum AlcanceParametro
{
    /// <summary>Regla de las cajas: general, de una sucursal o de una caja; baja a las cajas.</summary>
    Caja,

    /// <summary>Regla del propio Central: solo general y nunca baja a las cajas.</summary>
    Central,
}

/// <summary>Parámetro que un usuario configura en el Central: qué regula, qué tipo de valor admite y si es obligatorio.</summary>
public sealed record DefinicionParametro(
    string Clave,
    string Modulo,
    string Descripcion,
    TipoValorParametro Tipo,
    bool Obligatorio,
    AlcanceParametro Alcance = AlcanceParametro.Caja,
    decimal? Minimo = null,
    decimal? Maximo = null)
{
    /// <returns>El motivo por el que el valor no sirve, o <c>null</c> si es válido. Vacío significa «sin configurar».</returns>
    public string? ValidarValor(string? valor)
    {
        var texto = valor?.Trim() ?? string.Empty;
        if (texto.Length == 0)
            return Obligatorio ? $"El parámetro «{Clave}» es obligatorio." : null;
        if (texto.Length > Parametro.LargoMaximoValor)
            return $"El valor no puede exceder {Parametro.LargoMaximoValor} caracteres.";

        decimal numero;
        switch (Tipo)
        {
            case TipoValorParametro.Booleano:
                return bool.TryParse(texto, out _) ? null : "El valor debe ser true o false.";
            case TipoValorParametro.Entero:
                if (!int.TryParse(texto, NumberStyles.Integer, CultureInfo.InvariantCulture, out var entero))
                    return "El valor debe ser un número entero.";
                numero = entero;
                break;
            case TipoValorParametro.Decimal:
                if (!decimal.TryParse(texto, NumberStyles.Number, CultureInfo.InvariantCulture, out numero))
                    return "El valor debe ser un número (use punto decimal).";
                break;
            default:
                return null;
        }

        if (Minimo is { } minimo && numero < minimo)
            return $"El valor no puede ser menor que {minimo.ToString(CultureInfo.InvariantCulture)}.";
        if (Maximo is { } maximo && numero > maximo)
            return $"El valor no puede ser mayor que {maximo.ToString(CultureInfo.InvariantCulture)}.";

        return null;
    }
}

/// <summary>
/// Todos los parámetros que leen las cajas y el Central. Las reglas de negocio no tienen valores fijos en el código: un usuario las configura en el
/// Central y el catálogo valida que el valor sirva antes de guardarlo. Las pruebas verifican que toda clave leída esté aquí.
/// </summary>
public static class CatalogoParametros
{
    public const string MonedaLocal = "General.MonedaLocal";

    private const TipoValorParametro Texto = TipoValorParametro.Texto;
    private const TipoValorParametro Entero = TipoValorParametro.Entero;
    private const TipoValorParametro Decimal = TipoValorParametro.Decimal;
    private const TipoValorParametro Booleano = TipoValorParametro.Booleano;
    private const AlcanceParametro Central = AlcanceParametro.Central;

    public static IReadOnlyList<DefinicionParametro> Todos { get; } =
    [
        new("General.MonedaLocal", "General", "Código ISO de la moneda local de la caja; debe estar publicada en el maestro de monedas", Texto, true),

        new("Seguridad.IntentosMaximosPin", "Seguridad de la caja", "Intentos de PIN fallidos seguidos que bloquean al usuario de la caja", Entero, true, Minimo: 1),
        new("Seguridad.MinutosBloqueo", "Seguridad de la caja", "Minutos que dura el bloqueo del usuario de la caja", Entero, true, Minimo: 1),
        new("Seguridad.MinutosVigenciaAutorizacion", "Seguridad de la caja", "Minutos para usar una autorización de supervisor concedida", Entero, true, Minimo: 1),
        new("Seguridad.HorasSesion", "Seguridad de la caja", "Horas que dura la sesión de un usuario en la caja", Decimal, true, Minimo: 0.1m),

        new("Fiscal.MontoIdentificacionConsumo", "Fiscal", "Total desde el cual la factura de consumo exige cédula o RNC", Decimal, true, Minimo: 0),
        new("Fiscal.PorcentajeAlertaSecuenciaEcf", "Fiscal", "Porcentaje restante de un rango de e-CF desde el cual se alerta", Decimal, true, Minimo: 0, Maximo: 100),
        new("Fiscal.DiasAlertaCertificado", "Fiscal", "Días antes del vencimiento del certificado digital para alertar", Entero, true, Minimo: 0),
        new("Fiscal.TipoIngresos", "Fiscal", "Tipo de ingresos de los e-CF según la tabla de la DGII (1 a 6)", Entero, true, Minimo: 1, Maximo: 6),

        new("Caja.PasoRedondeoEfectivo", "Caja y cierre", "Múltiplo al que se redondea el cobro en efectivo (0 = sin redondeo)", Decimal, true, Minimo: 0),
        new("Caja.CierreCiego", "Caja y cierre", "El cajero declara el cierre sin ver lo esperado", Booleano, true),
        new("Caja.FondoEnCuadre", "Caja y cierre", "El fondo de caja forma parte del efectivo esperado en el cierre", Booleano, true),
        new("Caja.FondoPredeterminado", "Caja y cierre", "Fondo sugerido al abrir turno", Decimal, false, Minimo: 0),

        new("Devoluciones.DiasRetencionImpuesto", "Devoluciones", "Días desde la factura tras los cuales la devolución retiene el ITBIS", Entero, true, Minimo: 0),
        new("Devoluciones.MesesVigenciaNotaCredito", "Devoluciones", "Meses de vigencia de la nota de crédito para consumirla", Entero, true, Minimo: 1),
        new("Devoluciones.PoliticaNotaCredito", "Devoluciones", "Política impresa en la copia del cliente de la nota de crédito", Texto, false),
        new("Devoluciones.PoliticaNotaCreditoContabilidad", "Devoluciones", "Texto impreso en la copia de contabilidad de la nota de crédito", Texto, false),

        new("Fidelidad.ValorPunto", "Fidelidad", "Valor en moneda local de cada punto al canjearlo (sin él no se canjean puntos)", Decimal, false, Minimo: 0),
        new("Fidelidad.MesesVigenciaPuntos", "Fidelidad", "Meses de vigencia de los puntos que acumula la caja", Entero, false, Minimo: 1),
        new("Fidelidad.MinimoPuntosCanje", "Fidelidad", "Puntos mínimos por canje", Entero, false, Minimo: 0),
        new("Fidelidad.MaximoPuntosCanjeSinConexion", "Fidelidad", "Puntos máximos por transacción mientras la caja no confirma el saldo con el Central", Entero, false, Minimo: 0),

        new("Entregas.PoliticaPendiente", "Entregas", "Política impresa en el voucher de pendiente de entrega o envío", Texto, false),

        new("Sincronizacion.DiasRetencionXmlEnviados", "Sincronización y mantenimiento", "Días que la caja conserva los XML ya confirmados por el Central", Entero, false, Minimo: 1),
        new("Sincronizacion.DiasRetencionMensajesConfirmados", "Sincronización y mantenimiento", "Días que la caja conserva los mensajes ya confirmados de la bandeja de salida", Entero, false, Minimo: 1),
        new("Sincronizacion.AlertaTamanoBaseDatosMb", "Sincronización y mantenimiento", "Tamaño de la base de la caja (MB) desde el que se alerta", Entero, false, Minimo: 1),
        new("Sincronizacion.HorasAlertaPendientes", "Sincronización y mantenimiento", "Horas sin sincronizar un documento desde las que se alerta", Decimal, false, Minimo: 0),
        new("Respaldo.DiasRetencion", "Sincronización y mantenimiento", "Días que se conservan los respaldos de la base de la caja", Entero, false, Minimo: 1),
        new("Reloj.ToleranciaSegundos", "Sincronización y mantenimiento", "Segundos de diferencia con el servidor de hora desde los que se alerta", Entero, false, Minimo: 1),

        new("Tickets.MensajePie", "Tickets y pantallas", "Mensaje al pie del ticket de venta", Texto, false),
        new("Pantallas.MensajeBienvenida", "Tickets y pantallas", "Mensaje de bienvenida de la pantalla del cliente", Texto, false),
        new("Pantallas.MensajeDespedida", "Tickets y pantallas", "Mensaje de la pantalla del cliente al cobrar", Texto, false),
        new("Pantallas.SegundosPorImagen", "Tickets y pantallas", "Segundos que se muestra cada imagen de publicidad (sin él no rota)", Entero, false, Minimo: 1),

        new("Balanza.PrefijoPeso", "Balanza", "Prefijo de las etiquetas de balanza con peso", Texto, false),
        new("Balanza.PrefijoPrecio", "Balanza", "Prefijo de las etiquetas de balanza con precio", Texto, false),
        new("Balanza.DigitosCodigoArticulo", "Balanza", "Dígitos del código de artículo en la etiqueta de balanza", Entero, false, Minimo: 1),
        new("Balanza.DigitosValor", "Balanza", "Dígitos del peso o precio en la etiqueta de balanza", Entero, false, Minimo: 1),
        new("Balanza.DecimalesPeso", "Balanza", "Decimales del peso en la etiqueta de balanza", Entero, false, Minimo: 0),
        new("Balanza.DecimalesPrecio", "Balanza", "Decimales del precio en la etiqueta de balanza", Entero, false, Minimo: 0),

        new("Central.Seguridad.IntentosMaximos", "Seguridad del Central", "Intentos de contraseña fallidos que bloquean al usuario del Central", Entero, true, Central, Minimo: 1),
        new("Central.Seguridad.MinutosBloqueo", "Seguridad del Central", "Minutos de bloqueo del usuario del Central por intentos fallidos", Entero, true, Central, Minimo: 1),
        new("Central.Seguridad.MinutosToken", "Seguridad del Central", "Minutos de vigencia del token de acceso del Central Manager", Entero, true, Central, Minimo: 1),
        new("Central.Seguridad.MinutosInactividad", "Seguridad del Central", "Minutos sin actividad tras los que vence la sesión del Central Manager", Entero, true, Central, Minimo: 1),
        new("Central.Seguridad.HorasSesion", "Seguridad del Central", "Horas máximas de una sesión del Central Manager", Entero, true, Central, Minimo: 1),
        new("Central.Seguridad.LargoMinimoContrasena", "Seguridad del Central", "Largo mínimo de las contraseñas del Central", Entero, true, Central, Minimo: 1),
        new("Central.Seguridad.ContrasenaCompleja", "Seguridad del Central", "Exige mayúsculas, minúsculas, números y símbolos, sin contener el usuario", Booleano, false, Central),
        new("Central.Dispositivos.MinutosToken", "Seguridad del Central", "Minutos de vigencia del token con el que las cajas se comunican con el Central", Entero, true, Central, Minimo: 1),

        new("Central.Dgii.Habilitado", "Envío a la DGII", "Envía a la DGII los e-CF recibidos de las cajas", Booleano, false, Central),
        new("Central.Dgii.UrlBase", "Envío a la DGII", "Dirección base (https) de los servicios de e-CF de la DGII según el ambiente", Texto, false, Central),
        new("Central.Dgii.SegundosCiclo", "Envío a la DGII", "Segundos entre ciclos de envío y consulta de resultados", Entero, true, Central, Minimo: 5),
        new("Central.Dgii.LoteEnvio", "Envío a la DGII", "Máximo de e-CF enviados y de resultados consultados por ciclo", Entero, true, Central, Minimo: 1, Maximo: 1000),
        new("Central.Dgii.MinutosReintento", "Envío a la DGII", "Minutos de espera tras un envío fallido; se duplica en cada fallo seguido", Entero, true, Central, Minimo: 1),
        new("Central.Dgii.MinutosMaximoReintento", "Envío a la DGII", "Espera máxima en minutos entre reintentos de envío", Entero, true, Central, Minimo: 1),
        new("Central.Dgii.SegundosConsultaEstado", "Envío a la DGII", "Segundos entre consultas del resultado de un e-CF ya recibido por la DGII", Entero, true, Central, Minimo: 5),

        new("Central.Monitor.MinutosSinComunicacion", "Monitor de sincronización", "Minutos sin mensajes ni descargas tras los que una caja habilitada es alerta", Entero, true, Central, Minimo: 1),
        new("Central.Monitor.MinutosAlertaDgii", "Monitor de sincronización", "Minutos sin resultado de la DGII tras los que un e-CF es alerta", Entero, true, Central, Minimo: 1),
    ];

    private static readonly FrozenDictionary<string, DefinicionParametro> PorClave = Todos.ToFrozenDictionary(d => d.Clave, StringComparer.Ordinal);

    public static DefinicionParametro? Buscar(string? clave) => clave is null ? null : PorClave.GetValueOrDefault(clave.Trim());
}
