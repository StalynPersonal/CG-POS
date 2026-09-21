using System.Net;
using System.Net.Http.Json;
using CgPos.Central.Aplicacion.Organizacion;
using CgPos.Central.Pruebas.Soporte;
using CgPos.Contratos.Central;
using CgPos.Contratos.Serializacion;
using CgPos.Dominio.Seguridad;
using Microsoft.EntityFrameworkCore;

namespace CgPos.Central.Pruebas.Api;

[Collection(ColeccionCentral.Nombre)]
public class ApiSesionesPruebas(CentralEnPruebas central)
{
    [SkippableFact]
    public async Task Ingreso_correcto_entrega_tokens_y_la_sesion_con_los_permisos_del_rol()
    {
        Skip.If(central.MotivoOmision is not null, central.MotivoOmision);
        using var cliente = central.CrearCliente();

        var (respuesta, ingreso) = await CentralEnPruebas.IngresarAsync(cliente, "ADMIN", CentralEnPruebas.ContrasenaAdministrador);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Equal("no-store", respuesta.Headers.CacheControl?.ToString());
        Assert.True(ingreso!.Exitoso, ingreso.Mensaje);
        Assert.False(string.IsNullOrEmpty(ingreso.TokenAcceso));
        Assert.False(string.IsNullOrEmpty(ingreso.TokenRenovacion));
        Assert.True(ingreso.RenovacionExpiraEn > ingreso.AccesoExpiraEn);
        Assert.Equal(CatalogoPermisosCentral.Todos.Count, ingreso.Sesion!.Permisos.Count);

        using var sinToken = await cliente.GetAsync("/api/sesion/actual");
        Assert.Equal(HttpStatusCode.Unauthorized, sinToken.StatusCode);

        using var conToken = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Get, "/api/sesion/actual", ingreso.TokenAcceso));
        var actual = await conToken.Content.ReadFromJsonAsync<DatosSesionCentral>(OpcionesJson.Predeterminadas);
        Assert.Equal("ADMIN", actual!.Codigo);
        Assert.Equal(ingreso.Sesion.SesionId, actual.SesionId);
        Assert.Contains(CatalogoPermisosCentral.AdministrarDispositivos, actual.Permisos);

        var auditado = await central.UsarContextoAsync(contexto =>
            contexto.Auditoria.AnyAsync(a => a.Accion == "Seguridad.IngresoExitoso" && a.EntidadId == ingreso.Sesion.UsuarioId.ToString()));
        Assert.True(auditado);
    }

    [SkippableFact]
    public async Task Contrasena_incorrecta_responde_mensaje_generico_y_bloquea_al_llegar_al_maximo()
    {
        Skip.If(central.MotivoOmision is not null, central.MotivoOmision);
        using var cliente = central.CrearCliente();
        await central.CrearUsuarioAsync("BLOQUEO", "Clave.Correcta1", false, CatalogoPermisosCentral.ConsultarReportes);

        var (_, inexistente) = await CentralEnPruebas.IngresarAsync(cliente, "NO-EXISTE", "Cualquiera.123");
        Assert.Equal("Usuario o contraseña incorrectos.", inexistente!.Mensaje);

        // Parámetro de desarrollo: 5 intentos.
        RespuestaSesionCentral? ultimo = null;
        for (var intento = 1; intento <= 5; intento++)
        {
            var (respuesta, cuerpo) = await CentralEnPruebas.IngresarAsync(cliente, "BLOQUEO", "Clave.Incorrecta1");
            Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
            Assert.Null(cuerpo!.TokenAcceso);
            ultimo = cuerpo;
            if (intento < 5)
                Assert.Equal("Usuario o contraseña incorrectos.", cuerpo.Mensaje);
        }

        Assert.StartsWith("Usuario bloqueado", ultimo!.Mensaje);
        Assert.NotNull(ultimo.BloqueadoHasta);

        var (conClaveCorrecta, bloqueado) = await CentralEnPruebas.IngresarAsync(cliente, "BLOQUEO", "Clave.Correcta1");
        Assert.Equal(HttpStatusCode.Unauthorized, conClaveCorrecta.StatusCode);
        Assert.StartsWith("Usuario bloqueado", bloqueado!.Mensaje);
    }

    [SkippableFact]
    public async Task Renovar_rota_el_token_y_reutilizar_uno_ya_usado_cierra_la_sesion()
    {
        Skip.If(central.MotivoOmision is not null, central.MotivoOmision);
        using var cliente = central.CrearCliente();
        var (_, ingreso) = await CentralEnPruebas.IngresarAsync(cliente, "ADMIN", CentralEnPruebas.ContrasenaAdministrador);

        var renovada = await RenovarAsync(cliente, ingreso!.TokenRenovacion!);
        Assert.True(renovada.Cuerpo!.Exitoso, renovada.Cuerpo.Mensaje);
        Assert.NotEqual(ingreso.TokenRenovacion, renovada.Cuerpo.TokenRenovacion);
        Assert.Equal(ingreso.Sesion!.SesionId, renovada.Cuerpo.Sesion!.SesionId);

        using (var conNuevo = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Get, "/api/sesion/actual", renovada.Cuerpo.TokenAcceso)))
            Assert.Equal(HttpStatusCode.OK, conNuevo.StatusCode);

        // Alguien presenta el token original ya usado: se revoca toda la sesión.
        var reutilizada = await RenovarAsync(cliente, ingreso.TokenRenovacion!);
        Assert.Equal(HttpStatusCode.Unauthorized, reutilizada.Estado);
        Assert.Equal("La sesión se cerró por seguridad. Ingrese nuevamente.", reutilizada.Cuerpo!.Mensaje);

        Assert.Equal(HttpStatusCode.Unauthorized, (await RenovarAsync(cliente, renovada.Cuerpo.TokenRenovacion!)).Estado);
        using var tokenRevocado = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Get, "/api/sesion/actual", renovada.Cuerpo.TokenAcceso));
        Assert.Equal(HttpStatusCode.Unauthorized, tokenRevocado.StatusCode);
    }

    [SkippableFact]
    public async Task Cerrar_sesion_invalida_el_token_de_acceso_y_el_de_renovacion()
    {
        Skip.If(central.MotivoOmision is not null, central.MotivoOmision);
        using var cliente = central.CrearCliente();
        var (_, ingreso) = await CentralEnPruebas.IngresarAsync(cliente, "ADMIN", CentralEnPruebas.ContrasenaAdministrador);

        using (var cierre = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Post, "/api/sesion/cerrar", ingreso!.TokenAcceso)))
            Assert.Equal(HttpStatusCode.NoContent, cierre.StatusCode);

        using var despues = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Get, "/api/sesion/actual", ingreso.TokenAcceso));
        Assert.Equal(HttpStatusCode.Unauthorized, despues.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await RenovarAsync(cliente, ingreso.TokenRenovacion!)).Estado);
    }

    [SkippableFact]
    public async Task Con_contrasena_temporal_solo_puede_cambiarla_y_el_cambio_cumple_la_politica()
    {
        Skip.If(central.MotivoOmision is not null, central.MotivoOmision);
        using var cliente = central.CrearCliente();
        await central.CrearUsuarioAsync("TEMPORAL", "Temporal.Clave1", true, CatalogoPermisosCentral.AdministrarDispositivos);

        var (_, ingreso) = await CentralEnPruebas.IngresarAsync(cliente, "TEMPORAL", "Temporal.Clave1");
        Assert.True(ingreso!.Sesion!.DebeCambiarContrasena);

        using (var sinCambiar = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Post, $"/api/cajas/{CentralEnPruebas.CajaDos}/credencial", ingreso.TokenAcceso)))
            Assert.Equal(HttpStatusCode.Forbidden, sinCambiar.StatusCode);

        var corta = await CambiarContrasenaAsync(cliente, ingreso.TokenAcceso!, "Temporal.Clave1", "Corta.1");
        Assert.Equal(HttpStatusCode.BadRequest, corta.Estado);
        Assert.Equal("La contraseña debe tener al menos 10 caracteres.", corta.Cuerpo!.Mensaje);

        var simple = await CambiarContrasenaAsync(cliente, ingreso.TokenAcceso!, "Temporal.Clave1", "solominusculas");
        Assert.Equal("La contraseña debe combinar mayúsculas, minúsculas, números y símbolos.", simple.Cuerpo!.Mensaje);

        var conUsuario = await CambiarContrasenaAsync(cliente, ingreso.TokenAcceso!, "Temporal.Clave1", "Temporal.Nueva2");
        Assert.Equal("La contraseña no puede contener el nombre de usuario.", conUsuario.Cuerpo!.Mensaje);

        var incorrecta = await CambiarContrasenaAsync(cliente, ingreso.TokenAcceso!, "No.Es.La.Actual1", "Segura.Nueva#2026");
        Assert.Equal("La contraseña actual es incorrecta.", incorrecta.Cuerpo!.Mensaje);

        var cambio = await CambiarContrasenaAsync(cliente, ingreso.TokenAcceso!, "Temporal.Clave1", "Segura.Nueva#2026");
        Assert.Equal(HttpStatusCode.OK, cambio.Estado);
        Assert.False(cambio.Cuerpo!.Sesion!.DebeCambiarContrasena);
        Assert.NotEqual(ingreso.Sesion.SesionId, cambio.Cuerpo.Sesion.SesionId);

        // La sesión abierta con la contraseña temporal quedó cerrada.
        using (var anterior = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Get, "/api/sesion/actual", ingreso.TokenAcceso)))
            Assert.Equal(HttpStatusCode.Unauthorized, anterior.StatusCode);

        // Lo que se comprueba es que el permiso ya no lo frena. Si esa caja tiene credencial vigente el Central responde
        // que hay que revocarla primero, y eso también sirve: lo que no puede volver a salir es un 403.
        using var conPermiso = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Post, $"/api/cajas/{CentralEnPruebas.CajaDos}/credencial", cambio.Cuerpo.TokenAcceso));
        Assert.True(conPermiso.StatusCode is HttpStatusCode.OK or HttpStatusCode.Conflict, $"Respondió {conPermiso.StatusCode}.");
        Assert.True((await CentralEnPruebas.IngresarAsync(cliente, "TEMPORAL", "Segura.Nueva#2026")).Cuerpo!.Exitoso);
    }

    [SkippableFact]
    public async Task Usuario_sin_el_permiso_recibe_403()
    {
        Skip.If(central.MotivoOmision is not null, central.MotivoOmision);
        using var cliente = central.CrearCliente();
        await central.CrearUsuarioAsync("REPORTES", "Reportes.Clave1", false, CatalogoPermisosCentral.ConsultarReportes);
        var (_, ingreso) = await CentralEnPruebas.IngresarAsync(cliente, "REPORTES", "Reportes.Clave1");

        using var respuesta = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Post, $"/api/cajas/{CentralEnPruebas.CajaUno}/credencial", ingreso!.TokenAcceso));

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    [SkippableFact]
    public async Task Sin_la_vigencia_del_token_configurada_el_ingreso_se_rechaza_con_422()
    {
        Skip.If(central.MotivoOmision is not null, central.MotivoOmision);
        using var cliente = central.CrearCliente();
        var anterior = await central.CambiarParametroAsync(ClavesParametrosCentral.MinutosTokenAcceso, null);

        try
        {
            var (respuesta, _) = await CentralEnPruebas.IngresarAsync(cliente, "ADMIN", CentralEnPruebas.ContrasenaAdministrador);

            Assert.Equal(HttpStatusCode.UnprocessableEntity, respuesta.StatusCode);
            Assert.Contains(ClavesParametrosCentral.MinutosTokenAcceso, await respuesta.Content.ReadAsStringAsync());
        }
        finally
        {
            await central.CambiarParametroAsync(ClavesParametrosCentral.MinutosTokenAcceso, anterior);
        }
    }

    [SkippableFact]
    public async Task Las_api_solo_responden_por_https()
    {
        Skip.If(central.MotivoOmision is not null, central.MotivoOmision);
        using var cliente = central.CrearCliente(https: false);

        var (respuesta, cuerpo) = await CentralEnPruebas.IngresarAsync(cliente, "ADMIN", CentralEnPruebas.ContrasenaAdministrador);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Null(cuerpo);
        Assert.Contains("HTTPS", await respuesta.Content.ReadAsStringAsync());
    }

    private static async Task<(HttpStatusCode Estado, RespuestaSesionCentral? Cuerpo)> RenovarAsync(HttpClient cliente, string tokenRenovacion)
    {
        using var respuesta = await cliente.PostAsJsonAsync("/api/sesion/renovar", new SolicitudRenovacionSesion(tokenRenovacion), OpcionesJson.Predeterminadas);
        return (respuesta.StatusCode, await respuesta.Content.ReadFromJsonAsync<RespuestaSesionCentral>(OpcionesJson.Predeterminadas));
    }

    private static async Task<(HttpStatusCode Estado, RespuestaSesionCentral? Cuerpo)> CambiarContrasenaAsync(HttpClient cliente, string token, string actual, string nueva)
    {
        using var respuesta = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Post, "/api/sesion/contrasena", token, new SolicitudCambioContrasena(actual, nueva)));
        return (respuesta.StatusCode, await respuesta.Content.ReadFromJsonAsync<RespuestaSesionCentral>(OpcionesJson.Predeterminadas));
    }
}
