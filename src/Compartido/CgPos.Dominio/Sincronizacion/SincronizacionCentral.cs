using CgPos.Dominio.Comun;
using CgPos.Dominio.Fiscal;

namespace CgPos.Dominio.Sincronizacion;

/// <summary>
/// Documento recibido de una caja tal como lo envió (RF-274). <see cref="MensajeId"/> es el Id del mensaje de la bandeja de salida: la clave de
/// idempotencia (RN-25).
/// La caja es la autoridad sobre sus transacciones (RN-24): el Central lo guarda sin modificarlo.
/// </summary>
public sealed class DocumentoRecibido : Entidad
{
    public const int LargoMaximoTipo = 100;
    public const int LargoHash = 64;
    public const int LargoMaximoReferencia = 40;

    private DocumentoRecibido()
    {
    }

    public Guid MensajeId { get; private set; }
    public int CajaId { get; private set; }
    public int SucursalId { get; private set; }
    public string TipoMensaje { get; private set; } = string.Empty;

    /// <summary>Número del documento de la caja (factura, nota de crédito, pendiente…) o su llave natural (cédula, turno).</summary>
    public string Referencia { get; private set; } = string.Empty;

    public string Contenido { get; private set; } = string.Empty;
    public string HashContenido { get; private set; } = string.Empty;
    public DateTimeOffset CreadoEnCaja { get; private set; }
    public DateTimeOffset RecibidoEn { get; private set; }

    /// <summary>Veces que la caja lo volvió a enviar después de recibirlo (sin duplicarlo).</summary>
    public int Reenvios { get; private set; }

    public DateTimeOffset? UltimoReenvioEn { get; private set; }

    public static DocumentoRecibido Recibir(Guid mensajeId, int cajaId, int sucursalId, string tipoMensaje, string referencia, string contenido, string hashContenido,
        DateTimeOffset creadoEnCaja, DateTimeOffset ahora)
    {
        ArgumentException.ThrowIfNullOrEmpty(contenido);
        if (hashContenido is not { Length: LargoHash })
            throw new ArgumentException($"El hash del contenido debe tener {LargoHash} caracteres.", nameof(hashContenido));

        return new DocumentoRecibido
        {
            MensajeId = mensajeId == Guid.Empty ? throw new ArgumentException("Mensaje es obligatorio.", nameof(mensajeId)) : mensajeId,
            CajaId = Validar.Id(cajaId, "Caja"),
            SucursalId = Validar.Id(sucursalId, "Sucursal"),
            TipoMensaje = Validar.Texto(tipoMensaje, "Tipo de mensaje", LargoMaximoTipo),
            Referencia = Validar.Texto(referencia, "Referencia del documento", LargoMaximoReferencia),
            Contenido = contenido,
            HashContenido = hashContenido.ToUpperInvariant(),
            CreadoEnCaja = creadoEnCaja,
            RecibidoEn = ahora,
        };
    }

    public bool MismoContenido(string hashContenido) => string.Equals(HashContenido, hashContenido, StringComparison.OrdinalIgnoreCase);

    public void RegistrarReenvio(DateTimeOffset ahora)
    {
        Reenvios++;
        UltimoReenvioEn = ahora;
    }
}

public enum EstadoEnvioDgii
{
    Pendiente,
    Enviado,
    Aceptado,
    AceptadoCondicional,
    Rechazado,
}

/// <summary>e-CF firmado en una caja y recibido por el Central, que lo envía a la DGII (RF-222, RN-18).</summary>
public sealed class ComprobanteRecibido : Entidad
{
    private ComprobanteRecibido()
    {
    }

    /// <summary>Documento recibido (mensaje) que trajo el comprobante.</summary>
    public int DocumentoId { get; private set; }

    /// <summary>Número de la factura o de la nota de crédito en la caja.</summary>
    public string Referencia { get; private set; } = string.Empty;

    public int CajaId { get; private set; }
    public int SucursalId { get; private set; }
    public string Encf { get; private set; } = string.Empty;
    public TipoComprobante TipoComprobante { get; private set; }
    public string XmlFirmado { get; private set; } = string.Empty;
    public string HashXml { get; private set; } = string.Empty;
    public DateTimeOffset FechaFirma { get; private set; }
    public DateTimeOffset RecibidoEn { get; private set; }

    /// <summary>El XML recibido es el resumen de consumo (RFCE) y no el e-CF completo: la DGII lo recibe en otro servicio.</summary>
    public bool EsResumenConsumo { get; private set; }
    public EstadoEnvioDgii EstadoDgii { get; private set; }
    public DateTimeOffset? EstadoDgiiEn { get; private set; }
    public string? MensajeDgii { get; private set; }

    /// <summary>Identificador con el que la DGII recibió el e-CF, para consultar su resultado.</summary>
    public string? TrackId { get; private set; }

    /// <summary>Envíos a la DGII intentados, contando los fallidos.</summary>
    public int IntentosEnvio { get; private set; }

    public DateTimeOffset? EnviadoEn { get; private set; }

    /// <summary>Cuándo toca el próximo envío (si está pendiente) o la próxima consulta del resultado (si está enviado); nulo si ya tiene resultado.</summary>
    public DateTimeOffset? ProximoIntentoEn { get; private set; }

    public bool TieneResultado => EstadoDgii is EstadoEnvioDgii.Aceptado or EstadoEnvioDgii.AceptadoCondicional or EstadoEnvioDgii.Rechazado;

    public const int LargoMaximoTrackId = 100;
    public const int LargoMaximoMensajeDgii = 2000;

    /// <summary>La DGII recibió el e-CF y dará su resultado al consultarlo.</summary>
    public void RegistrarEnvio(string trackId, DateTimeOffset ahora, DateTimeOffset proximaConsulta)
    {
        if (EstadoDgii != EstadoEnvioDgii.Pendiente)
            throw new InvalidOperationException($"El e-CF {Encf} ya se envió a la DGII.");
        ArgumentException.ThrowIfNullOrWhiteSpace(trackId);

        TrackId = Recortar(trackId, LargoMaximoTrackId);
        IntentosEnvio++;
        EnviadoEn = ahora;
        EstadoDgii = EstadoEnvioDgii.Enviado;
        EstadoDgiiEn = ahora;
        MensajeDgii = null;
        ProximoIntentoEn = proximaConsulta;
    }

    /// <summary>El envío no llegó a la DGII (comunicación, autenticación, error del servicio): sigue pendiente y se reintenta después.</summary>
    public void RegistrarFalloEnvio(string motivo, DateTimeOffset ahora, DateTimeOffset proximoIntento)
    {
        if (EstadoDgii != EstadoEnvioDgii.Pendiente)
            throw new InvalidOperationException($"El e-CF {Encf} no está pendiente de envío.");

        IntentosEnvio++;
        MensajeDgii = Recortar(motivo, LargoMaximoMensajeDgii);
        EstadoDgiiEn = ahora;
        ProximoIntentoEn = proximoIntento;
    }

    /// <summary>Resultado de la DGII, al enviarlo (respuesta inmediata) o al consultarlo.</summary>
    public void RegistrarResultado(EstadoEnvioDgii estado, string? mensaje, DateTimeOffset ahora, string? trackId = null)
    {
        if (estado is not (EstadoEnvioDgii.Aceptado or EstadoEnvioDgii.AceptadoCondicional or EstadoEnvioDgii.Rechazado))
            throw new ArgumentOutOfRangeException(nameof(estado), estado, "El resultado de la DGII es aceptado, aceptado condicional o rechazado.");
        if (TieneResultado)
            throw new InvalidOperationException($"El e-CF {Encf} ya tiene resultado de la DGII.");

        if (EstadoDgii == EstadoEnvioDgii.Pendiente)
        {
            IntentosEnvio++;
            EnviadoEn = ahora;
        }

        if (!string.IsNullOrWhiteSpace(trackId))
            TrackId ??= Recortar(trackId, LargoMaximoTrackId);
        EstadoDgii = estado;
        EstadoDgiiEn = ahora;
        MensajeDgii = Recortar(mensaje, LargoMaximoMensajeDgii);
        ProximoIntentoEn = null;
    }

    /// <summary>La DGII aún no tiene resultado, o no se pudo consultar: se vuelve a consultar más tarde.</summary>
    public void ProgramarConsulta(DateTimeOffset proximaConsulta, string? mensaje = null)
    {
        if (EstadoDgii != EstadoEnvioDgii.Enviado)
            throw new InvalidOperationException($"El e-CF {Encf} no está esperando resultado de la DGII.");

        ProximoIntentoEn = proximaConsulta;
        if (mensaje is not null)
            MensajeDgii = Recortar(mensaje, LargoMaximoMensajeDgii);
    }

    /// <summary>Un usuario del Central vuelve a poner en cola un e-CF rechazado o que no se ha podido enviar (reenvío dirigido).</summary>
    public void PrepararReenvio(DateTimeOffset ahora)
    {
        if (EstadoDgii is EstadoEnvioDgii.Aceptado or EstadoEnvioDgii.AceptadoCondicional)
            throw new InvalidOperationException($"El e-CF {Encf} ya fue aceptado por la DGII y no se reenvía.");
        if (EstadoDgii == EstadoEnvioDgii.Enviado)
            throw new InvalidOperationException($"El e-CF {Encf} está en proceso en la DGII: espere su resultado.");

        EstadoDgii = EstadoEnvioDgii.Pendiente;
        TrackId = null;
        EstadoDgiiEn = ahora;
        ProximoIntentoEn = ahora;
    }

    private static string? Recortar(string? texto, int largo) =>
        string.IsNullOrWhiteSpace(texto) ? null : texto.Trim() is var limpio && limpio.Length > largo ? limpio[..largo] : texto.Trim();

    /// <param name="esResumenConsumo">El XML es el resumen de una factura de consumo (RFCE): se envía al servicio de consumo de la DGII.</param>
    public static ComprobanteRecibido Registrar(DocumentoRecibido documento, string encf, TipoComprobante tipoComprobante, string xmlFirmado, string hashXml,
        DateTimeOffset fechaFirma, DateTimeOffset ahora, bool esResumenConsumo = false)
    {
        ArgumentNullException.ThrowIfNull(documento);
        ArgumentException.ThrowIfNullOrEmpty(xmlFirmado);
        if (encf is not { Length: DocumentoElectronico.LargoEncf })
            throw new ArgumentException($"El e-NCF debe tener {DocumentoElectronico.LargoEncf} caracteres.", nameof(encf));
        if (hashXml is not { Length: DocumentoRecibido.LargoHash })
            throw new ArgumentException($"El hash del XML debe tener {DocumentoRecibido.LargoHash} caracteres.", nameof(hashXml));

        return new ComprobanteRecibido
        {
            DocumentoId = Validar.Id(documento.Id, "Documento recibido"),
            Referencia = documento.Referencia,
            CajaId = documento.CajaId,
            SucursalId = documento.SucursalId,
            Encf = encf,
            TipoComprobante = tipoComprobante,
            XmlFirmado = xmlFirmado,
            HashXml = hashXml.ToUpperInvariant(),
            FechaFirma = fechaFirma,
            RecibidoEn = ahora,
            EsResumenConsumo = esResumenConsumo,
            EstadoDgii = EstadoEnvioDgii.Pendiente,
        };
    }
}

public enum TipoConflictoSincronizacion
{
    /// <summary>El mensaje dice ser de una caja distinta a la que se autenticó.</summary>
    CajaNoCoincide,

    /// <summary>El contenido no corresponde a su hash: se alteró o llegó incompleto.</summary>
    HashInvalido,

    /// <summary>El Id del mensaje ya se recibió con otro contenido.</summary>
    ContenidoDistinto,

    /// <summary>El XML del e-CF no corresponde a su hash.</summary>
    XmlAlterado,

    /// <summary>El e-NCF ya llegó en otro documento.</summary>
    EncfDuplicado,

    /// <summary>El mensaje está incompleto o su contenido no se puede leer.</summary>
    DocumentoInvalido,

    /// <summary>Una caja inscribió en fidelidad una cédula que el Central ya tiene con otro Id: se conserva la del Central.</summary>
    MiembroDuplicado,

    /// <summary>El número de factura o de nota de crédito ya llegó de otra transacción: la numeración de una caja se repitió.</summary>
    NumeroDuplicado,
}

/// <summary>
/// Conflicto detectado al sincronizar (RF-272, RN-24). Se registra una vez por mensaje y tipo, contando las repeticiones, y queda abierto
/// hasta que un usuario del Central lo resuelve.
/// </summary>
public sealed class ConflictoSincronizacion : Entidad
{
    public const int LargoMaximoDetalle = 2000;
    public const int LargoMaximoNombre = 150;
    public const int LargoMaximoResolucion = 500;

    private ConflictoSincronizacion()
    {
    }

    public int CajaId { get; private set; }
    public int? SucursalId { get; private set; }
    public Guid MensajeId { get; private set; }
    public string TipoMensaje { get; private set; } = string.Empty;
    public TipoConflictoSincronizacion Tipo { get; private set; }
    public string Detalle { get; private set; } = string.Empty;
    public DateTimeOffset DetectadoEn { get; private set; }
    public int Ocurrencias { get; private set; }
    public DateTimeOffset UltimaOcurrenciaEn { get; private set; }
    public DateTimeOffset? ResueltoEn { get; private set; }
    public string? ResueltoPor { get; private set; }
    public string? Resolucion { get; private set; }

    public bool Abierto => ResueltoEn is null;

    public static ConflictoSincronizacion Registrar(int cajaId, int? sucursalId, Guid mensajeId, string? tipoMensaje, TipoConflictoSincronizacion tipo, string detalle,
        DateTimeOffset ahora) =>
        new()
        {
            CajaId = Validar.Id(cajaId, "Caja"),
            SucursalId = sucursalId,
            MensajeId = mensajeId,
            TipoMensaje = Recortar(tipoMensaje, DocumentoRecibido.LargoMaximoTipo) ?? string.Empty,
            Tipo = tipo,
            Detalle = Recortar(detalle, LargoMaximoDetalle) ?? throw new ArgumentException("El detalle es obligatorio.", nameof(detalle)),
            DetectadoEn = ahora,
            Ocurrencias = 1,
            UltimaOcurrenciaEn = ahora,
        };

    public void RegistrarOcurrencia(DateTimeOffset ahora)
    {
        Ocurrencias++;
        UltimaOcurrenciaEn = ahora;
    }

    public void Resolver(DateTimeOffset ahora, string usuario, string resolucion)
    {
        if (!Abierto)
            throw new InvalidOperationException("El conflicto ya está resuelto.");

        ResueltoPor = Validar.Texto(usuario, "Usuario", LargoMaximoNombre);
        Resolucion = Validar.Texto(resolucion, "Resolución", LargoMaximoResolucion);
        ResueltoEn = ahora;
    }

    private static string? Recortar(string? texto, int largo) =>
        string.IsNullOrWhiteSpace(texto) ? null : texto.Length <= largo ? texto : texto[..largo];
}

/// <summary>Estado de la sincronización de una caja visto desde el Central, para el monitor y las alertas de atraso (RF-277, RF-278).</summary>
public sealed class EstadoSincronizacionCaja
{
    public const int LargoMaximoError = 2000;

    private EstadoSincronizacionCaja()
    {
    }

    public int CajaId { get; private set; }
    public DateTimeOffset? UltimaRecepcionEn { get; private set; }
    public long MensajesRecibidos { get; private set; }
    public long Duplicados { get; private set; }
    public long Rechazados { get; private set; }
    public DateTimeOffset? UltimoRechazoEn { get; private set; }
    public string? UltimoError { get; private set; }

    /// <summary>Última vez que la caja pidió maestros.</summary>
    public DateTimeOffset? UltimaDescargaEn { get; private set; }

    /// <summary>Versión de maestros que la caja ya tenía aplicada al pedir la última descarga.</summary>
    public long VersionMaestrosConfirmada { get; private set; }

    /// <summary>Versión hasta la que se le entregaron maestros en la última descarga.</summary>
    public long VersionMaestrosEntregada { get; private set; }

    /// <summary>
    /// El Central quiere que esta caja vuelva a bajar los maestros desde cero. No se le puede ordenar nada a una caja: es
    /// ella la que pregunta, así que queda anotado y se le sirve desde la versión 0 la próxima vez que pida.
    /// </summary>
    public DateTimeOffset? ResincronizacionPedidaEn { get; private set; }

    /// <summary>Hay un pedido de volver a bajarlo todo que la caja aún no ha atendido.</summary>
    public bool ResincronizacionPendiente => ResincronizacionPedidaEn is not null;

    public void PedirResincronizacion(DateTimeOffset ahora) => ResincronizacionPedidaEn = ahora;

    /// <summary>
    /// Se le acaba de servir desde cero: el pedido queda cumplido. Se borra al servirlo y no al terminar la bajada, porque
    /// desde aquí no hay manera de saber que la caja lo aplicó; si algo falla, se vuelve a pedir desde el Central.
    /// </summary>
    public void ResincronizacionServida() => ResincronizacionPedidaEn = null;

    public static EstadoSincronizacionCaja Crear(int cajaId) => new() { CajaId = Validar.Id(cajaId, "Caja") };

    public void RegistrarDescarga(DateTimeOffset ahora, long desde, long hasta)
    {
        UltimaDescargaEn = ahora;
        VersionMaestrosConfirmada = desde;
        VersionMaestrosEntregada = hasta;
    }

    public void RegistrarRecepcion(DateTimeOffset ahora)
    {
        MensajesRecibidos++;
        UltimaRecepcionEn = ahora;
    }

    public void RegistrarDuplicado(DateTimeOffset ahora)
    {
        Duplicados++;
        UltimaRecepcionEn = ahora;
    }

    public void RegistrarRechazo(DateTimeOffset ahora, string error)
    {
        Rechazados++;
        UltimoRechazoEn = ahora;
        UltimoError = error.Length <= LargoMaximoError ? error : error[..LargoMaximoError];
    }
}
