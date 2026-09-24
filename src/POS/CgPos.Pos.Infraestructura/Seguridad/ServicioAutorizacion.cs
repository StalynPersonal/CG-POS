using CgPos.Dominio.Auditoria;
using CgPos.Dominio.Seguridad;
using CgPos.Pos.Aplicacion.Abstracciones;
using CgPos.Pos.Aplicacion.Organizacion;
using CgPos.Pos.Aplicacion.Seguridad;
using CgPos.Pos.Infraestructura.Persistencia;
using CgPos.Dominio.Comun;

namespace CgPos.Pos.Infraestructura.Seguridad;

internal sealed class ServicioAutorizacion(
    ContextoDatosPos contexto,
    VerificadorCredenciales verificador,
    IParametros parametros,
    IAuditoria auditoria,
    TimeProvider reloj) : IServicioAutorizacion
{
    public async Task<ResultadoAutorizacion> AutorizarAsync(SolicitudAutorizacionSupervisor solicitud, CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(solicitud);

        if (!CatalogoPermisos.Existe(solicitud.Permiso))
            return ResultadoAutorizacion.Rechazo(MotivoRechazoAutorizacion.PermisoInexistente);

        if (!solicitud.ForzarSupervisor && solicitud.Solicitante.TienePermiso(solicitud.Permiso))
            return ResultadoAutorizacion.SinSupervisor();

        // El motivo es opcional: quien autoriza está delante del cajero y muchas veces la explicación sobra. Lo que sí
        // queda siempre en la auditoría es qué se autorizó, a quién y quién lo autorizó.
        var motivo = solicitud.Motivo?.Trim();
        if (string.IsNullOrEmpty(motivo))
            motivo = null;
        else if (motivo.Length > RegistroAuditoria.LargoMaximoMotivo)
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
            // El tiempo para usar la autorización lo define el negocio.
            var minutosVigencia = await parametros.ObtenerEnteroAsync(ClavesParametros.MinutosVigenciaAutorizacion, solicitud.Solicitante.CajaId, cancelacion);
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
                reloj.Ahora(),
                TimeSpan.FromMinutes(minutosVigencia)));
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
                SupervisorIntentado = verificacion.Usuario?.Codigo ?? solicitud.CredencialSupervisor.CodigoUsuario?.Trim(),
            },
            Motivo: motivo,
            Usuario: new UsuarioAuditoria(solicitud.Solicitante.UsuarioId, solicitud.Solicitante.Nombre),
            AutorizadoPor: resultado.Concedida ? new UsuarioAuditoria(verificacion.Usuario!.Id, verificacion.Usuario.Nombre) : null));

        await contexto.SaveChangesAsync(cancelacion);
        return resultado;
    }
}
