namespace CgPos.Contratos.Central;

public sealed record DatosPermisoCentral(string Codigo, string Modulo, string Descripcion);

public sealed record DatosRolCentral(Guid Id, string Codigo, string Nombre, bool Activo, IReadOnlyList<string> Permisos, int Usuarios);

/// <param name="Codigo">No cambia después de crear el rol.</param>
public sealed record SolicitudRolCentral(string Codigo, string Nombre, IReadOnlyList<string> Permisos);

public sealed record DatosUsuarioCentral(
    Guid Id,
    string Codigo,
    string Nombre,
    string? Correo,
    Guid RolId,
    string RolNombre,
    bool Activo,
    DateTimeOffset? BloqueadoHasta,
    bool DebeCambiarContrasena,
    DateTimeOffset? UltimoIngresoEn);

/// <param name="ContrasenaTemporal">El usuario la cambia en su primer ingreso.</param>
public sealed record SolicitudUsuarioCentral(string Codigo, string Nombre, string? Correo, Guid RolId, string ContrasenaTemporal);

public sealed record SolicitudActualizarUsuarioCentral(string Nombre, string? Correo, Guid RolId);

public sealed record SolicitudContrasenaTemporal(string ContrasenaTemporal);

/// <summary>Resultado de una operación de administración. Con error, <paramref name="Mensaje"/> explica qué corregir.</summary>
public sealed record RespuestaAdministracion(bool Exitosa, string? Mensaje = null, Guid? Id = null);
