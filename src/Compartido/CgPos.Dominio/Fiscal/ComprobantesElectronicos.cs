using CgPos.Dominio.Comun;

namespace CgPos.Dominio.Fiscal;

/// <summary>
/// Rango de e-CF asignado a una caja para un tipo de comprobante. Lo administra el Central (RF-28, RF-225) y la caja lo
/// consume de forma atómica al emitir.
/// </summary>
public sealed class SecuenciaEcf : Entidad
{
    public const long SecuenciaMaxima = 9_999_999_999;

    /// <summary>La que usa hoy la DGII para los comprobantes electrónicos. Se guarda con el rango por si algún día cambia.</summary>
    public const string SeriePredeterminada = "E";

    private SecuenciaEcf()
    {
    }

    public int CajaId { get; private set; }

    /// <summary>Letra con la que empieza el e-NCF de este rango. Va con el rango: lo ya emitido conserva la suya.</summary>
    public string Serie { get; private set; } = SeriePredeterminada;

    public TipoComprobante TipoComprobante { get; private set; }
    public long Desde { get; private set; }
    public long Hasta { get; private set; }

    /// <summary>Última secuencia emitida; <see cref="Desde"/> − 1 si aún no se usa.</summary>
    public long Ultimo { get; private set; }

    public DateOnly VenceEn { get; private set; }
    public bool Activa { get; private set; } = true;

    public long Total => Hasta - Desde + 1;
    public long Restantes => Hasta - Ultimo;
    public decimal PorcentajeRestante => Total == 0 ? 0m : decimal.Round(Restantes * 100m / Total, 2);

    /// <param name="proximo">
    /// Número con el que arranca el consumo. Se indica cuando parte del rango ya se usó fuera del sistema; si se omite,
    /// arranca en <paramref name="desde"/>.
    /// </param>
    public static SecuenciaEcf Asignar(int cajaId, TipoComprobante tipo, long desde, long hasta, DateOnly venceEn, long? proximo = null,
        string? serie = null)
    {
        if (!Enum.IsDefined(tipo))
            throw new ArgumentOutOfRangeException(nameof(tipo), tipo, "Tipo de comprobante no válido.");
        if (desde < 1 || hasta > SecuenciaMaxima || desde > hasta)
            throw new ArgumentException($"El rango {desde}–{hasta} no es válido para e-CF.");
        if (proximo is { } inicio && (inicio < desde || inicio > hasta + 1))
            throw new ArgumentException($"El próximo número ({inicio}) debe estar dentro del rango {desde}–{hasta}.", nameof(proximo));

        var secuencia = new SecuenciaEcf
        {
            Serie = NormalizarSerie(serie),
            CajaId = Validar.Id(cajaId, "Caja"),
            TipoComprobante = tipo,
            Desde = desde,
            Ultimo = (proximo ?? desde) - 1,
        };
        secuencia.Actualizar(hasta, venceEn, activa: true);
        return secuencia;
    }

    /// <summary>
    /// Corrige el número con el que sigue la caja. Sirve cuando la realidad se separa del sistema: se restauró un respaldo
    /// viejo, se emitieron comprobantes fuera del sistema o hubo que saltar un tramo. Adelantar deja esos números sin usar.
    /// </summary>
    /// <exception cref="ArgumentException">Queda fuera del rango.</exception>
    public void CambiarProximo(long proximo)
    {
        if (proximo < Desde || proximo > Hasta + 1)
            throw new ArgumentException($"El próximo número ({proximo}) debe estar entre {Desde} y {Hasta}.", nameof(proximo));

        Ultimo = proximo - 1;
    }

    /// <summary>El Central puede ampliar el rango, cambiar el vencimiento o desactivarlo; nunca retroceder lo ya emitido.</summary>
    public void Actualizar(long hasta, DateOnly venceEn, bool activa)
    {
        if (hasta < Math.Max(Desde, Ultimo) || hasta > SecuenciaMaxima)
            throw new ArgumentException($"El final del rango ({hasta}) no puede ser menor que lo ya emitido ({Ultimo}).", nameof(hasta));

        Hasta = hasta;
        VenceEn = venceEn;
        Activa = activa;
    }

    public bool Disponible(DateOnly hoy) => Activa && Ultimo < Hasta && VenceEn >= hoy;

    /// <summary>El e-NCF completo: serie, tipo de dos dígitos y secuencia de diez, como lo pide la DGII.</summary>
    public static string FormatearEncf(TipoComprobante tipo, long secuencia, string? serie = null) =>
        $"{NormalizarSerie(serie)}{(int)tipo:00}{secuencia:0000000000}";

    /// <summary>El e-NCF de este rango para esa secuencia, con la serie que se le asignó.</summary>
    public string Encf(long secuencia) => FormatearEncf(TipoComprobante, secuencia, Serie);

    /// <exception cref="ArgumentException">La serie no es una sola letra.</exception>
    private static string NormalizarSerie(string? serie)
    {
        var texto = (serie ?? string.Empty).Trim().ToUpperInvariant();
        if (texto.Length == 0)
            return SeriePredeterminada;

        if (texto.Length != 1 || !char.IsAsciiLetterUpper(texto[0]))
            throw new ArgumentException($"La serie «{serie}» debe ser una sola letra.", nameof(serie));

        return texto;
    }
}

/// <summary>Ciclo de estados del e-CF (RF-223). La caja llega hasta Pendiente por sincronizar; el Central continúa.</summary>
public enum EstadoDocumentoElectronico
{
    Emitido,
    PendienteSincronizar,
    Sincronizado,
    EnviadoDgii,
    EnProceso,
    Aceptado,
    AceptadoCondicional,
    Rechazado,
}

/// <summary>
/// Comprobante fiscal electrónico emitido y firmado en la caja (RF-218), con la ruta y el hash de su XML (RF-220) y el
/// historial de estados (RF-223).
/// </summary>
/// <summary>De qué documento salió el comprobante electrónico.</summary>
public enum OrigenComprobante
{
    Venta,
    Devolucion,
}

public sealed class DocumentoElectronico : Entidad
{
    public const int LargoEncf = 13;
    public const int LargoMaximoRuta = 400;
    public const int LargoMaximoUrl = 600;
    public const int LargoMaximoMensaje = 500;

    private readonly List<HistorialEstadoEcf> _historial = [];

    private DocumentoElectronico()
    {
    }

    /// <summary>Id de la venta o de la devolución que originó el comprobante, según <see cref="TipoOrigen"/>.</summary>
    public int VentaId { get; private set; }

    /// <summary>
    /// Si el comprobante salió de una venta o de una devolución. Las dos numeran sus Id por separado, así que sin esto la
    /// devolución 5 y la venta 5 se pisarían en la misma tabla.
    /// </summary>
    public OrigenComprobante TipoOrigen { get; private set; }

    public int CajaId { get; private set; }
    public TipoComprobante TipoComprobante { get; private set; }
    public string Encf { get; private set; } = string.Empty;
    public DateTimeOffset FechaEmision { get; private set; }
    public DateTimeOffset FechaFirma { get; private set; }
    public string CodigoSeguridad { get; private set; } = string.Empty;
    public decimal MontoTotal { get; private set; }

    /// <summary>SHA-256 en hexadecimal del XML firmado, para verificar su integridad al sincronizar.</summary>
    public string HashXml { get; private set; } = string.Empty;

    public string RutaXml { get; private set; } = string.Empty;
    public string UrlTimbre { get; private set; } = string.Empty;
    public EstadoDocumentoElectronico Estado { get; private set; }
    public DateTimeOffset EstadoActualizadoEn { get; private set; }
    public string? MensajeEstado { get; private set; }

    public IReadOnlyCollection<HistorialEstadoEcf> Historial => _historial;

    public static DocumentoElectronico Emitir(int ventaId, OrigenComprobante tipoOrigen, int cajaId, TipoComprobante tipo, string encf,
        DateTimeOffset fechaEmision, DateTimeOffset fechaFirma, string codigoSeguridad, decimal montoTotal, string hashXml, string rutaXml,
        string urlTimbre)
    {
        if (encf is not { Length: LargoEncf })
            throw new ArgumentException($"El eNCF '{encf}' no tiene {LargoEncf} caracteres.", nameof(encf));

        var documento = new DocumentoElectronico
        {
            VentaId = Validar.Id(ventaId, "Documento de origen"),
            TipoOrigen = tipoOrigen,
            CajaId = Validar.Id(cajaId, "Caja"),
            TipoComprobante = tipo,
            Encf = encf,
            FechaEmision = fechaEmision,
            FechaFirma = fechaFirma,
            CodigoSeguridad = Validar.Texto(codigoSeguridad, "Código de seguridad", 20),
            MontoTotal = montoTotal,
            HashXml = Validar.Texto(hashXml, "Hash del XML", 64),
            RutaXml = Validar.Texto(rutaXml, "Ruta del XML", LargoMaximoRuta),
            UrlTimbre = Validar.Texto(urlTimbre, "URL del timbre", LargoMaximoUrl),
        };

        documento.CambiarEstado(EstadoDocumentoElectronico.Emitido, null, fechaFirma);
        documento.CambiarEstado(EstadoDocumentoElectronico.PendienteSincronizar, "Firmado en la caja; pendiente de enviar al Central.", fechaFirma);
        return documento;
    }

    /// <summary>Nueva ubicación del XML, por ejemplo de Pendientes a Enviados cuando el Central confirma su recepción (RF-219, RN-19).</summary>
    public void MoverXml(string rutaXml) => RutaXml = Validar.Texto(rutaXml, "Ruta del XML", LargoMaximoRuta);

    public void CambiarEstado(EstadoDocumentoElectronico estado, string? mensaje, DateTimeOffset ahora)
    {
        var texto = mensaje is { Length: > LargoMaximoMensaje } ? mensaje[..LargoMaximoMensaje] : mensaje;
        Estado = estado;
        EstadoActualizadoEn = ahora;
        MensajeEstado = texto;
        _historial.Add(new HistorialEstadoEcf(Id, estado, ahora, texto));
    }
}

public sealed class HistorialEstadoEcf : Entidad
{
    private HistorialEstadoEcf()
    {
    }

    internal HistorialEstadoEcf(int documentoId, EstadoDocumentoElectronico estado, DateTimeOffset fecha, string? mensaje)
    {
        DocumentoElectronicoId = documentoId;
        Estado = estado;
        Fecha = fecha;
        Mensaje = mensaje;
    }

    public int DocumentoElectronicoId { get; private set; }
    public EstadoDocumentoElectronico Estado { get; private set; }
    public DateTimeOffset Fecha { get; private set; }
    public string? Mensaje { get; private set; }
}
