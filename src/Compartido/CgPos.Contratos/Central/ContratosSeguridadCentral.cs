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
/// <param name="DireccionIp">Dirección de red configurada en la caja; el Central comprueba que sea la registrada para ella.</param>
public sealed record SolicitudTokenDispositivo(string SucursalCodigo, string CajaCodigo, string Secreto, string? DireccionIp = null);

public sealed record RespuestaTokenDispositivo(bool Exitoso, string? Mensaje = null, string? Token = null, DateTimeOffset? ExpiraEn = null);

/// <summary>
/// Lo que la caja manda al Central mientras la están configurando: los datos del equipo y el usuario del Central que lo
/// autoriza. La contraseña se usa una sola vez para comprobarla y no se guarda en ningún lado de la caja.
/// </summary>
public sealed record SolicitudValidarConfiguracionCaja(
    string SucursalCodigo,
    string CajaCodigo,
    string Secreto,
    string? DireccionIp,
    string Usuario,
    string Contrasena);

/// <param name="Mensaje">Qué falló, para mostrarlo en la pantalla de la caja; nulo si se aceptó.</param>
public sealed record RespuestaValidarConfiguracionCaja(bool Aceptada, string? Mensaje = null, string? SucursalNombre = null, string? CajaNombre = null);

/// <summary>
/// Credencial recién emitida. El secreto solo se entrega en esta respuesta: el Central guarda su hash. En la caja se configuran
/// <c>Caja:Sucursal</c>, <c>Caja:Codigo</c> y <c>Central:Secreto</c>.
/// </summary>
public sealed record DatosCredencialDispositivo(int CajaId, string SucursalCodigo, string CajaCodigo, string Secreto, DateTimeOffset EmitidaEn);

public sealed record SolicitudRevocacionCredencial(string Motivo);

public sealed record SolicitudMotivo(string Motivo);

/// <summary>La caja autenticada, tal como la reconoce el Central.</summary>
public sealed record DatosDispositivo(int CajaId, string CajaCodigo, string CajaNombre, int SucursalId, string SucursalCodigo);

/// <summary>Nombres de los atributos de los tokens emitidos por el Central.</summary>
public static class AtributosTokenCentral
{
    public const string Tipo = "tipo";
    public const string TipoUsuario = "usuario";
    public const string TipoDispositivo = "dispositivo";

    /// <summary>Supervisor o gerente de tienda dentro del módulo de cuadre: entra con su usuario de caja.</summary>
    public const string TipoCuadre = "cuadre";

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

    /// <summary>Sucursales que el usuario del módulo de cuadre puede ver; una por cada caja que tiene asignada.</summary>
    public const string SucursalPermitida = "sucursal_cuadre";
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
