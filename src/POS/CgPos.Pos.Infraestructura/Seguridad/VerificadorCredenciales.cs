using CgPos.Dominio.Seguridad;
using CgPos.Pos.Aplicacion.Abstracciones;
using CgPos.Pos.Aplicacion.Organizacion;
using CgPos.Pos.Aplicacion.Seguridad;
using CgPos.Pos.Infraestructura.Persistencia;
using Microsoft.EntityFrameworkCore;

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
/// Identifica al usuario por su credencial y aplica el bloqueo por intentos fallidos.
/// No guarda: quien lo usa registra la auditoría y persiste en el mismo SaveChanges.
/// </summary>
internal sealed class VerificadorCredenciales(
    ContextoDatosPos contexto,
    IHashCredenciales hashCredenciales,
    IParametros parametros,
    ILectorHuella lectorHuella,
    TimeProvider reloj)
{
    // Hash de relleno para que un código inexistente tarde lo mismo que un PIN incorrecto (evita enumerar usuarios).
    private static readonly Lazy<string> HashRelleno = new(() => new HashCredenciales().HashPin("0000"));

    public async Task<VerificacionCredencial> VerificarAsync(CredencialUsuario credencial, Guid? cajaId, CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(credencial);
        var ahora = reloj.GetUtcNow();

        var (usuario, credencialValida) = credencial switch
        {
            CredencialUsuario.Pin pin => await IdentificarPorPinAsync(pin, cancelacion),
            CredencialUsuario.Carne carne => (await IdentificarPorCarneAsync(carne, cancelacion), true),
            CredencialUsuario.Huella => (await IdentificarPorHuellaAsync(cancelacion), true),
            _ => throw new ArgumentOutOfRangeException(nameof(credencial), credencial, "Tipo de credencial no soportado."),
        };

        if (usuario is null)
            return new(ResultadoVerificacion.NoIdentificado, null, null, null);

        if (usuario.EstaBloqueado(ahora))
            return new(ResultadoVerificacion.Bloqueado, usuario, null, usuario.BloqueadoHasta);

        if (!credencialValida)
        {
            var intentosMaximos = await parametros.ObtenerEnteroAsync(ClavesParametros.IntentosMaximosPin, cajaId, cancelacion);
            var minutosBloqueo = await parametros.ObtenerEnteroAsync(ClavesParametros.MinutosBloqueo, cajaId, cancelacion);
            var quedoBloqueado = usuario.RegistrarIngresoFallido(ahora, Math.Max(1, intentosMaximos), TimeSpan.FromMinutes(Math.Max(1, minutosBloqueo)));

            return quedoBloqueado
                ? new(ResultadoVerificacion.Bloqueado, usuario, null, usuario.BloqueadoHasta)
                : new(ResultadoVerificacion.Incorrecta, usuario, null, null);
        }

        // El estado del usuario se revela solo después de una credencial correcta.
        if (!usuario.Activo)
            return new(ResultadoVerificacion.Inactivo, usuario, null, null);

        var rol = await contexto.Roles.Include(r => r.PermisosAsignados).SingleAsync(r => r.Id == usuario.RolId, cancelacion);
        return rol.Activo
            ? new(ResultadoVerificacion.Valida, usuario, rol, null)
            : new(ResultadoVerificacion.RolInactivo, usuario, rol, null);
    }

    private async Task<(Usuario? Usuario, bool Valida)> IdentificarPorPinAsync(CredencialUsuario.Pin pin, CancellationToken cancelacion)
    {
        var codigo = pin.CodigoUsuario?.Trim();
        var valor = pin.Valor ?? string.Empty;

        var usuario = string.IsNullOrEmpty(codigo)
            ? null
            : await contexto.Usuarios.Include(u => u.CajasAsignadas).SingleOrDefaultAsync(u => u.Codigo == codigo, cancelacion);

        if (usuario?.PinHash is null)
        {
            hashCredenciales.VerificarPin("0000", HashRelleno.Value);
            return (usuario, false);
        }

        return (usuario, hashCredenciales.VerificarPin(valor, usuario.PinHash));
    }

    private async Task<Usuario?> IdentificarPorCarneAsync(CredencialUsuario.Carne carne, CancellationToken cancelacion)
    {
        if (string.IsNullOrWhiteSpace(carne.CodigoBarras))
            return null;

        var hash = hashCredenciales.HashCredencialBarras(carne.CodigoBarras);
        return await contexto.Usuarios.Include(u => u.CajasAsignadas).SingleOrDefaultAsync(u => u.CredencialBarrasHash == hash, cancelacion);
    }

    private async Task<Usuario?> IdentificarPorHuellaAsync(CancellationToken cancelacion)
    {
        var usuarioId = await lectorHuella.IdentificarUsuarioAsync(cancelacion);
        return usuarioId is null
            ? null
            : await contexto.Usuarios.Include(u => u.CajasAsignadas).SingleOrDefaultAsync(u => u.Id == usuarioId, cancelacion);
    }
}
