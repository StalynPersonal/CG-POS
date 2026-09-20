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
            return Obligatorio ? $"El parámetro «{Descripcion}» ({Clave}) es obligatorio." : null;
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

    public const string DigitosSecuenciaDocumentos = "Numeracion.DigitosSecuencia";

    /// <summary>Días desde la emisión en que se puede consumir una nota de crédito (RF-39, RF-40); lo leen la caja y el Central al usarla.</summary>
    public const string DiasVigenciaNotaCredito = "Devoluciones.DiasVigenciaNotaCredito";
    /// <summary>Porcentaje de retención de la Ley 32-23 en facturas de régimen especial (E44); 0 = sin retención.</summary>
    public const string PorcentajeRetencionLey3223 = "Fiscal.PorcentajeRetencionLey3223";

    public const string ProximaFactura = "Numeracion.ProximaFactura";
    public const string ProximaNotaCredito = "Numeracion.ProximaNotaCredito";

    private const TipoValorParametro Texto = TipoValorParametro.Texto;
    private const TipoValorParametro Entero = TipoValorParametro.Entero;
    private const TipoValorParametro Decimal = TipoValorParametro.Decimal;
    private const TipoValorParametro Booleano = TipoValorParametro.Booleano;
    private const AlcanceParametro Central = AlcanceParametro.Central;

    public static IReadOnlyList<DefinicionParametro> Todos { get; } =
    [
        new("General.MonedaLocal", "General", "Código ISO de la moneda local de la caja; debe estar publicada en el maestro de monedas", Texto, true),

        new("Seguridad.IntentosMaximosClave", "Seguridad de la caja", "Intentos de clave fallidos seguidos que bloquean al usuario de la caja", Entero, true, Minimo: 1),
        new("Seguridad.MinutosBloqueo", "Seguridad de la caja", "Minutos que dura el bloqueo del usuario de la caja", Entero, true, Minimo: 1),
        new("Seguridad.MinutosVigenciaAutorizacion", "Seguridad de la caja", "Minutos para usar una autorización de supervisor concedida", Entero, true, Minimo: 1),
        new("Seguridad.HorasSesion", "Seguridad de la caja", "Horas que dura la sesión de un usuario en la caja", Decimal, true, Minimo: 0.1m),

        new("Fiscal.MontoIdentificacionConsumo", "Fiscal", "Total desde el cual la factura de consumo exige cédula o RNC", Decimal, true, Minimo: 0),
        new("Fiscal.PorcentajeAlertaSecuenciaEcf", "Fiscal", "Porcentaje restante de un rango de e-CF desde el cual se alerta", Decimal, true, Minimo: 0, Maximo: 100),
        new("Fiscal.DiasAlertaCertificado", "Fiscal", "Días antes del vencimiento del certificado digital para alertar", Entero, true, Minimo: 0),
        new("Fiscal.TipoIngresos", "Fiscal", "Tipo de ingresos de los e-CF según la tabla de la DGII (1 a 6)", Entero, true, Minimo: 1, Maximo: 6),
        new(PorcentajeRetencionLey3223, "Fiscal",
            "Porcentaje de retención de la Ley 32-23 en facturas gubernamentales (E45); un emisor electrónico autorizado está exento, así que normalmente va en 0",
            Decimal, false, Minimo: 0, Maximo: 100),

        new(DigitosSecuenciaDocumentos, "Numeración de documentos",
            "Dígitos de la secuencia en el número de factura, nota de crédito y pendiente (sucursal + caja + tipo + secuencia); se puede aumentar en cualquier momento",
            Entero, true, Minimo: Comun.NumeroDocumento.DigitosMinimosSecuencia, Maximo: Comun.NumeroDocumento.DigitosMaximosSecuencia),
        new(ProximaFactura, "Numeración de documentos",
            "Secuencia mínima de la próxima factura de la caja (para continuar la numeración tras reinstalarla); nunca hace retroceder la numeración",
            Entero, false, Minimo: 1),
        new(ProximaNotaCredito, "Numeración de documentos",
            "Secuencia mínima de la próxima nota de crédito de la caja; nunca hace retroceder la numeración", Entero, false, Minimo: 1),

        new("Caja.PasoRedondeoEfectivo", "Caja y cierre", "Múltiplo al que se redondea el cobro en efectivo (0 = sin redondeo)", Decimal, true, Minimo: 0),
        new("Caja.CierreCiego", "Caja y cierre", "El cajero declara el cierre sin ver lo esperado", Booleano, true),
        new("Caja.FondoEnCuadre", "Caja y cierre", "El fondo de caja forma parte del efectivo esperado en el cierre", Booleano, true),
        new("Caja.FondoPredeterminado", "Caja y cierre", "Fondo sugerido al abrir turno", Decimal, false, Minimo: 0),

        new("Devoluciones.DiasRetencionImpuesto", "Devoluciones", "Días desde la factura tras los cuales la devolución retiene el ITBIS", Entero, true, Minimo: 0),
        new(DiasVigenciaNotaCredito, "Devoluciones",
            "Días desde la emisión en que se puede consumir una nota de crédito; al subirlos, las notas que habían vencido vuelven a poder usarse", Entero, true,
            Minimo: 1),
        new("Devoluciones.PoliticaNotaCredito", "Devoluciones", "Política impresa en la copia del cliente de la nota de crédito", Texto, false),
        new("Devoluciones.PoliticaNotaCreditoContabilidad", "Devoluciones", "Texto impreso en la copia de contabilidad de la nota de crédito", Texto, false),
        new("Ecf.UrlConsultaTimbre", "Facturación electrónica", "Dirección de la consulta del timbre de la DGII que va en el código QR (define el ambiente)", Texto, true),
        new("Ecf.UrlConsultaTimbreConsumo", "Facturación electrónica", "Dirección de la consulta del timbre de las facturas de consumo menores (código QR)", Texto, true),

        new("Devoluciones.ReembolsoEfectivo", "Devoluciones", "Permite devolver el dinero en efectivo de la gaveta", Booleano, false),
        new("Devoluciones.MontoMaximoReembolsoEfectivo", "Devoluciones", "Monto máximo que se devuelve en efectivo por devolución", Decimal, false, Minimo: 0),
        new("Devoluciones.ReembolsoTarjeta", "Devoluciones", "Permite devolver el dinero a la tarjeta con la que se pagó", Booleano, false),
        new("Devoluciones.ReembolsoCheque", "Devoluciones", "Permite registrar la devolución para pagarla con cheque", Booleano, false),

        new("Fidelidad.ValorPunto", "Fidelidad", "Valor en moneda local de cada punto al canjearlo (sin él no se canjean puntos)", Decimal, false, Minimo: 0),
        new("Fidelidad.MesesVigenciaPuntos", "Fidelidad", "Meses de vigencia de los puntos que acumula la caja", Entero, false, Minimo: 1),
        new("Fidelidad.MinimoPuntosCanje", "Fidelidad", "Puntos mínimos por canje", Entero, false, Minimo: 0),
        new("Fidelidad.MaximoPuntosCanjeSinConexion", "Fidelidad", "Puntos máximos por transacción mientras la caja no confirma el saldo con el Central", Entero, false, Minimo: 0),

        new("Entregas.PoliticaPendiente", "Entregas", "Política impresa en el voucher de pendiente de entrega o envío", Texto, false),

        new("Sincronizacion.IntervaloSegundos", "Sincronización y mantenimiento", "Segundos entre ciclos de sincronización de la caja con el Central", Entero, false, Minimo: 5, Maximo: 3600),
        new("Sincronizacion.IntervaloMaestrosSegundos", "Sincronización y mantenimiento", "Segundos entre descargas de maestros del Central", Entero, false, Minimo: 30, Maximo: 86400),
        new("Sincronizacion.TamanoLote", "Sincronización y mantenimiento", "Mensajes que la caja envía al Central por ciclo", Entero, false, Minimo: 1, Maximo: 500),
        new("Sincronizacion.EsperaInicialSegundos", "Sincronización y mantenimiento", "Espera tras el primer fallo de comunicación; se duplica con cada intento", Entero, false, Minimo: 1, Maximo: 3600),
        new("Sincronizacion.EsperaMaximaSegundos", "Sincronización y mantenimiento", "Tope de la espera entre reintentos de sincronización", Entero, false, Minimo: 1, Maximo: 86400),
        new("Sincronizacion.TiempoEsperaSegundos", "Sincronización y mantenimiento", "Segundos que la caja espera una respuesta del Central antes de darla por perdida", Entero, false, Minimo: 1, Maximo: 300),
        new("Mantenimiento.IntervaloMinutos", "Sincronización y mantenimiento", "Minutos entre ciclos de mantenimiento de la caja (respaldo, purga y hora)", Entero, false, Minimo: 1, Maximo: 1440),
        new("Respaldo.Hora", "Sincronización y mantenimiento", "Hora del día (0 a 23) desde la que la caja hace su respaldo diario", Entero, false, Minimo: 0, Maximo: 23),
        new("Reloj.ServidorNtp", "Sincronización y mantenimiento", "Servidor de hora contra el que la caja compara su reloj", Texto, false),
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
        new("Central.Dgii.UrlSemilla", "Envío a la DGII", "Dirección del servicio que entrega la semilla de autenticación", Texto, false, Central),
        new("Central.Dgii.UrlValidarSemilla", "Envío a la DGII", "Dirección del servicio que valida la semilla firmada y entrega el token", Texto, false, Central),
        new("Central.Dgii.UrlRecepcion", "Envío a la DGII", "Dirección del servicio que recibe los e-CF", Texto, false, Central),
        new("Central.Dgii.UrlConsultaResultado", "Envío a la DGII", "Dirección del servicio que consulta el resultado de un e-CF por su trackId", Texto, false, Central),
        new("Central.Dgii.UrlConsultaTrackIds", "Envío a la DGII", "Dirección del servicio que busca los envíos de un e-NCF (recupera el trackId perdido)", Texto, false, Central),
        new("Central.Dgii.UrlRecepcionConsumo", "Envío a la DGII", "Dirección del servicio que recibe los resúmenes de facturas de consumo (RFCE)", Texto, false, Central),
        new("Central.Dgii.UrlConsultaConsumo", "Envío a la DGII", "Dirección del servicio que consulta el resultado de un resumen de factura de consumo", Texto, false, Central),
        new("Central.Dgii.UrlAnulacion", "Envío a la DGII", "Dirección del servicio que anula rangos de e-NCF no utilizados (ANECF)", Texto, false, Central),
        new("Central.Dgii.SegundosCiclo", "Envío a la DGII", "Segundos entre ciclos de envío y consulta de resultados", Entero, true, Central, Minimo: 5),
        new("Central.Dgii.LoteEnvio", "Envío a la DGII", "Máximo de e-CF enviados y de resultados consultados por ciclo", Entero, true, Central, Minimo: 1, Maximo: 1000),
        new("Central.Dgii.MinutosReintento", "Envío a la DGII", "Minutos de espera tras un envío fallido; se duplica en cada fallo seguido", Entero, true, Central, Minimo: 1),
        new("Central.Dgii.MinutosMaximoReintento", "Envío a la DGII", "Espera máxima en minutos entre reintentos de envío", Entero, true, Central, Minimo: 1),
        new("Central.Dgii.SegundosConsultaEstado", "Envío a la DGII", "Segundos entre consultas del resultado de un e-CF ya recibido por la DGII", Entero, true, Central, Minimo: 5),

        new("Central.Monitor.MinutosSinComunicacion", "Monitor de sincronización", "Minutos sin mensajes ni descargas tras los que una caja habilitada es alerta", Entero, true, Central, Minimo: 1),
        new("Central.Monitor.MinutosAlertaDgii", "Monitor de sincronización", "Minutos sin resultado de la DGII tras los que un e-CF es alerta", Entero, true, Central, Minimo: 1),

        new("Central.NotasCredito.MinutosReserva", "Notas de crédito", "Minutos que se retiene el saldo de una nota de crédito mientras una caja cobra", Entero, true, Central, Minimo: 1),
        new("Central.Devoluciones.MinutosReserva", "Devoluciones",
            "Minutos que se retienen las líneas de una factura mientras una caja de otra tienda le emite la nota de crédito",
            Entero, true, Central, Minimo: 1),
        new("Central.Chequeador.Habilitado", "Chequeador de precios",
            "Enciende la página de consulta de precios de la tienda; su consulta no pide sesión, por eso se activa a propósito",
            Booleano, false, Central),
        new("Central.ListasBoda.DescontarCompras", "Listas de boda",
            "Lo que se compra contra una lista de boda se descuenta de las cantidades pedidas; apagado, la compra solo queda en su historial",
            Booleano, false, Central),

        new("Central.Cotizaciones.DiasVigencia", "Cotizaciones",
            "Días que vale una cotización desde que se hace; pasados, solo se factura con autorización de un supervisor", Entero, true, Central, Minimo: 1),
        new("Central.Cotizaciones.Condiciones", "Cotizaciones",
            "Condiciones que se imprimen al pie de la cotización (validez, disponibilidad, forma de pago)", Texto, false, Central),

        new("Central.Fidelidad.MinutosCicloVencimiento", "Fidelidad", "Minutos entre revisiones de los puntos de fidelidad que ya vencieron", Entero, true, Central, Minimo: 1),
        new("Central.Fidelidad.LoteVencimiento", "Fidelidad", "Máximo de miembros cuyo saldo de puntos se recalcula por ciclo de vencimiento", Entero, true, Central, Minimo: 1, Maximo: 10000),

        new("Central.Actualizaciones.CarpetaPaquetes", "Actualización de cajas", "Carpeta del servidor con los paquetes del Agente que descargan las cajas", Texto, false, Central),
        new("Central.Actualizaciones.VersionPublicada", "Actualización de cajas", "Versión del Agente que deben instalar las cajas", Texto, false, Central),

        new("Central.Correo.Servidor", "Correo", "Servidor SMTP de la empresa desde el que el Central envía correos", Texto, false, Central),
        new("Central.Correo.Puerto", "Correo", "Puerto del servidor SMTP", Entero, false, Central, Minimo: 1, Maximo: 65535),
        new("Central.Correo.UsarTls", "Correo", "Cifra la conexión con el servidor de correo", Booleano, false, Central),
        new("Central.Correo.Usuario", "Correo", "Usuario del buzón (la contraseña va en la configuración del servidor)", Texto, false, Central),
        new("Central.Correo.Remitente", "Correo", "Dirección desde la que se envían los correos", Texto, false, Central),
        new("Central.Correo.NombreRemitente", "Correo", "Nombre que ve el cliente como remitente", Texto, false, Central),

        new("Central.Despacho.AvisarPreparado", "Despacho", "Avisa por correo al cliente cuando su pedido queda preparado", Booleano, false, Central),
        new("Central.Despacho.MinutosCicloAvisos", "Despacho", "Minutos entre revisiones de pedidos preparados sin avisar", Entero, true, Central, Minimo: 1),
        new("Central.Despacho.LoteAvisos", "Despacho", "Máximo de avisos al cliente por ciclo", Entero, true, Central, Minimo: 1, Maximo: 1000),
    ];

    private static readonly FrozenDictionary<string, DefinicionParametro> PorClave = Todos.ToFrozenDictionary(d => d.Clave, StringComparer.Ordinal);

    public static DefinicionParametro? Buscar(string? clave) => clave is null ? null : PorClave.GetValueOrDefault(clave.Trim());
}
