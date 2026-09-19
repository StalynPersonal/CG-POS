using CgPos.Contratos.Central;

namespace CgPos.Central.Aplicacion.Auditoria;

/// <param name="Desde">Primer día que se consulta; si no se indica, no hay límite hacia atrás.</param>
/// <param name="SoloConCambios">Deja fuera las acciones que no modificaron datos (ingresos, consultas).</param>
/// <param name="Buscar">Texto libre: entidad, motivo, usuario o cualquier valor que haya cambiado.</param>
public sealed record FiltroAuditoria(
    DateOnly? Desde = null,
    DateOnly? Hasta = null,
    string? Accion = null,
    string? TipoEntidad = null,
    string? Usuario = null,
    string? Buscar = null,
    bool SoloConCambios = false,
    int Pagina = 0,
    int Tamano = 25);

/// <summary>
/// Auditoría del Central: qué se hizo, quién lo hizo y el antes y el después de cada campo que cambió. Es de solo lectura:
/// los registros no se editan ni se borran desde la aplicación.
/// </summary>
public interface IServicioConsultaAuditoria
{
    public const int TamanoMaximoPagina = 200;

    Task<PaginaAuditoria> ListarAsync(FiltroAuditoria filtro, CancellationToken cancelacion = default);

    /// <summary>Un movimiento con el antes y el después de cada campo; nulo si no existe.</summary>
    Task<DatosAuditoriaDetalle?> ObtenerAsync(int registroId, CancellationToken cancelacion = default);

    /// <summary>Acciones, entidades y usuarios que existen en la auditoría, para llenar los filtros.</summary>
    Task<OpcionesAuditoria> OpcionesAsync(CancellationToken cancelacion = default);
}
