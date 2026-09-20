using CgPos.Dominio.Comun;

namespace CgPos.Dominio.Turnos;

public enum EstadoTurno
{
    Abierto,
    Cerrado,
}

/// <summary>
/// Turno de una caja (RF-259): puede haber varios por caja y por día, pero solo uno abierto a la vez.
/// Cada venta queda asociada a su turno.
/// </summary>
public sealed class Turno : Entidad
{
    public const int LargoMaximoUsuario = 150;

    private Turno()
    {
    }

    public int CajaId { get; private set; }
    public int SucursalId { get; private set; }

    /// <summary>Número correlativo del turno en la caja.</summary>
    public long Numero { get; private set; }

    /// <summary>Día operativo del turno (hora local de la caja).</summary>
    public DateOnly FechaOperacion { get; private set; }

    public int UsuarioAperturaId { get; private set; }
    public string UsuarioAperturaNombre { get; private set; } = string.Empty;

    /// <summary>Usuario que opera la caja ahora; cambia con el relevo (RF-260) sin cerrar el turno.</summary>
    public int UsuarioActualId { get; private set; }

    public string UsuarioActualNombre { get; private set; } = string.Empty;

    /// <summary>Fondo de caja entregado al abrir; opcional (RF-4), cero si no se usa.</summary>
    public decimal FondoInicial { get; private set; }

    public EstadoTurno Estado { get; private set; }
    public DateTimeOffset AbiertoEn { get; private set; }
    public DateTimeOffset? CerradoEn { get; private set; }

    public static Turno Abrir(int cajaId, int sucursalId, long numero, DateOnly fechaOperacion, int usuarioId, string usuarioNombre,
        decimal fondoInicial, DateTimeOffset ahora)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(fondoInicial);
        ArgumentOutOfRangeException.ThrowIfLessThan(numero, 1);

        var nombre = Validar.Texto(usuarioNombre, "Usuario", LargoMaximoUsuario);
        return new Turno
        {
            CajaId = Validar.Id(cajaId, "Caja"),
            SucursalId = Validar.Id(sucursalId, "Sucursal"),
            Numero = numero,
            FechaOperacion = fechaOperacion,
            UsuarioAperturaId = Validar.Id(usuarioId, "Usuario"),
            UsuarioAperturaNombre = nombre,
            UsuarioActualId = usuarioId,
            UsuarioActualNombre = nombre,
            FondoInicial = decimal.Round(fondoInicial, 2, MidpointRounding.AwayFromZero),
            Estado = EstadoTurno.Abierto,
            AbiertoEn = ahora,
        };
    }

    public bool EstaAbierto => Estado == EstadoTurno.Abierto;

    public void Cerrar(DateTimeOffset ahora)
    {
        if (!EstaAbierto)
            throw new InvalidOperationException("El turno ya está cerrado.");

        Estado = EstadoTurno.Cerrado;
        CerradoEn = ahora;
    }

    /// <summary>Otro usuario toma el turno sin cerrarlo ni cuadrar (RF-260, RF-113). Devuelve el movimiento para el historial.</summary>
    public MovimientoCaja Relevar(int numero, int usuarioId, string usuarioNombre, int? autorizadoPorId, string? autorizadoPorNombre, DateTimeOffset ahora)
    {
        if (!EstaAbierto)
            throw new InvalidOperationException("No se puede relevar un turno cerrado.");
        if (usuarioId == UsuarioActualId)
            throw new InvalidOperationException("El turno ya está a nombre de ese usuario.");

        var movimiento = MovimientoCaja.Relevo(this, numero, usuarioId, usuarioNombre, autorizadoPorId, autorizadoPorNombre, ahora);
        UsuarioActualId = movimiento.UsuarioId;
        UsuarioActualNombre = movimiento.UsuarioNombre;
        return movimiento;
    }
}
