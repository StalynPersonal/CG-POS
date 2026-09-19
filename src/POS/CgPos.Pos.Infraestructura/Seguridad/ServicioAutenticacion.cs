using System.Collections.Frozen;
using CgPos.Dominio.Organizacion;
using CgPos.Dominio.Seguridad;
using CgPos.Pos.Aplicacion.Abstracciones;
using CgPos.Pos.Aplicacion.Organizacion;
using CgPos.Pos.Aplicacion.Seguridad;
using CgPos.Pos.Infraestructura.Persistencia;
using Microsoft.EntityFrameworkCore;
using CgPos.Dominio.Comun;

namespace CgPos.Pos.Infraestructura.Seguridad;

internal sealed class ServicioAutenticacion(
    ContextoDatosPos contexto,
    VerificadorCredenciales verificador,
    IContextoCaja contextoCaja,
    IAuditoria auditoria,
    TimeProvider reloj) : IServicioAutenticacion
{
    public async Task<ResultadoAutenticacion> IngresarAsync(CredencialUsuario credencial, CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(credencial);

        var caja = contextoCaja.CajaId is { } cajaId
            ? await contexto.Cajas.SingleOrDefaultAsync(c => c.Id == cajaId, cancelacion)
            : null;
        if (caja is null)
            return ResultadoAutenticacion.Rechazo(MotivoRechazoIngreso.CajaNoConfigurada);

        var sucursalActiva = await contexto.Sucursales.Where(s => s.Id == caja.SucursalId).Select(s => s.Activa).SingleAsync(cancelacion);
        var verificacion = await verificador.VerificarAsync(credencial, caja.Id, cancelacion);

        var resultado = verificacion.Resultado switch
        {
            ResultadoVerificacion.NoIdentificado or ResultadoVerificacion.Incorrecta => ResultadoAutenticacion.Rechazo(MotivoRechazoIngreso.CredencialesInvalidas),
            ResultadoVerificacion.Bloqueado => ResultadoAutenticacion.Rechazo(MotivoRechazoIngreso.UsuarioBloqueado, verificacion.BloqueadoHasta),
            ResultadoVerificacion.Inactivo => ResultadoAutenticacion.Rechazo(MotivoRechazoIngreso.UsuarioInactivo),
            ResultadoVerificacion.RolInactivo => ResultadoAutenticacion.Rechazo(MotivoRechazoIngreso.RolInactivo),
            _ when !caja.Habilitada || !sucursalActiva => ResultadoAutenticacion.Rechazo(MotivoRechazoIngreso.CajaDeshabilitada),
            _ when !verificacion.Usuario!.PuedeOperarCaja(caja.Id) => ResultadoAutenticacion.Rechazo(MotivoRechazoIngreso.CajaNoAsignada),
            _ => ResultadoAutenticacion.Exito(CrearSesion(verificacion.Usuario!, verificacion.Rol!, caja)),
        };

        if (resultado.Exitoso)
            verificacion.Usuario!.RegistrarIngresoExitoso(reloj.Ahora());

        Auditar(credencial, verificacion.Usuario, resultado, caja);
        await contexto.SaveChangesAsync(cancelacion);
        return resultado;
    }

    private void Auditar(CredencialUsuario credencial, Usuario? usuario, ResultadoAutenticacion resultado, Caja caja)
    {
        var accion = resultado switch
        {
            { Exitoso: true } => "Seguridad.IngresoExitoso",
            { Motivo: MotivoRechazoIngreso.UsuarioBloqueado } => "Seguridad.IngresoBloqueado",
            _ => "Seguridad.IngresoRechazado",
        };

        var codigoIntentado = usuario is null ? credencial.CodigoUsuario?.Trim() : null;

        auditoria.Registrar(new EntradaAuditoria(
            accion,
            "Usuario",
            usuario?.Id.ToString(),
            Detalle: new { Motivo = resultado.Motivo?.ToString(), Caja = caja.Codigo, CodigoIntentado = codigoIntentado },
            Usuario: usuario is null ? null : new UsuarioAuditoria(usuario.Id, usuario.Nombre)));
    }

    private static SesionUsuario CrearSesion(Usuario usuario, Rol rol, Caja caja) =>
        new(
            usuario.Id,
            usuario.Codigo,
            usuario.Nombre,
            rol.Id,
            rol.Codigo,
            rol.Nombre,
            rol.Nivel,
            rol.PermisosAsignados.Select(p => p.PermisoCodigo).ToFrozenSet(StringComparer.Ordinal),
            caja.Id,
            caja.Codigo,
            caja.Nombre,
            caja.SucursalId);
}
