namespace CgPos.Central.Aplicacion.Abstracciones;

/// <summary>Registra acciones auditables en la unidad de trabajo actual: se guardan con el mismo <c>SaveChanges</c> de la operación.</summary>
public interface IAuditoriaCentral
{
    void Registrar(EntradaAuditoria entrada);
}

/// <param name="Accion">Acción realizada, ej. "Seguridad.IngresoExitoso".</param>
/// <param name="TipoEntidad">Tipo de objeto afectado, ej. "UsuarioCentral", "Caja".</param>
/// <param name="Detalle">Datos adicionales; se guardan como JSON.</param>
public sealed record EntradaAuditoria(
    string Accion,
    string TipoEntidad,
    string? EntidadId = null,
    object? Detalle = null,
    string? Motivo = null,
    UsuarioAuditoria? Usuario = null);

public sealed record UsuarioAuditoria(Guid Id, string Nombre);

/// <summary>Hash de las contraseñas del Central Manager.</summary>
public interface IHashContrasenas
{
    string Hash(string contrasena);

    bool Verificar(string contrasena, string hash);

    bool EsHashReconocido(string? hash);
}

/// <summary>Origen de una solicitud: se guarda en la sesión y en la auditoría.</summary>
public sealed record OrigenSolicitud(string? DireccionIp, string? AgenteUsuario);
