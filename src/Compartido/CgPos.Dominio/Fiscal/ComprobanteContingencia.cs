using CgPos.Dominio.Comun;

namespace CgPos.Dominio.Fiscal;

/// <summary>
/// Venta cobrada sin e-CF porque la caja no pudo firmarlo (certificado sin cargar o vencido, o secuencia agotada). Se entrega un
/// comprobante provisional numerado y la venta queda en cola: el e-CF se emite y firma en cuanto se restablece lo que faltaba,
/// con la fecha real del cobro. Mientras haya contingencias abiertas, la caja lo alerta.
/// </summary>
public sealed class ComprobanteContingencia : Entidad
{
    public const int LargoMaximoNumero = 30;
    public const int LargoMaximoMotivo = 300;

    private ComprobanteContingencia()
    {
    }

    public int VentaId { get; private set; }
    public string VentaNumero { get; private set; } = string.Empty;
    public int CajaId { get; private set; }
    public int? TurnoId { get; private set; }

    /// <summary>Número del comprobante provisional que se le entrega al cliente.</summary>
    public string Numero { get; private set; } = string.Empty;

    public TipoComprobante TipoComprobante { get; private set; }
    public decimal Total { get; private set; }

    /// <summary>Por qué no se pudo emitir el e-CF, tal como lo explicó la emisión.</summary>
    public string Motivo { get; private set; } = string.Empty;

    public DateTimeOffset CreadoEn { get; private set; }

    /// <summary>Cuándo se emitió por fin el e-CF; nulo mientras siga pendiente.</summary>
    public DateTimeOffset? RegularizadoEn { get; private set; }

    public string? Encf { get; private set; }

    /// <summary>Intentos fallidos de emitir el e-CF después del cobro.</summary>
    public int Intentos { get; private set; }

    public string? UltimoError { get; private set; }

    public bool EstaPendiente => RegularizadoEn is null;

    public static ComprobanteContingencia Registrar(int ventaId, string ventaNumero, int cajaId, int? turnoId, string numero, TipoComprobante tipo,
        decimal total, string motivo, DateTimeOffset ahora) =>
        new()
        {
            VentaId = Validar.Id(ventaId, "Venta"),
            VentaNumero = Validar.Texto(ventaNumero, "Número de la venta", LargoMaximoNumero),
            CajaId = Validar.Id(cajaId, "Caja"),
            TurnoId = turnoId,
            Numero = Validar.Texto(numero, "Número del comprobante provisional", LargoMaximoNumero),
            TipoComprobante = tipo,
            Total = total,
            Motivo = Validar.Texto(motivo, "Motivo", LargoMaximoMotivo),
            CreadoEn = ahora,
        };

    /// <summary>El e-CF se emitió: la venta queda regularizada y deja de alertar.</summary>
    public void Regularizar(string encf, DateTimeOffset ahora)
    {
        Encf = Validar.Texto(encf, "e-NCF", DocumentoElectronico.LargoEncf);
        RegularizadoEn = ahora;
        UltimoError = null;
    }

    /// <summary>Sigue sin poder emitirse: se registra para verlo en la caja y en el Central.</summary>
    public void RegistrarIntento(string? error)
    {
        Intentos++;
        UltimoError = error is { Length: > LargoMaximoMotivo } largo ? largo[..LargoMaximoMotivo] : error;
    }
}
