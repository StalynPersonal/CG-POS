using CgPos.Dominio.Comun;

namespace CgPos.Dominio.Turnos;

/// <summary>
/// Por qué se dejó la caja sola: baño, almuerzo, relevo, un llamado del supervisor… Los decide el negocio en el Central,
/// como los motivos de descuento y de devolución.
/// </summary>
/// <remarks>
/// El almuerzo es tiempo previsto y el baño no, así que conviene no mezclarlos: separados, el reporte de caja parada dice
/// algo; juntos, solo dice que la caja estuvo cerrada.
/// </remarks>
public sealed class MotivoSuspension : Entidad
{
    public const int LargoMaximoNombre = 100;

    private MotivoSuspension()
    {
    }

    public int Codigo { get; private set; }
    public string Nombre { get; private set; } = string.Empty;

    /// <summary>Es tiempo previsto (el almuerzo, el receso): en el reporte no cuenta igual que una parada imprevista.</summary>
    public bool Programado { get; private set; }

    /// <summary>Pide escribir en qué consistió; para el motivo «Otro», que si no no explica nada.</summary>
    public bool ExigeNota { get; private set; }

    public bool Activo { get; private set; } = true;

    public static MotivoSuspension Crear(int codigo, string nombre, bool programado = false, bool exigeNota = false)
    {
        var motivo = new MotivoSuspension
        {
            Codigo = Validar.Codigo(codigo, "Código de motivo"),
            Programado = programado,
            ExigeNota = exigeNota,
        };
        motivo.CambiarNombre(nombre);
        return motivo;
    }

    public void CambiarNombre(string nombre) => Nombre = Validar.Texto(nombre, "Nombre de motivo", LargoMaximoNombre);

    public void Actualizar(string nombre, bool programado, bool exigeNota, bool activo)
    {
        CambiarNombre(nombre);
        Programado = programado;
        ExigeNota = exigeNota;
        Activo = activo;
    }
}

/// <summary>
/// Una caja parada: desde cuándo, por qué y hasta cuándo (RF-23). Mientras está abierta, la pantalla está bloqueada y solo
/// la desbloquea quien la suspendió, con su clave; así el tiempo es de verdad de esa persona y de esa caja.
/// </summary>
public sealed class SuspensionCaja : Entidad
{
    public const int LargoMaximoNota = 250;

    private SuspensionCaja()
    {
    }

    public int CajaId { get; private set; }
    public int TurnoId { get; private set; }
    public long TurnoNumero { get; private set; }
    public int UsuarioId { get; private set; }
    public string UsuarioNombre { get; private set; } = string.Empty;

    public int? MotivoCodigo { get; private set; }
    public string MotivoNombre { get; private set; } = string.Empty;

    /// <summary>Tiempo previsto (almuerzo, receso), copiado del motivo: el reporte lo separa de las paradas imprevistas.</summary>
    public bool Programado { get; private set; }

    public string? Nota { get; private set; }
    public DateTimeOffset SuspendidaEn { get; private set; }
    public DateTimeOffset? ReanudadaEn { get; private set; }

    /// <summary>Nadie volvió: la cerró el cierre del turno. Sin esto, una caja suspendida de noche contaría catorce horas.</summary>
    public bool CerradaPorCierreDeTurno { get; private set; }

    public bool Abierta => ReanudadaEn is null;

    /// <summary>Cuánto estuvo parada; mientras sigue abierta, lo que lleva hasta ahora.</summary>
    public TimeSpan Duracion(DateTimeOffset ahora) => (ReanudadaEn ?? ahora) - SuspendidaEn;

    public static SuspensionCaja Registrar(Turno turno, int usuarioId, string usuarioNombre, MotivoSuspension? motivo, string? nota, DateTimeOffset ahora)
    {
        ArgumentNullException.ThrowIfNull(turno);

        return new SuspensionCaja
        {
            CajaId = turno.CajaId,
            TurnoId = turno.Id,
            TurnoNumero = turno.Numero,
            UsuarioId = Validar.Id(usuarioId, "Usuario"),
            UsuarioNombre = Validar.Texto(usuarioNombre, "Usuario", Turno.LargoMaximoUsuario),
            MotivoCodigo = motivo?.Codigo,
            MotivoNombre = motivo?.Nombre ?? "Sin motivo",
            Programado = motivo?.Programado ?? false,
            Nota = Validar.TextoOpcional(nota, "Nota", LargoMaximoNota),
            SuspendidaEn = ahora,
        };
    }

    /// <summary>El cajero volvió y digitó su clave.</summary>
    public void Reanudar(DateTimeOffset ahora)
    {
        if (Abierta)
            ReanudadaEn = ahora;
    }

    /// <summary>La cierra el cierre del turno, para que el reporte no cuente el tiempo en que no había nadie.</summary>
    public void CerrarPorCierreDeTurno(DateTimeOffset ahora)
    {
        if (!Abierta)
            return;

        ReanudadaEn = ahora;
        CerradaPorCierreDeTurno = true;
    }
}
