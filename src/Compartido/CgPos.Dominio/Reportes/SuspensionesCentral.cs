using CgPos.Dominio.Comun;

namespace CgPos.Dominio.Reportes;

/// <summary>
/// Un rato de caja parada informado por una caja (RF-23), para el reporte de tiempos: quién dejó la caja, por qué y cuánto duró.
/// Se identifica por la caja y el número que le puso la caja, así que un reenvío actualiza la fila en vez de duplicarla.
/// </summary>
public sealed class SuspensionCajaCentral : Entidad
{
    public const int LargoMaximoTexto = 200;
    public const int LargoMaximoNota = 250;

    private SuspensionCajaCentral()
    {
    }

    public int SucursalId { get; private set; }
    public int CajaId { get; private set; }
    public long TurnoNumero { get; private set; }

    /// <summary>El número de la suspensión dentro de esa caja: junto con la caja, es la llave que evita duplicarla.</summary>
    public int Numero { get; private set; }

    public DateOnly FechaOperacion { get; private set; }
    public string UsuarioNombre { get; private set; } = string.Empty;
    public int? MotivoCodigo { get; private set; }
    public string MotivoNombre { get; private set; } = string.Empty;

    /// <summary>Tiempo previsto (almuerzo, receso): el reporte lo separa de las paradas imprevistas.</summary>
    public bool Programado { get; private set; }

    public string? Nota { get; private set; }
    public DateTimeOffset SuspendidaEn { get; private set; }
    public DateTimeOffset ReanudadaEn { get; private set; }

    /// <summary>Nadie volvió: la cerró el cierre del turno.</summary>
    public bool CerradaPorCierreDeTurno { get; private set; }

    public int Segundos { get; private set; }

    public static SuspensionCajaCentral Registrar(int sucursalId, int cajaId, SuspensionInformada suspension)
    {
        var registro = new SuspensionCajaCentral
        {
            SucursalId = Validar.Id(sucursalId, "Sucursal"),
            CajaId = Validar.Id(cajaId, "Caja"),
            Numero = suspension.Numero,
        };
        registro.Actualizar(suspension);
        return registro;
    }

    public void Actualizar(SuspensionInformada suspension)
    {
        ArgumentNullException.ThrowIfNull(suspension);

        TurnoNumero = suspension.TurnoNumero;
        FechaOperacion = suspension.FechaOperacion;
        UsuarioNombre = Validar.TextoOpcional(suspension.UsuarioNombre, "Usuario", LargoMaximoTexto) ?? string.Empty;
        MotivoCodigo = suspension.MotivoCodigo;
        MotivoNombre = Validar.TextoOpcional(suspension.MotivoNombre, "Motivo", LargoMaximoTexto) ?? "Sin motivo";
        Programado = suspension.Programado;
        Nota = Validar.TextoOpcional(suspension.Nota, "Nota", LargoMaximoNota);
        SuspendidaEn = suspension.SuspendidaEn;
        ReanudadaEn = suspension.ReanudadaEn;
        CerradaPorCierreDeTurno = suspension.CerradaPorCierreDeTurno;

        // El tiempo se guarda calculado: el reporte suma minutos, no resta fechas en SQL.
        Segundos = (int)Math.Max(0, Math.Round((suspension.ReanudadaEn - suspension.SuspendidaEn).TotalSeconds));
    }
}

/// <summary>El rato parado tal como lo informó la caja, para guardarlo en el Central.</summary>
public sealed record SuspensionInformada(
    int Numero,
    long TurnoNumero,
    DateOnly FechaOperacion,
    string UsuarioNombre,
    int? MotivoCodigo,
    string MotivoNombre,
    bool Programado,
    string? Nota,
    DateTimeOffset SuspendidaEn,
    DateTimeOffset ReanudadaEn,
    bool CerradaPorCierreDeTurno);
