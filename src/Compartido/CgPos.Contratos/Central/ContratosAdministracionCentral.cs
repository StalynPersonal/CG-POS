namespace CgPos.Contratos.Central;

public sealed record DatosPermisoCentral(string Codigo, string Modulo, string Descripcion);

public sealed record DatosRolCentral(int Id, string Codigo, string Nombre, bool Activo, IReadOnlyList<string> Permisos, int Usuarios);

/// <param name="Codigo">No cambia después de crear el rol.</param>
public sealed record SolicitudRolCentral(string Codigo, string Nombre, IReadOnlyList<string> Permisos);

public sealed record DatosUsuarioCentral(
    int Id,
    string Codigo,
    string Nombre,
    string? Correo,
    int RolId,
    string RolNombre,
    bool Activo,
    DateTimeOffset? BloqueadoHasta,
    bool DebeCambiarContrasena,
    DateTimeOffset? UltimoIngresoEn);

/// <param name="ContrasenaTemporal">El usuario la cambia en su primer ingreso.</param>
public sealed record SolicitudUsuarioCentral(string Codigo, string Nombre, string? Correo, int RolId, string ContrasenaTemporal);

public sealed record SolicitudActualizarUsuarioCentral(string Nombre, string? Correo, int RolId);

public sealed record SolicitudContrasenaTemporal(string ContrasenaTemporal);

/// <summary>Resultado de una operación de administración. Con error, <paramref name="Mensaje"/> explica qué corregir.</summary>
/// <param name="Advertencia">La operación se hizo, pero hay algo que conviene revisar.</param>
public sealed record RespuestaAdministracion(bool Exitosa, string? Mensaje = null, int? Id = null, string? Advertencia = null);
