using System.Globalization;
using CgPos.Central.Aplicacion.Abstracciones;
using CgPos.Dominio.Globalizacion;

namespace CgPos.Central.Aplicacion.Seguridad;

/// <summary>Usuario autenticado en el Central, con los permisos de su rol al momento de ingresar o renovar.</summary>
/// <param name="SesionId">Familia de tokens de renovación de esta sesión; cerrarla invalida también el token de acceso.</param>
public sealed record SesionCentralUsuario(
    int UsuarioId,
    string Codigo,
    string Nombre,
    int RolId,
    string RolCodigo,
    string RolNombre,
    IReadOnlySet<string> Permisos,
    bool DebeCambiarContrasena,
    Guid SesionId)
{
    public bool TienePermiso(string permiso) => Permisos.Contains(permiso);
}

public enum MotivoRechazoCentral
{
    CredencialesInvalidas,
    UsuarioBloqueado,
    UsuarioInactivo,
    RolInactivo,
    SesionInvalida,

    /// <summary>Se presentó un token de renovación ya usado: la sesión completa se revocó.</summary>
    SesionReutilizada,

    ContrasenaNoCumple,

    /// <summary>El usuario existe y la contraseña es buena, pero su rol no tiene el permiso que hace falta.</summary>
    PermisoInsuficiente,

    /// <summary>Tiene pendiente cambiar la contraseña: primero entra al Central Manager y la cambia.</summary>
    DebeCambiarContrasena,
}

public sealed record ResultadoSesionCentral(
    SesionCentralUsuario? Sesion,
    string? TokenRenovacion,
    DateTimeOffset? RenovacionExpiraEn,
    MotivoRechazoCentral? Motivo,
    DateTimeOffset? BloqueadoHasta = null,
    string? Detalle = null)
{
    public bool Exitoso => Sesion is not null;

    public static ResultadoSesionCentral Exito(SesionCentralUsuario sesion, string tokenRenovacion, DateTimeOffset renovacionExpiraEn) =>
        new(sesion, tokenRenovacion, renovacionExpiraEn, null);

    public static ResultadoSesionCentral Rechazo(MotivoRechazoCentral motivo, DateTimeOffset? bloqueadoHasta = null, string? detalle = null) =>
        new(null, null, null, motivo, bloqueadoHasta, detalle);
}

public interface IServicioSesionesCentral
{
    /// <summary>
    /// Valida usuario y contraseña, aplica el bloqueo por intentos e inicia una sesión con su token de renovación.
    /// Todo intento queda en auditoría.
    /// </summary>
    Task<ResultadoSesionCentral> IngresarAsync(string codigo, string contrasena, OrigenSolicitud origen, CancellationToken cancelacion = default);

    /// <summary>
    /// Usa el token de renovación una sola vez y entrega otro de la misma sesión. Un token ya usado revoca la sesión completa;
    /// un usuario desactivado, bloqueado o con el rol inactivo no renueva.
    /// </summary>
    Task<ResultadoSesionCentral> RenovarAsync(string tokenRenovacion, OrigenSolicitud origen, CancellationToken cancelacion = default);

    Task CerrarAsync(Guid sesionId, int usuarioId, CancellationToken cancelacion = default);

    /// <summary>La sesión sigue abierta y el usuario activo: se consulta con cada token de acceso.</summary>
    Task<bool> EsSesionActivaAsync(Guid sesionId, int usuarioId, CancellationToken cancelacion = default);

    /// <summary>Cambia la contraseña del propio usuario según la política configurada, cierra todas sus sesiones e inicia una nueva.</summary>
    Task<ResultadoSesionCentral> CambiarContrasenaAsync(int usuarioId, string actual, string nueva, OrigenSolicitud origen, CancellationToken cancelacion = default);

    /// <summary>
    /// Comprueba usuario, contraseña y un permiso concreto sin abrir sesión: es para autorizar una acción puntual, como
    /// configurar una caja. Cuenta los intentos fallidos, bloquea y audita igual que el ingreso normal, así que no sirve
    /// para probar contraseñas sin límite.
    /// </summary>
    Task<ResultadoAutorizacionCentral> AutorizarConPermisoAsync(string codigo, string contrasena, string permiso, OrigenSolicitud origen,
        CancellationToken cancelacion = default);
}

/// <summary>Política de contraseñas del Central; los valores salen de parámetros.</summary>
public static class ReglasContrasena
{
    /// <returns>El motivo por el que no cumple, o <c>null</c> si cumple.</returns>
    public static string? Validar(string? contrasena, int largoMinimo, bool compleja, string codigoUsuario)
    {
        if (string.IsNullOrEmpty(contrasena) || contrasena.Length < largoMinimo)
            return $"La contraseña debe tener al menos {largoMinimo} caracteres.";

        if (!compleja)
            return null;

        if (!contrasena.Any(char.IsUpper) || !contrasena.Any(char.IsLower) || !contrasena.Any(char.IsDigit) || contrasena.All(char.IsLetterOrDigit))
            return "La contraseña debe combinar mayúsculas, minúsculas, números y símbolos.";

        return contrasena.Contains(codigoUsuario, StringComparison.OrdinalIgnoreCase)
            ? "La contraseña no puede contener el nombre de usuario."
            : null;
    }
}

/// <summary>Mensajes para el usuario. No revelan si un usuario existe.</summary>
/// <summary>Resultado de autorizar una acción puntual con usuario y contraseña.</summary>
/// <param name="Usuario">Quién autorizó, para la auditoría; nulo si no se autorizó.</param>
public sealed record ResultadoAutorizacionCentral(UsuarioAuditoria? Usuario, MotivoRechazoCentral? Motivo, DateTimeOffset? BloqueadoHasta = null)
{
    public bool Exitoso => Usuario is not null;

    public static ResultadoAutorizacionCentral Exito(UsuarioAuditoria usuario) => new(usuario, null);

    public static ResultadoAutorizacionCentral Rechazo(MotivoRechazoCentral motivo, DateTimeOffset? bloqueadoHasta = null) => new(null, motivo, bloqueadoHasta);
}

public static class MensajesSeguridadCentral
{
    private static readonly CultureInfo Cultura = CulturaRd.Crear();

    public static string Para(MotivoRechazoCentral motivo, DateTimeOffset? bloqueadoHasta = null, string? detalle = null) =>
        motivo switch
        {
            MotivoRechazoCentral.CredencialesInvalidas => "Usuario o contraseña incorrectos.",
            MotivoRechazoCentral.UsuarioBloqueado => "Usuario bloqueado por intentos fallidos." + Hasta(bloqueadoHasta),
            MotivoRechazoCentral.UsuarioInactivo => "El usuario está inactivo. Contacte al administrador.",
            MotivoRechazoCentral.RolInactivo => "El rol del usuario está inactivo. Contacte al administrador.",
            MotivoRechazoCentral.SesionInvalida => "La sesión venció. Ingrese nuevamente.",
            MotivoRechazoCentral.SesionReutilizada => "La sesión se cerró por seguridad. Ingrese nuevamente.",
            MotivoRechazoCentral.ContrasenaNoCumple => detalle ?? "La contraseña no cumple la política.",
            MotivoRechazoCentral.PermisoInsuficiente => "Este usuario no tiene permiso para configurar cajas.",
            MotivoRechazoCentral.DebeCambiarContrasena => "Este usuario debe cambiar su contraseña en el Central Manager antes de configurar una caja.",
            _ => "No se pudo iniciar sesión.",
        };

    private static string Hasta(DateTimeOffset? bloqueadoHasta) =>
        bloqueadoHasta is { } hasta ? $" Intente nuevamente a las {hasta.ToLocalTime().ToString("h:mm tt", Cultura)}." : string.Empty;
}
