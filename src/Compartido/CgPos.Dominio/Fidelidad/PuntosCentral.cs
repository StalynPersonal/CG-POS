using CgPos.Dominio.Comun;

namespace CgPos.Dominio.Fidelidad;

/// <summary>De dónde salió el movimiento: de una caja al sincronizar o de un ajuste hecho en el Central.</summary>
public enum OrigenMovimientoPuntos
{
    Caja,
    Central,
}

/// <summary>
/// Movimiento de puntos en el saldo oficial del Central (RF-240), del miembro con esa cédula. Los de las cajas se identifican por el documento que
/// los originó y su tipo, así un reenvío no acumula dos veces; los ajustes del Central llevan usuario y motivo.
/// </summary>
public sealed class MovimientoPuntosCentral : Entidad
{
    public const int LargoMaximoDocumento = 40;
    public const int LargoMaximoCedula = 11;
    public const int LargoMaximoUsuario = 150;
    public const int LargoMaximoMotivo = 500;

    private MovimientoPuntosCentral()
    {
    }

    public string Cedula { get; private set; } = string.Empty;
    public TipoMovimientoPuntos Tipo { get; private set; }
    public OrigenMovimientoPuntos Origen { get; private set; }

    /// <summary>Positivo al acumular o ajustar a favor; negativo al canjear, reversar o ajustar en contra.</summary>
    public int Puntos { get; private set; }

    /// <summary>Número de la factura o de la nota de crédito que originó el movimiento; vacío en un ajuste.</summary>
    public string Documento { get; private set; } = string.Empty;
    public int? CajaId { get; private set; }
    public int? SucursalId { get; private set; }
    public DateTimeOffset Fecha { get; private set; }

    /// <summary>Fecha hasta la que valen los puntos acumulados; nula si no vencen (RF-242).</summary>
    public DateOnly? VenceEn { get; private set; }

    public DateTimeOffset RegistradoEn { get; private set; }
    public string? Usuario { get; private set; }
    public string? Motivo { get; private set; }

    /// <summary>Movimiento informado por una caja; el documento y el tipo son su clave de idempotencia.</summary>
    public static MovimientoPuntosCentral DesdeCaja(string cedula, TipoMovimientoPuntos tipo, int puntos, string documento, int cajaId, int sucursalId,
        DateTimeOffset fecha, DateOnly? venceEn, DateTimeOffset ahora)
    {
        if (puntos == 0)
            throw new ArgumentOutOfRangeException(nameof(puntos), puntos, "El movimiento de puntos no puede ser cero.");
        if (!Enum.IsDefined(tipo))
            throw new ArgumentOutOfRangeException(nameof(tipo), tipo, "Tipo de movimiento de puntos no válido.");
        if (tipo == TipoMovimientoPuntos.Acumulacion ? puntos < 0 : puntos > 0)
            throw new ArgumentException($"El signo de los puntos no corresponde a un movimiento de tipo {tipo}.", nameof(puntos));

        return new MovimientoPuntosCentral
        {
            Cedula = MiembroFidelidad.ValidarCedula(cedula),
            Tipo = tipo,
            Origen = OrigenMovimientoPuntos.Caja,
            Puntos = puntos,
            Documento = Validar.Texto(documento, "Documento", LargoMaximoDocumento),
            CajaId = Validar.Id(cajaId, "Caja"),
            SucursalId = Validar.Id(sucursalId, "Sucursal"),
            Fecha = fecha,
            VenceEn = tipo == TipoMovimientoPuntos.Acumulacion ? venceEn : null,
            RegistradoEn = ahora,
        };
    }

    /// <summary>Ajuste manual del Central, a favor o en contra, siempre con motivo y responsable.</summary>
    public static MovimientoPuntosCentral Ajuste(string cedula, int puntos, string usuario, string motivo, DateOnly? venceEn, DateTimeOffset ahora)
    {
        if (puntos == 0)
            throw new ArgumentOutOfRangeException(nameof(puntos), puntos, "El ajuste de puntos no puede ser cero.");

        return new MovimientoPuntosCentral
        {
            Cedula = MiembroFidelidad.ValidarCedula(cedula),
            Tipo = TipoMovimientoPuntos.Ajuste,
            Origen = OrigenMovimientoPuntos.Central,
            Puntos = puntos,
            Documento = string.Empty,
            Fecha = ahora,
            VenceEn = puntos > 0 ? venceEn : null,
            RegistradoEn = ahora,
            Usuario = Validar.Texto(usuario, "Usuario", LargoMaximoUsuario),
            Motivo = Validar.Texto(motivo, "Motivo", LargoMaximoMotivo),
        };
    }
}

/// <summary>
/// Saldo de puntos de un miembro tal como lo calculó el Central a partir de sus movimientos. Es lo que baja a las cajas
/// dentro del maestro del miembro para que puedan canjear sin conexión (RF-240, RF-242).
/// </summary>
/// <param name="Puntos">Disponible hoy, ya sin los puntos vencidos.</param>
/// <param name="PuntosPorVencer">Los que se pierden en <paramref name="ProximoVencimiento"/> si no se canjean.</param>
/// <param name="Vencidos">Los que se perdieron hasta hoy, para explicarle al cliente por qué bajó su saldo.</param>
public sealed record SaldoPuntos(int Puntos, int PuntosPorVencer, DateOnly? ProximoVencimiento, int Vencidos);

/// <summary>Saldo calculado y publicado del miembro; se recalcula con cada movimiento y cuando vencen puntos.</summary>
public sealed class SaldoPuntosCentral
{
    private SaldoPuntosCentral()
    {
    }

    /// <summary>Miembro del programa: es la clave, un saldo por miembro.</summary>
    public int MiembroId { get; private set; }

    public string Cedula { get; private set; } = string.Empty;
    public int Puntos { get; private set; }
    public int PuntosPorVencer { get; private set; }
    public DateOnly? ProximoVencimiento { get; private set; }
    public int Vencidos { get; private set; }
    public DateTimeOffset CalculadoEn { get; private set; }

    public static SaldoPuntosCentral Crear(int miembroId, string cedula) => new()
    {
        MiembroId = Validar.Id(miembroId, "Miembro"),
        Cedula = MiembroFidelidad.ValidarCedula(cedula),
    };

    /// <returns><c>true</c> si el saldo cambió: solo entonces hay que publicar otra vez el maestro del miembro.</returns>
    public bool Aplicar(SaldoPuntos saldo, DateTimeOffset ahora)
    {
        ArgumentNullException.ThrowIfNull(saldo);
        var cambio = Puntos != saldo.Puntos || PuntosPorVencer != saldo.PuntosPorVencer || ProximoVencimiento != saldo.ProximoVencimiento || Vencidos != saldo.Vencidos;
        Puntos = saldo.Puntos;
        PuntosPorVencer = saldo.PuntosPorVencer;
        ProximoVencimiento = saldo.ProximoVencimiento;
        Vencidos = saldo.Vencidos;
        CalculadoEn = ahora;
        return cambio;
    }
}

/// <summary>
/// Saldo oficial de puntos: los movimientos positivos forman lotes con su vencimiento y los negativos consumen primero
/// los que vencen antes, de modo que el cliente no pierda puntos que pudo gastar (RF-240, RF-242).
/// </summary>
public static class SaldoPuntosCalculo
{
    public static SaldoPuntos Calcular(IEnumerable<MovimientoPuntosCentral> movimientos, DateOnly hoy)
    {
        ArgumentNullException.ThrowIfNull(movimientos);

        var lotes = new List<Lote>();
        var descubierto = 0;
        foreach (var movimiento in movimientos.OrderBy(m => m.Fecha).ThenBy(m => m.RegistradoEn))
        {
            if (movimiento.Puntos > 0)
            {
                lotes.Add(new Lote(movimiento.VenceEn, movimiento.Puntos));
                continue;
            }

            // Se gasta primero lo que vence antes; lo que no vence queda para el final.
            var porGastar = -movimiento.Puntos;
            foreach (var lote in lotes.Where(l => l.Restante > 0).OrderBy(l => l.VenceEn ?? DateOnly.MaxValue))
            {
                if (porGastar == 0)
                    break;

                var gastado = Math.Min(lote.Restante, porGastar);
                lote.Restante -= gastado;
                porGastar -= gastado;
            }

            // Una caja sin conexión pudo canjear más de lo que el Central ve todavía: el saldo queda corto hasta que lleguen los demás mensajes.
            descubierto += porGastar;
        }

        var vencidos = lotes.Where(l => l.VenceEn is { } vence && vence < hoy).Sum(l => l.Restante);
        var vivos = lotes.Where(l => l.Restante > 0 && (l.VenceEn is not { } vence || vence >= hoy)).ToList();
        var proximo = vivos.Where(l => l.VenceEn is not null).Select(l => l.VenceEn!.Value).DefaultIfEmpty().Min();
        var proximoVencimiento = proximo == default ? (DateOnly?)null : proximo;

        return new SaldoPuntos(
            vivos.Sum(l => l.Restante) - descubierto,
            proximoVencimiento is { } fecha ? vivos.Where(l => l.VenceEn == fecha).Sum(l => l.Restante) : 0,
            proximoVencimiento,
            vencidos);
    }

    private sealed class Lote(DateOnly? venceEn, int puntos)
    {
        public DateOnly? VenceEn { get; } = venceEn;
        public int Restante { get; set; } = puntos;
    }
}
