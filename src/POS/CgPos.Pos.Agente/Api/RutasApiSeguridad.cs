using System.Security.Claims;
using CgPos.Contratos.Seguridad;
using CgPos.Pos.Agente.Pantallas;
using CgPos.Pos.Agente.Seguridad;
using CgPos.Pos.Aplicacion.Abstracciones;
using CgPos.Pos.Aplicacion.Organizacion;
using CgPos.Pos.Aplicacion.Seguridad;
using CgPos.Pos.Infraestructura.Persistencia;

namespace CgPos.Pos.Agente.Api;

public static class RutasApiSeguridad
{
    public static IEndpointRouteBuilder MapearApiSeguridad(this IEndpointRouteBuilder aplicacion)
    {
        var api = aplicacion.MapGroup("/api");

        // Público: la pantalla de ingreso lo necesita antes de que haya sesión.
        api.MapGet("/caja/estado", async (IEstadoCaja estadoCaja, CancellationToken cancelacion) =>
            Results.Ok(await estadoCaja.ObtenerAsync(cancelacion)));

        var sesion = api.MapGroup("/sesion");

        sesion.MapPost("/pin", (SolicitudIngresoPin solicitud, IServicioAutenticacion autenticacion, EmisorTokens emisor, IParametros parametros, CancellationToken cancelacion) =>
            IngresarAsync(new CredencialUsuario.Pin(solicitud.CodigoUsuario ?? string.Empty, solicitud.Pin ?? string.Empty), autenticacion, emisor, parametros, cancelacion));

        sesion.MapPost("/carne", (SolicitudIngresoCarne solicitud, IServicioAutenticacion autenticacion, EmisorTokens emisor, IParametros parametros, CancellationToken cancelacion) =>
            IngresarAsync(new CredencialUsuario.Carne(solicitud.CodigoBarras ?? string.Empty), autenticacion, emisor, parametros, cancelacion));

        sesion.MapPost("/huella", (IServicioAutenticacion autenticacion, EmisorTokens emisor, IParametros parametros, CancellationToken cancelacion) =>
            IngresarAsync(new CredencialUsuario.Huella(), autenticacion, emisor, parametros, cancelacion));

        sesion.MapGet("/actual", (ClaimsPrincipal usuario) =>
                EmisorTokens.LeerSesion(usuario) is { } actual ? Results.Ok(ConvertirDto(actual)) : Results.Unauthorized())
            .RequireAuthorization();

        sesion.MapPost("/cerrar", async (ClaimsPrincipal usuario, IAuditoria auditoria, ContextoDatosPos contexto, PublicadorPantallaCliente pantallaCliente,
                CancellationToken cancelacion) =>
            {
                if (EmisorTokens.LeerSesion(usuario) is { } actual)
                {
                    auditoria.Registrar(new EntradaAuditoria("Seguridad.SesionCerrada", "Usuario", actual.UsuarioId.ToString(),
                        Usuario: new UsuarioAuditoria(actual.UsuarioId, actual.Nombre)));
                    await contexto.SaveChangesAsync(cancelacion);
                }

                // Sin cajero, la pantalla del cliente vuelve a la bienvenida y la publicidad.
                await pantallaCliente.PublicarAsync(null, cancelacion);

                return Results.NoContent();
            })
            .RequireAuthorization();

        api.MapPost("/autorizaciones", AutorizarAsync).RequireAuthorization();

        return aplicacion;
    }

    private static async Task<IResult> IngresarAsync(CredencialUsuario credencial, IServicioAutenticacion autenticacion, EmisorTokens emisor, IParametros parametros,
        CancellationToken cancelacion)
    {
        var resultado = await autenticacion.IngresarAsync(credencial, cancelacion);

        if (resultado.Sesion is { } sesion)
        {
            var horas = await parametros.ObtenerDecimalAsync(ClavesParametros.HorasSesion, sesion.CajaId, cancelacion);
            if (horas <= 0)
                throw new ParametroNoConfiguradoExcepcion(ClavesParametros.HorasSesion, "debe ser mayor que cero");

            var (token, expiraEn) = emisor.Emitir(sesion, TimeSpan.FromHours((double)horas));
            return Results.Ok(new RespuestaIngreso(true, Token: token, ExpiraEn: expiraEn, Sesion: ConvertirDto(sesion)));
        }

        var mensaje = MensajesSeguridad.Para(resultado.Motivo!.Value, credencial, resultado.BloqueadoHasta);
        return Results.Json(new RespuestaIngreso(false, mensaje, BloqueadoHasta: resultado.BloqueadoHasta), statusCode: StatusCodes.Status401Unauthorized);
    }

    private static async Task<IResult> AutorizarAsync(SolicitudAutorizacion solicitud, ClaimsPrincipal usuario, IServicioAutorizacion servicio, CancellationToken cancelacion)
    {
        if (EmisorTokens.LeerSesion(usuario) is not { } solicitante)
            return Results.Unauthorized();

        CredencialUsuario credencial = string.IsNullOrWhiteSpace(solicitud.CodigoBarras)
            ? new CredencialUsuario.Pin(solicitud.CodigoSupervisor ?? string.Empty, solicitud.Pin ?? string.Empty)
            : new CredencialUsuario.Carne(solicitud.CodigoBarras);

        var resultado = await servicio.AutorizarAsync(
            new SolicitudAutorizacionSupervisor(solicitante, solicitud.Permiso ?? string.Empty, solicitud.Motivo ?? string.Empty, credencial, solicitud.TipoEntidad, solicitud.EntidadId,
                solicitud.ForzarSupervisor),
            cancelacion);

        return Results.Ok(new RespuestaAutorizacion(
            resultado.Concedida,
            resultado.Motivo is { } motivo ? MensajesSeguridad.Para(motivo, resultado.BloqueadoHasta) : null,
            resultado.AutorizacionId,
            resultado.SupervisorId,
            resultado.SupervisorNombre,
            resultado.RequirioSupervisor));
    }

    private static DatosSesion ConvertirDto(SesionUsuario sesion) =>
        new(
            sesion.UsuarioId,
            sesion.Codigo,
            sesion.Nombre,
            sesion.RolCodigo,
            sesion.RolNombre,
            sesion.Nivel,
            sesion.Permisos.Order(StringComparer.Ordinal).ToList(),
            sesion.CajaId,
            sesion.CajaCodigo,
            sesion.CajaNombre,
            sesion.SucursalId);
}
