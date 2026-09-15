using CgPos.Dominio.Comun;
using CgPos.Dominio.Fiscal;

namespace CgPos.Dominio.Sincronizacion;

/// <summary>
/// Documento recibido de una caja tal como lo envió (RF-274). Su Id es el del mensaje de la bandeja de salida: la clave de idempotencia (RN-25).
/// La caja es la autoridad sobre sus transacciones (RN-24): el Central lo guarda sin modificarlo.
/// </summary>
public sealed class DocumentoRecibido : Entidad
{
    public const int LargoMaximoTipo = 100;
    public const int LargoHash = 64;

    private DocumentoRecibido()
    {
    }

    public Guid CajaId { get; private set; }
    public Guid SucursalId { get; private set; }
    public string TipoMensaje { get; private set; } = string.Empty;

    /// <summary>Id del documento de la caja (venta, nota de crédito, cierre…).</summary>
    public Guid AgregadoId { get; private set; }

    public string Contenido { get; private set; } = string.Empty;
    public string HashContenido { get; private set; } = string.Empty;
    public DateTimeOffset CreadoEnCaja { get; private set; }
    public DateTimeOffset RecibidoEn { get; private set; }

    /// <summary>Veces que la caja lo volvió a enviar después de recibirlo (sin duplicarlo).</summary>
    public int Reenvios { get; private set; }

    public DateTimeOffset? UltimoReenvioEn { get; private set; }

    public static DocumentoRecibido Recibir(Guid mensajeId, Guid cajaId, Guid sucursalId, string tipoMensaje, Guid agregadoId, string contenido, string hashContenido,
        DateTimeOffset creadoEnCaja, DateTimeOffset ahora)
    {
        ArgumentException.ThrowIfNullOrEmpty(contenido);
        if (hashContenido is not { Length: LargoHash })
            throw new ArgumentException($"El hash del contenido debe tener {LargoHash} caracteres.", nameof(hashContenido));

        return new DocumentoRecibido
        {
            Id = Validar.Id(mensajeId, "Mensaje"),
            CajaId = Validar.Id(cajaId, "Caja"),
            SucursalId = Validar.Id(sucursalId, "Sucursal"),
            TipoMensaje = Validar.Texto(tipoMensaje, "Tipo de mensaje", LargoMaximoTipo),
            AgregadoId = Validar.Id(agregadoId, "Documento"),
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
    public Guid DocumentoId { get; private set; }

    /// <summary>Venta o devolución de la caja.</summary>
    public Guid AgregadoId { get; private set; }

    public Guid CajaId { get; private set; }
    public Guid SucursalId { get; private set; }
    public string Encf { get; private set; } = string.Empty;
    public TipoComprobante TipoComprobante { get; private set; }
    public string XmlFirmado { get; private set; } = string.Empty;
    public string HashXml { get; private set; } = string.Empty;
    public DateTimeOffset FechaFirma { get; private set; }
    public DateTimeOffset RecibidoEn { get; private set; }
    public EstadoEnvioDgii EstadoDgii { get; private set; }
    public DateTimeOffset? EstadoDgiiEn { get; private set; }
    public string? MensajeDgii { get; private set; }

    public static ComprobanteRecibido Registrar(DocumentoRecibido documento, string encf, TipoComprobante tipoComprobante, string xmlFirmado, string hashXml,
        DateTimeOffset fechaFirma, DateTimeOffset ahora)
    {
        ArgumentNullException.ThrowIfNull(documento);
        ArgumentException.ThrowIfNullOrEmpty(xmlFirmado);
        if (encf is not { Length: DocumentoElectronico.LargoEncf })
            throw new ArgumentException($"El e-NCF debe tener {DocumentoElectronico.LargoEncf} caracteres.", nameof(encf));
        if (hashXml is not { Length: DocumentoRecibido.LargoHash })
            throw new ArgumentException($"El hash del XML debe tener {DocumentoRecibido.LargoHash} caracteres.", nameof(hashXml));

        return new ComprobanteRecibido
        {
            DocumentoId = documento.Id,
            AgregadoId = documento.AgregadoId,
            CajaId = documento.CajaId,
            SucursalId = documento.SucursalId,
            Encf = encf,
            TipoComprobante = tipoComprobante,
            XmlFirmado = xmlFirmado,
            HashXml = hashXml.ToUpperInvariant(),
            FechaFirma = fechaFirma,
            RecibidoEn = ahora,
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

    public Guid CajaId { get; private set; }
    public Guid? SucursalId { get; private set; }
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

    public static ConflictoSincronizacion Registrar(Guid cajaId, Guid? sucursalId, Guid mensajeId, string? tipoMensaje, TipoConflictoSincronizacion tipo, string detalle,
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

    public Guid CajaId { get; private set; }
    public DateTimeOffset? UltimaRecepcionEn { get; private set; }
    public long MensajesRecibidos { get; private set; }
    public long Duplicados { get; private set; }
    public long Rechazados { get; private set; }
    public DateTimeOffset? UltimoRechazoEn { get; private set; }
    public string? UltimoError { get; private set; }

    public static EstadoSincronizacionCaja Crear(Guid cajaId) => new() { CajaId = Validar.Id(cajaId, "Caja") };

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
