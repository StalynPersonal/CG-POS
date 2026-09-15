using CgPos.Dominio.Auditoria;
using CgPos.Dominio.Seguridad;
using CgPos.Pos.Aplicacion.Abstracciones;
using CgPos.Pos.Aplicacion.Seguridad;
using CgPos.Pos.Infraestructura.Persistencia;

namespace CgPos.Pos.Infraestructura.Seguridad;

internal sealed class ServicioAutorizacion(
    ContextoDatosPos contexto,
    VerificadorCredenciales verificador,
    IAuditoria auditoria,
    TimeProvider reloj) : IServicioAutorizacion
{
    /// <summary>Tiempo que tiene el usuario para usar la autorización concedida.</summary>
    internal static readonly TimeSpan VigenciaAutorizacion = TimeSpan.FromMinutes(5);

    public async Task<ResultadoAutorizacion> AutorizarAsync(SolicitudAutorizacionSupervisor solicitud, CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(solicitud);

        if (!CatalogoPermisos.Existe(solicitud.Permiso))
            return ResultadoAutorizacion.Rechazo(MotivoRechazoAutorizacion.PermisoInexistente);

        if (!solicitud.ForzarSupervisor && solicitud.Solicitante.TienePermiso(solicitud.Permiso))
            return ResultadoAutorizacion.SinSupervisor();

        var motivo = solicitud.Motivo?.Trim();
        if (string.IsNullOrEmpty(motivo))
            return ResultadoAutorizacion.Rechazo(MotivoRechazoAutorizacion.MotivoRequerido);
        if (motivo.Length > RegistroAuditoria.LargoMaximoMotivo)
            motivo = motivo[..RegistroAuditoria.LargoMaximoMotivo];

        var verificacion = await verificador.VerificarAsync(solicitud.CredencialSupervisor, solicitud.Solicitante.CajaId, cancelacion);

        var resultado = verificacion.Resultado switch
        {
            ResultadoVerificacion.NoIdentificado or ResultadoVerificacion.Incorrecta => ResultadoAutorizacion.Rechazo(MotivoRechazoAutorizacion.CredencialesInvalidas),
            ResultadoVerificacion.Bloqueado => ResultadoAutorizacion.Rechazo(MotivoRechazoAutorizacion.SupervisorBloqueado, verificacion.BloqueadoHasta),
            ResultadoVerificacion.Inactivo or ResultadoVerificacion.RolInactivo => ResultadoAutorizacion.Rechazo(MotivoRechazoAutorizacion.SupervisorInactivo),
            _ when !verificacion.Rol!.TienePermiso(CatalogoPermisos.AutorizarOperaciones) || !verificacion.Rol.TienePermiso(solicitud.Permiso)
                => ResultadoAutorizacion.Rechazo(MotivoRechazoAutorizacion.SinPermisoParaAutorizar),
            _ when verificacion.Rol!.Nivel < solicitud.Solicitante.Nivel
                   || (solicitud.ForzarSupervisor && verificacion.Rol.Nivel <= solicitud.Solicitante.Nivel)
                => ResultadoAutorizacion.Rechazo(MotivoRechazoAutorizacion.NivelInsuficiente),
            _ => ResultadoAutorizacion.Conceder(Guid.CreateVersion7(), verificacion.Usuario!.Id, verificacion.Usuario.Nombre),
        };

        if (resultado.Concedida)
        {
            verificacion.Usuario!.Desbloquear();

            // Queda registrada para que la operación la consuma una sola vez y antes de que venza.
            contexto.AutorizacionesOtorgadas.Add(AutorizacionOtorgada.Otorgar(
                resultado.AutorizacionId!.Value,
                solicitud.Permiso,
                solicitud.Solicitante.CajaId,
                solicitud.Solicitante.UsuarioId,
                solicitud.Solicitante.Nombre,
                verificacion.Usuario.Id,
                verificacion.Usuario.Nombre,
                motivo,
                reloj.GetUtcNow(),
                VigenciaAutorizacion));
        }

        auditoria.Registrar(new EntradaAuditoria(
            resultado.Concedida ? "Seguridad.AutorizacionConcedida" : "Seguridad.AutorizacionDenegada",
            solicitud.TipoEntidad ?? "Autorizacion",
            solicitud.EntidadId ?? resultado.AutorizacionId?.ToString(),
            Detalle: new
            {
                solicitud.Permiso,
                resultado.AutorizacionId,
                Rechazo = resultado.Motivo?.ToString(),
                solicitud.CredencialSupervisor.Metodo,
                SupervisorIntentado = verificacion.Usuario?.Codigo,
            },
            Motivo: motivo,
            Usuario: new UsuarioAuditoria(solicitud.Solicitante.UsuarioId, solicitud.Solicitante.Nombre),
            AutorizadoPor: resultado.Concedida ? new UsuarioAuditoria(verificacion.Usuario!.Id, verificacion.Usuario.Nombre) : null));

        await contexto.SaveChangesAsync(cancelacion);
        return resultado;
    }
}
