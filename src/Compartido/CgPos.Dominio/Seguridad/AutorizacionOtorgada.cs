using CgPos.Dominio.Comun;

namespace CgPos.Dominio.Seguridad;

/// <summary>
/// Autorización de supervisor concedida para un permiso y un solicitante. Es de un solo uso y vence pronto:
/// la operación que la usa la consume en la misma transacción, así el servidor no confía solo en la pantalla.
/// Su Id es un Guid, no un entero de la base: la pantalla lo presenta como comprobante y no debe poder adivinarse.
/// </summary>
public sealed class AutorizacionOtorgada
{
    public const int LargoMaximoNombre = 150;
    public const int LargoMaximoMotivo = 500;
    public const int LargoMaximoEntidad = 100;
    public const int LargoMaximoEntidadId = 64;

    private AutorizacionOtorgada()
    {
    }

    public Guid Id { get; private init; }

    public string Permiso { get; private set; } = string.Empty;
    public int CajaId { get; private set; }
    public int SolicitanteId { get; private set; }
    public string SolicitanteNombre { get; private set; } = string.Empty;
    public int SupervisorId { get; private set; }
    public string SupervisorNombre { get; private set; } = string.Empty;
    /// <summary>Por qué se autorizó; opcional, porque el supervisor está delante y muchas veces la explicación sobra.</summary>
    public string? Motivo { get; private set; }
    public DateTimeOffset ConcedidaEn { get; private set; }
    public DateTimeOffset VenceEn { get; private set; }
    public DateTimeOffset? UsadaEn { get; private set; }
    public string? UsadaEnTipoEntidad { get; private set; }
    public string? UsadaEnEntidadId { get; private set; }

    public static AutorizacionOtorgada Otorgar(Guid id, string permiso, int cajaId, int solicitanteId, string solicitanteNombre,
        int supervisorId, string supervisorNombre, string? motivo, DateTimeOffset ahora, TimeSpan vigencia)
    {
        if (!CatalogoPermisos.Existe(permiso))
            throw new ArgumentException($"El permiso '{permiso}' no existe en el catálogo.", nameof(permiso));
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(vigencia, TimeSpan.Zero);

        return new AutorizacionOtorgada
        {
            Id = id == Guid.Empty ? throw new ArgumentException("Autorización es obligatorio.", nameof(id)) : id,
            Permiso = permiso,
            CajaId = Validar.Id(cajaId, "Caja"),
            SolicitanteId = Validar.Id(solicitanteId, "Solicitante"),
            SolicitanteNombre = Validar.Texto(solicitanteNombre, "Solicitante", LargoMaximoNombre),
            SupervisorId = Validar.Id(supervisorId, "Supervisor"),
            SupervisorNombre = Validar.Texto(supervisorNombre, "Supervisor", LargoMaximoNombre),
            Motivo = Validar.TextoOpcional(motivo, "Motivo", LargoMaximoMotivo),
            ConcedidaEn = ahora,
            VenceEn = ahora + vigencia,
        };
    }

    public bool PuedeUsarse(string permiso, int solicitanteId, int cajaId, DateTimeOffset ahora) =>
        UsadaEn is null && Permiso == permiso && SolicitanteId == solicitanteId && CajaId == cajaId && ahora <= VenceEn;

    public void MarcarUsada(DateTimeOffset ahora, string tipoEntidad, string? entidadId)
    {
        if (UsadaEn is not null)
            throw new InvalidOperationException("La autorización ya fue usada.");

        UsadaEn = ahora;
        UsadaEnTipoEntidad = Validar.Texto(tipoEntidad, "Tipo de entidad", LargoMaximoEntidad);
        UsadaEnEntidadId = Validar.TextoOpcional(entidadId, "Id de entidad", LargoMaximoEntidadId);
    }
}
