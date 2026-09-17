using CgPos.Dominio.Comun;

namespace CgPos.Dominio.Fiscal;

public enum EstadoAnulacionEcf
{
    /// <summary>La DGII registró la anulación: esos e-NCF ya no se pueden usar.</summary>
    Aceptada,

    /// <summary>La DGII la rechazó (ej. un e-NCF del rango ya se había usado).</summary>
    Rechazada,

    /// <summary>No hubo respuesta válida de la DGII; se puede volver a solicitar.</summary>
    Fallida,
}

/// <summary>
/// Anulación de un rango de e-NCF no utilizados de una caja (ANECF), solicitada desde el Central. Queda registrada con quién la pidió, el motivo,
/// la respuesta de la DGII y el XML firmado que se envió.
/// </summary>
public sealed class AnulacionEcfCentral : Entidad
{
    public const int LargoMaximoMotivo = 500;
    public const int LargoMaximoUsuario = 150;
    public const int LargoMaximoRespuesta = 2000;

    private AnulacionEcfCentral()
    {
    }

    public int SecuenciaId { get; private set; }
    public int CajaId { get; private set; }
    public TipoComprobante TipoComprobante { get; private set; }
    public long Desde { get; private set; }
    public long Hasta { get; private set; }
    public long Cantidad => Hasta - Desde + 1;
    public string Motivo { get; private set; } = string.Empty;
    public string UsuarioNombre { get; private set; } = string.Empty;
    public DateTimeOffset SolicitadaEn { get; private set; }
    public EstadoAnulacionEcf Estado { get; private set; }
    public string? RespuestaDgii { get; private set; }
    public string? XmlFirmado { get; private set; }

    public static AnulacionEcfCentral Registrar(int secuenciaId, int cajaId, TipoComprobante tipo, long desde, long hasta, string motivo, string usuarioNombre,
        EstadoAnulacionEcf estado, string? respuestaDgii, string? xmlFirmado, DateTimeOffset ahora)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(desde, 1);
        if (hasta < desde)
            throw new ArgumentException("El final del rango no puede ser menor que el inicio.", nameof(hasta));

        return new AnulacionEcfCentral
        {
            SecuenciaId = Validar.Id(secuenciaId, "Rango de e-CF"),
            CajaId = Validar.Id(cajaId, "Caja"),
            TipoComprobante = tipo,
            Desde = desde,
            Hasta = hasta,
            Motivo = Validar.Texto(motivo, "Motivo", LargoMaximoMotivo),
            UsuarioNombre = Validar.Texto(usuarioNombre, "Usuario", LargoMaximoUsuario),
            SolicitadaEn = ahora,
            Estado = estado,
            RespuestaDgii = respuestaDgii is { Length: > LargoMaximoRespuesta } largo ? largo[..LargoMaximoRespuesta] : respuestaDgii,
            XmlFirmado = xmlFirmado,
        };
    }

    /// <summary>El rango comparte algún e-NCF con otro del mismo tipo.</summary>
    public bool Solapa(TipoComprobante tipo, long desde, long hasta) => TipoComprobante == tipo && desde <= Hasta && hasta >= Desde;
}
