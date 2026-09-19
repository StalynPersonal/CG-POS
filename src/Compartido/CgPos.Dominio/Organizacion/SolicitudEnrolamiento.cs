using CgPos.Dominio.Comun;

namespace CgPos.Dominio.Organizacion;

public enum EstadoSolicitudEnrolamiento
{
    /// <summary>La caja pidió entrar y espera que alguien la acepte en el Central.</summary>
    Pendiente,

    /// <summary>Aceptada, pero la caja todavía no ha venido a recoger su credencial.</summary>
    Aprobada,

    /// <summary>La caja ya recogió su credencial: la solicitud está cumplida y no sirve otra vez.</summary>
    Entregada,

    Rechazada,
}

/// <summary>
/// Petición de una caja para entrar al Central por primera vez. La caja se anuncia con su sucursal, su código y la huella de su
/// equipo; alguien la acepta en el Central y solo entonces se le entrega una credencial, que nadie escribe a mano.
/// </summary>
/// <remarks>
/// La huella identifica al equipo, pero es un dato que se puede repetir si alguien la observa: por eso la caja guarda además un
/// token secreto propio de la solicitud, del que aquí solo queda el hash. Sin ese token no se recoge la credencial, así que un
/// tercero que conozca la sucursal, la caja y la huella tampoco puede quedarse con ella.
/// </remarks>
public sealed class SolicitudEnrolamiento : Entidad
{
    public const int LargoHuella = 64;
    public const int LargoHashToken = 64;
    public const int LargoMaximoNombreEquipo = 100;
    public const int LargoMaximoIp = 45;
    public const int LargoMaximoNombre = 150;
    public const int LargoMaximoMotivo = 250;

    private SolicitudEnrolamiento()
    {
    }

    public string SucursalCodigo { get; private set; } = string.Empty;
    public string CajaCodigo { get; private set; } = string.Empty;

    /// <summary>La caja del Central a la que corresponde, si existe; nulo cuando alguien pide una caja que no está creada.</summary>
    public int? CajaId { get; private set; }

    public string HuellaEquipo { get; private set; } = string.Empty;
    public string NombreEquipo { get; private set; } = string.Empty;
    public string TokenHash { get; private set; } = string.Empty;
    public string? DireccionIp { get; private set; }
    public DateTimeOffset SolicitadaEn { get; private set; }
    public EstadoSolicitudEnrolamiento Estado { get; private set; } = EstadoSolicitudEnrolamiento.Pendiente;
    public DateTimeOffset? ResueltaEn { get; private set; }

    /// <summary>Usuario del Central que la aceptó o la rechazó.</summary>
    public string? ResueltaPor { get; private set; }

    public string? Motivo { get; private set; }

    public bool EsperaDecision => Estado is EstadoSolicitudEnrolamiento.Pendiente;

    public static SolicitudEnrolamiento Crear(string sucursalCodigo, string cajaCodigo, int? cajaId, string huellaEquipo, string nombreEquipo,
        string tokenHash, string? direccionIp, DateTimeOffset ahora) =>
        new()
        {
            SucursalCodigo = Validar.CodigoDosDigitos(sucursalCodigo, "Código de sucursal"),
            CajaCodigo = Validar.CodigoDosDigitos(cajaCodigo, "Código de caja"),
            CajaId = cajaId is { } id ? Validar.Id(id, "Caja") : null,
            HuellaEquipo = Huella(huellaEquipo),
            NombreEquipo = Validar.Texto(nombreEquipo, "Nombre del equipo", LargoMaximoNombreEquipo),
            TokenHash = Hash(tokenHash, nameof(tokenHash)),
            DireccionIp = Recortar(direccionIp),
            SolicitadaEn = ahora,
        };

    /// <summary>La misma caja vuelve a pedir desde el mismo equipo: se refresca en vez de acumular solicitudes repetidas.</summary>
    public void Repetir(int? cajaId, string nombreEquipo, string tokenHash, string? direccionIp, DateTimeOffset ahora)
    {
        CajaId = cajaId is { } id ? Validar.Id(id, "Caja") : null;
        NombreEquipo = Validar.Texto(nombreEquipo, "Nombre del equipo", LargoMaximoNombreEquipo);
        TokenHash = Hash(tokenHash, nameof(tokenHash));
        DireccionIp = Recortar(direccionIp);
        SolicitadaEn = ahora;
        Estado = EstadoSolicitudEnrolamiento.Pendiente;
        ResueltaEn = null;
        ResueltaPor = null;
        Motivo = null;
    }

    public void Aprobar(int cajaId, DateTimeOffset ahora, string aprobadaPor)
    {
        ExigirPendiente("aprobar");
        CajaId = Validar.Id(cajaId, "Caja");
        Estado = EstadoSolicitudEnrolamiento.Aprobada;
        ResueltaEn = ahora;
        ResueltaPor = Validar.Texto(aprobadaPor, "Aprobada por", LargoMaximoNombre);
    }

    public void Rechazar(DateTimeOffset ahora, string rechazadaPor, string motivo)
    {
        ExigirPendiente("rechazar");
        Estado = EstadoSolicitudEnrolamiento.Rechazada;
        ResueltaEn = ahora;
        ResueltaPor = Validar.Texto(rechazadaPor, "Rechazada por", LargoMaximoNombre);
        Motivo = Validar.Texto(motivo, "Motivo del rechazo", LargoMaximoMotivo);
    }

    /// <summary>La caja recogió su credencial: la solicitud queda cumplida y el token no vuelve a servir.</summary>
    public void MarcarEntregada(DateTimeOffset ahora)
    {
        if (Estado is not EstadoSolicitudEnrolamiento.Aprobada)
            throw new InvalidOperationException("Solo se entrega la credencial de una solicitud aprobada.");

        Estado = EstadoSolicitudEnrolamiento.Entregada;
        ResueltaEn = ahora;
    }

    public bool CoincideToken(string tokenHash) =>
        !string.IsNullOrEmpty(TokenHash) && string.Equals(TokenHash, tokenHash?.Trim().ToUpperInvariant(), StringComparison.Ordinal);

    private void ExigirPendiente(string accion)
    {
        if (Estado is not EstadoSolicitudEnrolamiento.Pendiente)
            throw new InvalidOperationException($"Solo se puede {accion} una solicitud pendiente.");
    }

    private static string Huella(string valor) => Hash(valor, nameof(valor), LargoHuella);

    private static string Hash(string valor, string parametro, int largo = LargoHashToken)
    {
        var limpio = (valor ?? string.Empty).Trim();
        if (limpio.Length != largo || !limpio.All(char.IsAsciiHexDigit))
            throw new ArgumentException($"Debe ser hexadecimal de {largo} caracteres.", parametro);

        return limpio.ToUpperInvariant();
    }

    private static string? Recortar(string? direccionIp) =>
        string.IsNullOrWhiteSpace(direccionIp) ? null
        : direccionIp.Length <= LargoMaximoIp ? direccionIp
        : direccionIp[..LargoMaximoIp];
}
