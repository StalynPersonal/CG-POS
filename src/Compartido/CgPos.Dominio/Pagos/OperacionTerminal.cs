using CgPos.Dominio.Comun;

namespace CgPos.Dominio.Pagos;

public enum TipoOperacionTerminal
{
    Venta,
    Anulacion,
}

public enum EstadoOperacionTerminal
{
    Aprobada,
    Rechazada,
    Anulada,
}

/// <summary>
/// Operación con el terminal de pago (Verifone o pasarela Azul/CardNet). Se guarda apenas responde el terminal, aunque el
/// cobro no termine, para poder anularla (RF-214) y conciliarla contra el lote de la pasarela (RF-215).
/// </summary>
public sealed class OperacionTerminal : Entidad
{
    public const int LargoMaximoAprobacion = 20;
    public const int LargoMaximoMarca = 30;
    public const int LargoMaximoMensaje = 200;

    private OperacionTerminal()
    {
    }

    public int CajaId { get; private set; }
    public int TurnoId { get; private set; }
    public int VentaId { get; private set; }
    public int UsuarioId { get; private set; }
    public TipoOperacionTerminal Tipo { get; private set; }
    public EstadoOperacionTerminal Estado { get; private set; }
    public decimal Monto { get; private set; }
    public string? Aprobacion { get; private set; }
    public string? UltimosDigitos { get; private set; }
    public string? Marca { get; private set; }
    public string? Mensaje { get; private set; }
    public DateTimeOffset Fecha { get; private set; }

    /// <summary>En una anulación, la operación de venta que anuló.</summary>
    public int? OperacionAnuladaId { get; private set; }

    /// <summary>La aprobación ya se aplicó como pago de una venta cobrada.</summary>
    public bool UsadaEnCobro { get; private set; }

    public static OperacionTerminal Registrar(int cajaId, int turnoId, int ventaId, int usuarioId, TipoOperacionTerminal tipo, decimal monto, bool aprobada,
        string? aprobacion, string? ultimosDigitos, string? marca, string? mensaje, DateTimeOffset fecha, int? operacionAnuladaId = null)
    {
        if (monto <= 0)
            throw new ArgumentOutOfRangeException(nameof(monto), monto, "El monto de la operación debe ser mayor que cero.");

        return new OperacionTerminal
        {
            CajaId = Validar.Id(cajaId, "Caja"),
            TurnoId = Validar.Id(turnoId, "Turno"),
            VentaId = Validar.Id(ventaId, "Venta"),
            UsuarioId = Validar.Id(usuarioId, "Usuario"),
            Tipo = tipo,
            Estado = aprobada ? EstadoOperacionTerminal.Aprobada : EstadoOperacionTerminal.Rechazada,
            Monto = decimal.Round(monto, 2, MidpointRounding.AwayFromZero),
            Aprobacion = Validar.TextoOpcional(aprobacion, "Aprobación", LargoMaximoAprobacion),
            UltimosDigitos = Validar.TextoOpcional(ultimosDigitos, "Últimos dígitos", 4),
            Marca = Validar.TextoOpcional(marca, "Marca", LargoMaximoMarca),
            Mensaje = mensaje is null ? null : mensaje.Length > LargoMaximoMensaje ? mensaje[..LargoMaximoMensaje] : mensaje,
            Fecha = fecha,
            OperacionAnuladaId = operacionAnuladaId,
        };
    }

    public bool DisponibleParaCobro => Tipo == TipoOperacionTerminal.Venta && Estado == EstadoOperacionTerminal.Aprobada && !UsadaEnCobro;

    public void MarcarUsada()
    {
        if (!DisponibleParaCobro)
            throw new InvalidOperationException("La operación del terminal no está aprobada o ya se usó.");
        UsadaEnCobro = true;
    }

    public void MarcarAnulada()
    {
        if (Tipo != TipoOperacionTerminal.Venta || Estado != EstadoOperacionTerminal.Aprobada)
            throw new InvalidOperationException("Solo se anula una venta aprobada del terminal.");
        Estado = EstadoOperacionTerminal.Anulada;
    }
}
