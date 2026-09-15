namespace CgPos.Pos.Aplicacion.Abstracciones;

/// <summary>
/// Registra acciones auditables dentro de la unidad de trabajo actual.
/// Igual que <see cref="IBandejaSalida"/>, se persiste con el mismo <c>SaveChanges</c> de la operación auditada.
/// </summary>
public interface IAuditoria
{
    void Registrar(EntradaAuditoria entrada);
}

/// <param name="Accion">Acción realizada, ej. "Venta.LineaEliminada", "Caja.GavetaAbierta".</param>
/// <param name="TipoEntidad">Tipo de objeto afectado, ej. "Factura", "Turno".</param>
/// <param name="EntidadId">Identificador del objeto afectado, si aplica.</param>
/// <param name="Detalle">Datos adicionales; se guardan como JSON.</param>
/// <param name="Motivo">Motivo indicado por el usuario (obligatorio en descuentos, anulaciones, devoluciones…).</param>
/// <param name="Usuario">Quien ejecutó la acción.</param>
/// <param name="AutorizadoPor">Supervisor que autorizó, cuando la acción requiere override.</param>
public sealed record EntradaAuditoria(
    string Accion,
    string TipoEntidad,
    string? EntidadId = null,
    object? Detalle = null,
    string? Motivo = null,
    UsuarioAuditoria? Usuario = null,
    UsuarioAuditoria? AutorizadoPor = null);

public sealed record UsuarioAuditoria(Guid Id, string Nombre);
