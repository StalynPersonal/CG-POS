using CgPos.Dominio.Comun;

namespace CgPos.Dominio.Auditoria;

/// <summary>Registro inmutable de una acción auditable (quién, qué, sobre qué, cuándo, por qué y quién autorizó).</summary>
public sealed class RegistroAuditoria : Entidad
{
    public const int LargoMaximoAccion = 100;
    public const int LargoMaximoTipoEntidad = 100;
    public const int LargoMaximoEntidadId = 64;
    public const int LargoMaximoMotivo = 500;
    public const int LargoMaximoNombre = 150;

    private RegistroAuditoria()
    {
    }

    public DateTimeOffset OcurridoEn { get; private set; }
    public string Accion { get; private set; } = string.Empty;
    public string TipoEntidad { get; private set; } = string.Empty;
    public string? EntidadId { get; private set; }

    /// <summary>Datos adicionales en JSON.</summary>
    public string? Detalle { get; private set; }

    public string? Motivo { get; private set; }
    public int? UsuarioId { get; private set; }
    public string? UsuarioNombre { get; private set; }
    public int? AutorizadoPorId { get; private set; }
    public string? AutorizadoPorNombre { get; private set; }

    public static RegistroAuditoria Crear(
        DateTimeOffset ocurridoEn,
        string accion,
        string tipoEntidad,
        string? entidadId = null,
        string? detalleJson = null,
        string? motivo = null,
        int? usuarioId = null,
        string? usuarioNombre = null,
        int? autorizadoPorId = null,
        string? autorizadoPorNombre = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accion);
        ArgumentException.ThrowIfNullOrWhiteSpace(tipoEntidad);

        return new RegistroAuditoria
        {
            OcurridoEn = ocurridoEn,
            Accion = accion,
            TipoEntidad = tipoEntidad,
            EntidadId = entidadId,
            Detalle = detalleJson,
            Motivo = motivo,
            UsuarioId = usuarioId,
            UsuarioNombre = usuarioNombre,
            AutorizadoPorId = autorizadoPorId,
            AutorizadoPorNombre = autorizadoPorNombre,
        };
    }
}
