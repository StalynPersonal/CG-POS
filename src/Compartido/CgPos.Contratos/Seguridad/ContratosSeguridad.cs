namespace CgPos.Contratos.Seguridad;

/// <summary>Ingreso a la caja con código de usuario y clave.</summary>
public sealed record SolicitudIngreso(string CodigoUsuario, string Clave);

public sealed record RespuestaIngreso(
    bool Exitoso,
    string? Mensaje = null,
    string? Token = null,
    DateTimeOffset? ExpiraEn = null,
    DatosSesion? Sesion = null,
    DateTimeOffset? BloqueadoHasta = null);

public sealed record DatosSesion(
    int UsuarioId,
    string Codigo,
    string Nombre,
    string RolCodigo,
    string RolNombre,
    int Nivel,
    IReadOnlyList<string> Permisos,
    int CajaId,
    string CajaCodigo,
    string CajaNombre,
    int SucursalId);

/// <summary>Solicitud de autorización de supervisor con su código de usuario y su clave.</summary>
public sealed record SolicitudAutorizacion(
    string Permiso,
    string Motivo,
    string? CodigoSupervisor = null,
    string? Clave = null,
    string? TipoEntidad = null,
    string? EntidadId = null,
    bool ForzarSupervisor = false);

public sealed record RespuestaAutorizacion(
    bool Concedida,
    string? Mensaje = null,
    Guid? AutorizacionId = null,
    int? SupervisorId = null,
    string? SupervisorNombre = null,
    bool RequirioSupervisor = true);

public sealed record DatosEstadoCaja(
    bool Configurada,
    bool Habilitada,
    int? CajaId = null,
    string? CajaCodigo = null,
    string? CajaNombre = null,
    string? SucursalNombre = null,
    string? EmpresaNombre = null,
    string? Problema = null,
    Catalogo.DatosMoneda? MonedaLocal = null,
    DatosActualizacionCaja? Actualizacion = null);

/// <summary>
/// Qué está bajando la caja del Central en este momento, para que la pantalla lo diga en vez de dejar al cajero
/// mirando un error mientras el Agente todavía está trabajando.
/// </summary>
/// <param name="EnCurso">Verdadero mientras se está bajando o aplicando algo.</param>
/// <param name="Etapa">Con qué se está actualizando la caja, tal como se lee en pantalla: «Actualizando la caja con clientes».</param>
/// <param name="DesdeCuando">Desde cuándo lleva en esta actualización.</param>
/// <param name="UltimoError">Lo que dejó la última actualización que falló; nulo si la última terminó bien.</param>
/// <param name="Hechos">Cuántos elementos lleva aplicados de la etapa; nulo en las etapas que no se cuentan.</param>
/// <param name="Total">Cuántos elementos trae la etapa.</param>
public sealed record DatosActualizacionCaja(
    bool EnCurso,
    string? Etapa = null,
    DateTimeOffset? DesdeCuando = null,
    string? UltimoError = null,
    int? Hechos = null,
    int? Total = null)
{
    /// <summary>
    /// Lo que se muestra en la caja: una sola línea que dice con qué se está actualizando. Cuánto lleva no se escribe: eso
    /// lo dice la barra de progreso, y en la caja lo que importa es si sigue trabajando, no el número exacto.
    /// </summary>
    public string? Detalle => Etapa;
}

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
