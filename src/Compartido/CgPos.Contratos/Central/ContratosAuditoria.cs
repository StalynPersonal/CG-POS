namespace CgPos.Contratos.Central;

/// <summary>Un campo que cambió, con lo que tenía antes y lo que quedó después.</summary>
public sealed record DatosCampoAuditoria(string Campo, string? Antes, string? Despues);

/// <summary>Una entidad tocada por la acción, con los campos que cambiaron.</summary>
public sealed record DatosEntidadAuditoria(string Entidad, string Tabla, string? EntidadId, string Operacion,
    IReadOnlyList<DatosCampoAuditoria> Campos);

/// <summary>
/// Fila de la auditoría: quién hizo qué, sobre qué, cuándo y con qué motivo. El antes y el después no viajan aquí, solo
/// su resumen: una publicación de maestros puede tocar cientos de filas y no tiene sentido traerlas todas al listado.
/// </summary>
public sealed record DatosRegistroAuditoria(
    int Id,
    DateTimeOffset OcurridoEn,
    string Accion,
    string TipoEntidad,
    string? EntidadId,
    string? Motivo,
    string? UsuarioNombre,
    string? AutorizadoPorNombre,
    int EntidadesCambiadas,
    int CamposCambiados,
    string? ResumenCambios);

/// <summary>Un movimiento con todo su detalle: lo que muestra la ventana al abrir una fila.</summary>
public sealed record DatosAuditoriaDetalle(
    DatosRegistroAuditoria Registro,
    string? Detalle,
    IReadOnlyList<DatosEntidadAuditoria> Cambios);

/// <param name="Total">Registros que cumplen el filtro, para la paginación.</param>
public sealed record PaginaAuditoria(IReadOnlyList<DatosRegistroAuditoria> Elementos, int Total);

/// <summary>Valores que existen en la auditoría, para llenar los filtros de la pantalla.</summary>
public sealed record OpcionesAuditoria(IReadOnlyList<string> Acciones, IReadOnlyList<string> TiposEntidad, IReadOnlyList<string> Usuarios);
