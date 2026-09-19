using CgPos.Contratos.CargaInicial;

namespace CgPos.Contratos.Central;

public sealed record SolicitudIngresoCentral(string Usuario, string Contrasena);

public sealed record SolicitudRenovacionSesion(string TokenRenovacion);

public sealed record SolicitudCambioContrasena(string ContrasenaActual, string ContrasenaNueva);

/// <summary>
/// Resultado del ingreso, la renovación o el cambio de contraseña. El token de acceso es corto; el de renovación se usa una sola vez
/// para obtener un par nuevo.
/// </summary>
public sealed record RespuestaSesionCentral(
    bool Exitoso,
    string? Mensaje = null,
    string? TokenAcceso = null,
    DateTimeOffset? AccesoExpiraEn = null,
    string? TokenRenovacion = null,
    DateTimeOffset? RenovacionExpiraEn = null,
    DatosSesionCentral? Sesion = null,
    DateTimeOffset? BloqueadoHasta = null);

public sealed record DatosSesionCentral(
    int UsuarioId,
    string Codigo,
    string Nombre,
    string RolCodigo,
    string RolNombre,
    IReadOnlyList<string> Permisos,
    bool DebeCambiarContrasena,
    Guid SesionId);

/// <summary>La caja cambia su credencial (el código de su sucursal, el suyo y el secreto) por un token de dispositivo de pocos minutos.</summary>
/// <param name="HuellaEquipo">Identifica al equipo donde corre la caja; el Central comprueba que sea el mismo de siempre.</param>
public sealed record SolicitudTokenDispositivo(string SucursalCodigo, string CajaCodigo, string Secreto, string? HuellaEquipo = null);

public sealed record RespuestaTokenDispositivo(bool Exitoso, string? Mensaje = null, string? Token = null, DateTimeOffset? ExpiraEn = null);

/// <summary>
/// Credencial recién emitida. El secreto solo se entrega en esta respuesta: el Central guarda su hash. En la caja se configuran
/// <c>Caja:Sucursal</c>, <c>Caja:Codigo</c> y <c>Central:Secreto</c>.
/// </summary>
public sealed record DatosCredencialDispositivo(int CajaId, string SucursalCodigo, string CajaCodigo, string Secreto, DateTimeOffset EmitidaEn);

public sealed record SolicitudRevocacionCredencial(string Motivo);

public sealed record SolicitudMotivo(string Motivo);

public enum EstadoEnrolamientoCaja
{
    /// <summary>La solicitud quedó registrada y espera que alguien la acepte en el Central.</summary>
    Pendiente,

    /// <summary>Aceptada: en esta misma respuesta viaja la credencial, y solo en esta.</summary>
    Entregada,

    Rechazada,

    /// <summary>La caja que se pide no existe en el Central, o su credencial ya está atada a otro equipo.</summary>
    NoDisponible,
}

/// <summary>
/// La caja pide entrar al Central por primera vez. No lleva secreto: eso es lo que viene a buscar.
/// </summary>
/// <param name="HuellaEquipo">Identificador del equipo, en hexadecimal; el Central lo ata a la credencial al aceptarla.</param>
/// <param name="Token">Secreto que la caja se inventa y guarda: sin él nadie más puede recoger la credencial aprobada.</param>
public sealed record SolicitudEnrolamientoCaja(string SucursalCodigo, string CajaCodigo, string HuellaEquipo, string NombreEquipo, string Token);

/// <summary>Respuesta a la solicitud. El secreto solo viaja cuando el estado es <see cref="EstadoEnrolamientoCaja.Entregada"/>.</summary>
public sealed record RespuestaEnrolamientoCaja(EstadoEnrolamientoCaja Estado, string? Secreto = null, string? Mensaje = null);

/// <summary>Solicitud de enrolamiento tal como se ve en el Central, para decidir si se acepta.</summary>
public sealed record DatosSolicitudEnrolamiento(
    int Id,
    string SucursalCodigo,
    string CajaCodigo,
    string? CajaNombre,
    string NombreEquipo,
    string HuellaEquipo,
    string? DireccionIp,
    DateTimeOffset SolicitadaEn,
    string Estado,
    DateTimeOffset? ResueltaEn,
    string? ResueltaPor,
    string? Motivo,
    /// <summary>La caja existe en el Central: si no, no hay nada que aceptar hasta crearla.</summary>
    bool CajaExiste,
    /// <summary>La credencial de esa caja ya está atada a otro equipo: hay que liberarlo antes de aceptar.</summary>
    bool CajaConEquipoFijado);

public sealed record DatosDispositivo(int CajaId, string CajaCodigo, string CajaNombre, int SucursalId, string SucursalCodigo);

/// <summary>Nombres de los atributos de los tokens emitidos por el Central.</summary>
public static class AtributosTokenCentral
{
    public const string Tipo = "tipo";
    public const string TipoUsuario = "usuario";
    public const string TipoDispositivo = "dispositivo";

    public const string UsuarioId = "sub";
    public const string Codigo = "codigo";
    public const string Nombre = "name";
    public const string RolId = "rol_id";
    public const string Rol = "rol";
    public const string RolNombre = "rol_nombre";
    public const string Permiso = "permiso";
    public const string Sesion = "sid";
    public const string CambiarContrasena = "cambiar_contrasena";

    public const string Caja = "caja";
    public const string CajaCodigo = "caja_codigo";
    public const string CajaNombre = "caja_nombre";
    public const string Sucursal = "sucursal";
    public const string SucursalCodigo = "sucursal_codigo";
    public const string Credencial = "credencial";
}

/// <summary>
/// Datos iniciales del Central (instalación o desarrollo): organización, parámetros y los usuarios del Central Manager.
/// Se aplica de forma idempotente.
/// </summary>
public sealed record PaqueteCargaCentral(
    EmpresaCarga Empresa,
    IReadOnlyList<SucursalCarga>? Sucursales,
    IReadOnlyList<CajaCarga>? Cajas,
    IReadOnlyList<ParametroCarga>? Parametros,
    IReadOnlyList<RolCentralCarga>? RolesCentral,
    IReadOnlyList<UsuarioCentralCarga>? UsuariosCentral);

/// <param name="Permisos">Códigos del catálogo del Central; <c>"*"</c> concede todos.</param>
public sealed record RolCentralCarga(string Codigo, string Nombre, IReadOnlyList<string>? Permisos, bool Activo = true);

/// <param name="Contrasena">Contraseña inicial en texto (solo al crear el usuario); alternativa a <paramref name="ContrasenaHash"/>.</param>
public sealed record UsuarioCentralCarga(
    string Codigo,
    string Nombre,
    string RolCodigo,
    string? Correo = null,
    string? Contrasena = null,
    string? ContrasenaHash = null,
    bool DebeCambiarContrasena = true,
    bool Activo = true);
