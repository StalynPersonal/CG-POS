using System.Globalization;
using CgPos.Dominio.Globalizacion;

namespace CgPos.Pos.Aplicacion.Seguridad;

/// <summary>Usuario autenticado en esta caja, con los permisos de su rol al momento de ingresar.</summary>
public sealed record SesionUsuario(
    Guid UsuarioId,
    string Codigo,
    string Nombre,
    Guid RolId,
    string RolCodigo,
    string RolNombre,
    int Nivel,
    IReadOnlySet<string> Permisos,
    Guid CajaId,
    string CajaCodigo,
    string CajaNombre,
    Guid SucursalId)
{
    public bool TienePermiso(string permiso) => Permisos.Contains(permiso);
}

/// <summary>Formas de identificar a un usuario.</summary>
public abstract record CredencialUsuario
{
    private CredencialUsuario()
    {
    }

    public sealed record Pin(string CodigoUsuario, string Valor) : CredencialUsuario;

    /// <summary>Código de barras del carné, leído con el escáner.</summary>
    public sealed record Carne(string CodigoBarras) : CredencialUsuario;

    /// <summary>El lector de huella identifica al usuario.</summary>
    public sealed record Huella : CredencialUsuario;

    public string Metodo => this switch
    {
        Pin => "PIN",
        Carne => "Carné",
        Huella => "Huella",
        _ => "Desconocido",
    };
}

public enum MotivoRechazoIngreso
{
    CredencialesInvalidas,
    UsuarioBloqueado,
    UsuarioInactivo,
    RolInactivo,
    CajaNoConfigurada,
    CajaDeshabilitada,
    CajaNoAsignada,
}

public sealed record ResultadoAutenticacion(SesionUsuario? Sesion, MotivoRechazoIngreso? Motivo, DateTimeOffset? BloqueadoHasta)
{
    public bool Exitoso => Sesion is not null;

    public static ResultadoAutenticacion Exito(SesionUsuario sesion) => new(sesion, null, null);

    public static ResultadoAutenticacion Rechazo(MotivoRechazoIngreso motivo, DateTimeOffset? bloqueadoHasta = null) =>
        new(null, motivo, bloqueadoHasta);
}

public interface IServicioAutenticacion
{
    /// <summary>
    /// Valida la credencial, que la caja esté habilitada y que el usuario pueda operarla (RF-169, RF-6).
    /// Registra el intento en auditoría y aplica el bloqueo por intentos fallidos.
    /// </summary>
    Task<ResultadoAutenticacion> IngresarAsync(CredencialUsuario credencial, CancellationToken cancelacion = default);
}

public enum MotivoRechazoAutorizacion
{
    PermisoInexistente,
    MotivoRequerido,
    CredencialesInvalidas,
    SupervisorBloqueado,
    SupervisorInactivo,
    SinPermisoParaAutorizar,
    NivelInsuficiente,
}

/// <param name="Solicitante">Usuario que necesita realizar la operación.</param>
/// <param name="Permiso">Permiso de la operación (del catálogo).</param>
/// <param name="Motivo">Motivo obligatorio cuando se requiere supervisor.</param>
/// <param name="CredencialSupervisor">Credencial del supervisor que autoriza.</param>
public sealed record SolicitudAutorizacionSupervisor(
    SesionUsuario Solicitante,
    string Permiso,
    string Motivo,
    CredencialUsuario CredencialSupervisor,
    string? TipoEntidad = null,
    string? EntidadId = null);

public sealed record ResultadoAutorizacion(
    bool Concedida,
    bool RequirioSupervisor,
    MotivoRechazoAutorizacion? Motivo,
    Guid? AutorizacionId,
    Guid? SupervisorId,
    string? SupervisorNombre,
    DateTimeOffset? BloqueadoHasta)
{
    /// <summary>El solicitante ya tiene el permiso: no hace falta supervisor.</summary>
    public static ResultadoAutorizacion SinSupervisor() => new(true, false, null, null, null, null, null);

    public static ResultadoAutorizacion Conceder(Guid autorizacionId, Guid supervisorId, string supervisorNombre) =>
        new(true, true, null, autorizacionId, supervisorId, supervisorNombre, null);

    public static ResultadoAutorizacion Rechazo(MotivoRechazoAutorizacion motivo, DateTimeOffset? bloqueadoHasta = null) =>
        new(false, true, motivo, null, null, null, bloqueadoHasta);
}

public interface IServicioAutorizacion
{
    /// <summary>
    /// Autoriza una operación para la que el solicitante no tiene permiso. El supervisor debe tener
    /// <c>Seguridad.AutorizarOperaciones</c>, el permiso de la operación y un nivel igual o superior.
    /// Todo intento queda en auditoría con el motivo y quién autorizó.
    /// </summary>
    Task<ResultadoAutorizacion> AutorizarAsync(SolicitudAutorizacionSupervisor solicitud, CancellationToken cancelacion = default);
}

/// <summary>Lector biométrico. El dispositivo compara la huella y devuelve el usuario identificado.</summary>
public interface ILectorHuella
{
    Task<Guid?> IdentificarUsuarioAsync(CancellationToken cancelacion = default);
}

/// <summary>Mensajes para el usuario. No revelan si un código de usuario existe.</summary>
public static class MensajesSeguridad
{
    public static string Para(MotivoRechazoIngreso motivo, CredencialUsuario? credencial = null, DateTimeOffset? bloqueadoHasta = null) =>
        motivo switch
        {
            MotivoRechazoIngreso.CredencialesInvalidas => credencial switch
            {
                CredencialUsuario.Carne => "Carné no reconocido.",
                CredencialUsuario.Huella => "Huella no reconocida.",
                _ => "Usuario o PIN incorrecto.",
            },
            MotivoRechazoIngreso.UsuarioBloqueado => "Usuario bloqueado por intentos fallidos." + Hasta(bloqueadoHasta),
            MotivoRechazoIngreso.UsuarioInactivo => "El usuario está inactivo. Contacte al supervisor.",
            MotivoRechazoIngreso.RolInactivo => "El rol del usuario está inactivo. Contacte al supervisor.",
            MotivoRechazoIngreso.CajaNoConfigurada => "Esta caja no está configurada. Contacte a soporte.",
            MotivoRechazoIngreso.CajaDeshabilitada => "Esta caja está deshabilitada. Contacte al supervisor.",
            MotivoRechazoIngreso.CajaNoAsignada => "El usuario no está asignado a esta caja.",
            _ => "No se pudo iniciar sesión.",
        };

    public static string Para(MotivoRechazoAutorizacion motivo, DateTimeOffset? bloqueadoHasta = null) =>
        motivo switch
        {
            MotivoRechazoAutorizacion.PermisoInexistente => "La operación indicada no existe.",
            MotivoRechazoAutorizacion.MotivoRequerido => "Debe indicar el motivo.",
            MotivoRechazoAutorizacion.CredencialesInvalidas => "Credenciales de supervisor incorrectas.",
            MotivoRechazoAutorizacion.SupervisorBloqueado => "Supervisor bloqueado por intentos fallidos." + Hasta(bloqueadoHasta),
            MotivoRechazoAutorizacion.SupervisorInactivo => "El supervisor está inactivo.",
            MotivoRechazoAutorizacion.SinPermisoParaAutorizar => "Ese usuario no puede autorizar esta operación.",
            MotivoRechazoAutorizacion.NivelInsuficiente => "El supervisor no tiene nivel suficiente para autorizar a este usuario.",
            _ => "Autorización denegada.",
        };

    private static readonly CultureInfo Cultura = CulturaRd.Crear();

    private static string Hasta(DateTimeOffset? bloqueadoHasta) =>
        bloqueadoHasta is { } hasta ? $" Intente nuevamente a las {hasta.ToLocalTime().ToString("h:mm tt", Cultura)}." : string.Empty;
}
