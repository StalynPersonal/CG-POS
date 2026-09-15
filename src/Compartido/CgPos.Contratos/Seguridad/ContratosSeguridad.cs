namespace CgPos.Contratos.Seguridad;

public sealed record SolicitudIngresoPin(string CodigoUsuario, string Pin);

public sealed record SolicitudIngresoCarne(string CodigoBarras);

public sealed record RespuestaIngreso(
    bool Exitoso,
    string? Mensaje = null,
    string? Token = null,
    DateTimeOffset? ExpiraEn = null,
    DatosSesion? Sesion = null,
    DateTimeOffset? BloqueadoHasta = null);

public sealed record DatosSesion(
    Guid UsuarioId,
    string Codigo,
    string Nombre,
    string RolCodigo,
    string RolNombre,
    int Nivel,
    IReadOnlyList<string> Permisos,
    Guid CajaId,
    string CajaCodigo,
    string CajaNombre,
    Guid SucursalId);

/// <summary>Solicitud de autorización de supervisor: credencial por código + PIN o por carné.</summary>
public sealed record SolicitudAutorizacion(
    string Permiso,
    string Motivo,
    string? CodigoSupervisor = null,
    string? Pin = null,
    string? CodigoBarras = null,
    string? TipoEntidad = null,
    string? EntidadId = null,
    bool ForzarSupervisor = false);

public sealed record RespuestaAutorizacion(
    bool Concedida,
    string? Mensaje = null,
    Guid? AutorizacionId = null,
    Guid? SupervisorId = null,
    string? SupervisorNombre = null,
    bool RequirioSupervisor = true);

public sealed record DatosEstadoCaja(
    bool Configurada,
    bool Habilitada,
    Guid? CajaId = null,
    string? CajaCodigo = null,
    string? CajaNombre = null,
    string? SucursalNombre = null,
    string? EmpresaNombre = null,
    string? Problema = null,
    Catalogo.DatosMoneda? MonedaLocal = null);

/// <summary>Nombres de los atributos del token de sesión de la caja.</summary>
public static class AtributosToken
{
    public const string UsuarioId = "sub";
    public const string Codigo = "codigo";
    public const string Nombre = "name";
    public const string RolId = "rol_id";
    public const string Rol = "rol";
    public const string RolNombre = "rol_nombre";
    public const string Nivel = "nivel";
    public const string Permiso = "permiso";
    public const string Caja = "caja";
    public const string CajaCodigo = "caja_codigo";
    public const string CajaNombre = "caja_nombre";
    public const string Sucursal = "sucursal";
}
