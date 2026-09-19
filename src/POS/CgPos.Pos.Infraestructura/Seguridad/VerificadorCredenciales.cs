using CgPos.Dominio.Seguridad;
using CgPos.Pos.Aplicacion.Abstracciones;
using CgPos.Pos.Aplicacion.Organizacion;
using CgPos.Pos.Aplicacion.Seguridad;
using CgPos.Pos.Infraestructura.Persistencia;
using Microsoft.EntityFrameworkCore;
using CgPos.Dominio.Comun;

namespace CgPos.Pos.Infraestructura.Seguridad;

internal enum ResultadoVerificacion
{
    Valida,
    NoIdentificado,
    Incorrecta,
    Bloqueado,
    Inactivo,
    RolInactivo,
}

internal sealed record VerificacionCredencial(ResultadoVerificacion Resultado, Usuario? Usuario, Rol? Rol, DateTimeOffset? BloqueadoHasta);

/// <summary>
/// Identifica al usuario por su código y clave, y aplica el bloqueo por intentos fallidos.
/// No guarda: quien lo usa registra la auditoría y persiste en el mismo SaveChanges.
/// </summary>
internal sealed class VerificadorCredenciales(
    ContextoDatosPos contexto,
    IHashCredenciales hashCredenciales,
    IParametros parametros,
    TimeProvider reloj)
{
    // Hash de relleno para que un código inexistente tarde lo mismo que una clave incorrecta (evita enumerar usuarios).
    private static readonly Lazy<string> HashRelleno = new(() => new HashCredenciales().HashClave("relleno"));

    public async Task<VerificacionCredencial> VerificarAsync(CredencialUsuario credencial, int? cajaId, CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(credencial);
        var ahora = reloj.Ahora();

        var codigo = credencial.CodigoUsuario?.Trim();
        var usuario = string.IsNullOrEmpty(codigo)
            ? null
            : await contexto.Usuarios.Include(u => u.CajasAsignadas).SingleOrDefaultAsync(u => u.Codigo == codigo, cancelacion);

        bool claveValida;
        if (usuario?.ClaveHash is null)
        {
            hashCredenciales.VerificarClave("relleno", HashRelleno.Value);
            claveValida = false;
        }
        else
        {
            claveValida = hashCredenciales.VerificarClave(credencial.Clave ?? string.Empty, usuario.ClaveHash);
        }

        if (usuario is null)
            return new(ResultadoVerificacion.NoIdentificado, null, null, null);

        if (usuario.EstaBloqueado(ahora))
            return new(ResultadoVerificacion.Bloqueado, usuario, null, usuario.BloqueadoHasta);

        if (!claveValida)
        {
            var intentosMaximos = await parametros.ObtenerEnteroAsync(ClavesParametros.IntentosMaximosClave, cajaId, cancelacion);
            var minutosBloqueo = await parametros.ObtenerEnteroAsync(ClavesParametros.MinutosBloqueo, cajaId, cancelacion);
            var quedoBloqueado = usuario.RegistrarIngresoFallido(ahora, Math.Max(1, intentosMaximos), TimeSpan.FromMinutes(Math.Max(1, minutosBloqueo)));

            return quedoBloqueado
                ? new(ResultadoVerificacion.Bloqueado, usuario, null, usuario.BloqueadoHasta)
                : new(ResultadoVerificacion.Incorrecta, usuario, null, null);
        }

        // El estado del usuario se revela solo después de una clave correcta.
        if (!usuario.Activo)
            return new(ResultadoVerificacion.Inactivo, usuario, null, null);

        var rol = await contexto.Roles.Include(r => r.PermisosAsignados).SingleAsync(r => r.Id == usuario.RolId, cancelacion);
        return rol.Activo
            ? new(ResultadoVerificacion.Valida, usuario, rol, null)
            : new(ResultadoVerificacion.RolInactivo, usuario, rol, null);
    }
}
