using CgPos.Dominio.Comun;

namespace CgPos.Dominio.Fiscal;

/// <summary>
/// Rango de e-CF asignado a una caja para un tipo de comprobante. Lo administra el Central (RF-28, RF-225) y la caja lo
/// consume de forma atómica al emitir.
/// </summary>
public sealed class SecuenciaEcf : Entidad
{
    public const long SecuenciaMaxima = 9_999_999_999;

    private SecuenciaEcf()
    {
    }

    public int CajaId { get; private set; }
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

    public static SecuenciaEcf Asignar(int cajaId, TipoComprobante tipo, long desde, long hasta, DateOnly venceEn)
    {
        if (!Enum.IsDefined(tipo))
            throw new ArgumentOutOfRangeException(nameof(tipo), tipo, "Tipo de comprobante no válido.");
        if (desde < 1 || hasta > SecuenciaMaxima || desde > hasta)
            throw new ArgumentException($"El rango {desde}–{hasta} no es válido para e-CF.");

        var secuencia = new SecuenciaEcf
        {
            CajaId = Validar.Id(cajaId, "Caja"),
            TipoComprobante = tipo,
            Desde = desde,
            Ultimo = desde - 1,
        };
        secuencia.Actualizar(hasta, venceEn, activa: true);
        return secuencia;
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

    public static string FormatearEncf(TipoComprobante tipo, long secuencia) => $"E{(int)tipo:00}{secuencia:0000000000}";
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

    public int VentaId { get; private set; }
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

    public static DocumentoElectronico Emitir(int ventaId, int cajaId, TipoComprobante tipo, string encf, DateTimeOffset fechaEmision, DateTimeOffset fechaFirma,
        string codigoSeguridad, decimal montoTotal, string hashXml, string rutaXml, string urlTimbre)
    {
        if (encf is not { Length: LargoEncf })
            throw new ArgumentException($"El eNCF '{encf}' no tiene {LargoEncf} caracteres.", nameof(encf));

        var documento = new DocumentoElectronico
        {
            VentaId = Validar.Id(ventaId, "Venta"),
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
