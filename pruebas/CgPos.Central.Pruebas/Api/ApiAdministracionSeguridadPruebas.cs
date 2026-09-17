using System.Net;
using System.Net.Http.Json;
using CgPos.Central.Pruebas.Soporte;
using CgPos.Contratos.Central;
using CgPos.Contratos.Serializacion;
using CgPos.Dominio.Seguridad;
using Microsoft.EntityFrameworkCore;

namespace CgPos.Central.Pruebas.Api;

[Collection(ColeccionCentral.Nombre)]
public class ApiAdministracionSeguridadPruebas(CentralEnPruebas central)
{
    private const string Temporal = "Temporal.Clave#2026";

    /// <summary>Usuario ADMIN y rol ADMINISTRADOR de los datos de desarrollo, con los Id que les dio el Central al cargarlos.</summary>
    private int Administrador => central.UsarContextoAsync(contexto => contexto.UsuariosCentral.Where(u => u.Codigo == "ADMIN").Select(u => u.Id).SingleAsync())
        .GetAwaiter().GetResult();

    private int RolAdministrador => central.UsarContextoAsync(contexto => contexto.RolesCentral.Where(r => r.Codigo == "ADMINISTRADOR").Select(r => r.Id).SingleAsync())
        .GetAwaiter().GetResult();

    [SkippableFact]
    public async Task Administrador_crea_rol_y_usuario_que_ingresa_con_contrasena_temporal()
    {
        Skip.If(central.MotivoOmision is not null, central.MotivoOmision);
        using var cliente = central.CrearCliente();
        var admin = await CentralEnPruebas.TokenAdministradorAsync(cliente);
        var sufijo = Sufijo();

        var (rolId, codigo) = await CrearUsuarioConRolAsync(cliente, admin, sufijo, CatalogoPermisosCentral.ConsultarReportes);

        var roles = await ListarAsync<DatosRolCentral>(cliente, admin, "/api/seguridad/roles");
        Assert.Equal(1, Assert.Single(roles, r => r.Id == rolId).Usuarios);
        var creado = Assert.Single(await ListarAsync<DatosUsuarioCentral>(cliente, admin, "/api/seguridad/usuarios"), u => u.Codigo == codigo);
        Assert.True(creado.DebeCambiarContrasena);
        Assert.Equal("Reportes " + sufijo, creado.RolNombre);

        var (_, ingreso) = await CentralEnPruebas.IngresarAsync(cliente, codigo, Temporal);
        Assert.True(ingreso!.Sesion!.DebeCambiarContrasena);
        Assert.Equal([CatalogoPermisosCentral.ConsultarReportes], ingreso.Sesion.Permisos);

        Assert.Contains(await ListarAsync<DatosPermisoCentral>(cliente, admin, "/api/seguridad/permisos"), p => p.Codigo == CatalogoPermisosCentral.AdministrarSeguridad);
    }

    [SkippableFact]
    public async Task Politica_de_contrasena_codigos_repetidos_y_permisos_inexistentes_se_rechazan()
    {
        Skip.If(central.MotivoOmision is not null, central.MotivoOmision);
        using var cliente = central.CrearCliente();
        var admin = await CentralEnPruebas.TokenAdministradorAsync(cliente);
        var sufijo = Sufijo();

        var corta = await EnviarAsync(cliente, admin, HttpMethod.Post, "/api/seguridad/usuarios",
            new SolicitudUsuarioCentral($"C{sufijo}", "Contraseña corta", null, RolAdministrador, "Corta.1"));
        Assert.Equal(HttpStatusCode.BadRequest, corta.Estado);
        Assert.Equal("La contraseña debe tener al menos 10 caracteres.", corta.Cuerpo!.Mensaje);

        var repetido = await EnviarAsync(cliente, admin, HttpMethod.Post, "/api/seguridad/usuarios",
            new SolicitudUsuarioCentral("ADMIN", "Otro administrador", null, RolAdministrador, Temporal));
        Assert.Equal("Ya existe el usuario 'ADMIN'.", repetido.Cuerpo!.Mensaje);

        var rolRepetido = await EnviarAsync(cliente, admin, HttpMethod.Post, "/api/seguridad/roles", new SolicitudRolCentral("ADMINISTRADOR", "Otro", []));
        Assert.Equal(HttpStatusCode.BadRequest, rolRepetido.Estado);

        var permisoInexistente = await EnviarAsync(cliente, admin, HttpMethod.Post, "/api/seguridad/roles", new SolicitudRolCentral($"R{sufijo}", "Rol", ["Central.No.Existe"]));
        Assert.Contains("no existe en el catálogo del Central", permisoInexistente.Cuerpo!.Mensaje);

        var inexistente = await EnviarAsync(cliente, admin, HttpMethod.Put, $"/api/seguridad/roles/{Ids.Siguiente()}", new SolicitudRolCentral("X", "X", []));
        Assert.Equal(HttpStatusCode.NotFound, inexistente.Estado);

        Assert.DoesNotContain(await ListarAsync<DatosUsuarioCentral>(cliente, admin, "/api/seguridad/usuarios"), u => u.Codigo == $"C{sufijo}");
    }

    [SkippableFact]
    public async Task No_se_puede_dejar_al_central_sin_administrador_de_seguridad()
    {
        Skip.If(central.MotivoOmision is not null, central.MotivoOmision);
        using var cliente = central.CrearCliente();
        var admin = await CentralEnPruebas.TokenAdministradorAsync(cliente);

        var propio = await EnviarAsync(cliente, admin, HttpMethod.Post, $"/api/seguridad/usuarios/{Administrador}/desactivar");
        Assert.Equal((HttpStatusCode.BadRequest, "No puede desactivar su propio usuario."), (propio.Estado, propio.Cuerpo!.Mensaje));

        var sinSeguridad = await EnviarAsync(cliente, admin, HttpMethod.Put, $"/api/seguridad/roles/{RolAdministrador}", new SolicitudRolCentral("ADMINISTRADOR",
            "Administrador del Central", CatalogoPermisosCentral.Todos.Select(p => p.Codigo).Where(c => c != CatalogoPermisosCentral.AdministrarSeguridad).ToList()));
        Assert.Equal(HttpStatusCode.BadRequest, sinSeguridad.Estado);
        Assert.Contains("sin ningún usuario activo", sinSeguridad.Cuerpo!.Mensaje);

        var rolDesactivado = await EnviarAsync(cliente, admin, HttpMethod.Post, $"/api/seguridad/roles/{RolAdministrador}/desactivar");
        Assert.Equal(HttpStatusCode.BadRequest, rolDesactivado.Estado);

        var rol = Assert.Single(await ListarAsync<DatosRolCentral>(cliente, admin, "/api/seguridad/roles"), r => r.Id == RolAdministrador);
        Assert.True(rol.Activo);
        Assert.Contains(CatalogoPermisosCentral.AdministrarSeguridad, rol.Permisos);
    }

    [SkippableFact]
    public async Task Restablecer_desactivar_y_desbloquear_aplican_al_ingreso_y_cierran_sesiones()
    {
        Skip.If(central.MotivoOmision is not null, central.MotivoOmision);
        using var cliente = central.CrearCliente();
        var admin = await CentralEnPruebas.TokenAdministradorAsync(cliente);
        var (_, codigo) = await CrearUsuarioConRolAsync(cliente, admin, Sufijo(), CatalogoPermisosCentral.ConsultarReportes);
        var usuarioId = Assert.Single(await ListarAsync<DatosUsuarioCentral>(cliente, admin, "/api/seguridad/usuarios"), u => u.Codigo == codigo).Id;

        var primera = (await CentralEnPruebas.IngresarAsync(cliente, codigo, Temporal)).Cuerpo!.TokenAcceso;
        const string otraTemporal = "Otra.Temporal#2026";
        Assert.True((await EnviarAsync(cliente, admin, HttpMethod.Post, $"/api/seguridad/usuarios/{usuarioId}/contrasena", new SolicitudContrasenaTemporal(otraTemporal))).Cuerpo!.Exitosa);
        Assert.Equal(HttpStatusCode.Unauthorized, await EstadoSesionAsync(cliente, primera));
        Assert.False((await CentralEnPruebas.IngresarAsync(cliente, codigo, Temporal)).Cuerpo!.Exitoso);

        var segunda = (await CentralEnPruebas.IngresarAsync(cliente, codigo, otraTemporal)).Cuerpo!.TokenAcceso;
        Assert.True((await EnviarAsync(cliente, admin, HttpMethod.Post, $"/api/seguridad/usuarios/{usuarioId}/desactivar")).Cuerpo!.Exitosa);
        Assert.Equal(HttpStatusCode.Unauthorized, await EstadoSesionAsync(cliente, segunda));
        Assert.Equal("El usuario está inactivo. Contacte al administrador.", (await CentralEnPruebas.IngresarAsync(cliente, codigo, otraTemporal)).Cuerpo!.Mensaje);

        Assert.True((await EnviarAsync(cliente, admin, HttpMethod.Post, $"/api/seguridad/usuarios/{usuarioId}/activar")).Cuerpo!.Exitosa);
        for (var intento = 0; intento < 5; intento++)
            await CentralEnPruebas.IngresarAsync(cliente, codigo, "No.Es.La.Clave1");
        Assert.StartsWith("Usuario bloqueado", (await CentralEnPruebas.IngresarAsync(cliente, codigo, otraTemporal)).Cuerpo!.Mensaje);

        Assert.True((await EnviarAsync(cliente, admin, HttpMethod.Post, $"/api/seguridad/usuarios/{usuarioId}/desbloquear")).Cuerpo!.Exitosa);
        Assert.True((await CentralEnPruebas.IngresarAsync(cliente, codigo, otraTemporal)).Cuerpo!.Exitoso);
    }

    [SkippableFact]
    public async Task Sin_el_permiso_de_seguridad_no_se_administran_usuarios()
    {
        Skip.If(central.MotivoOmision is not null, central.MotivoOmision);
        using var cliente = central.CrearCliente();
        var codigo = $"SINSEG{Sufijo()}";
        await central.CrearUsuarioAsync(codigo, "Sin.Seguridad#2026", false, CatalogoPermisosCentral.ConsultarReportes);
        var token = (await CentralEnPruebas.IngresarAsync(cliente, codigo, "Sin.Seguridad#2026")).Cuerpo!.TokenAcceso;

        using var respuesta = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Get, "/api/seguridad/usuarios", token));

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    private static string Sufijo() => Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();

    private static async Task<(int RolId, string Codigo)> CrearUsuarioConRolAsync(HttpClient cliente, string token, string sufijo, params string[] permisos)
    {
        var rol = await EnviarAsync(cliente, token, HttpMethod.Post, "/api/seguridad/roles", new SolicitudRolCentral($"REP{sufijo}", "Reportes " + sufijo, permisos));
        Assert.True(rol.Cuerpo!.Exitosa, rol.Cuerpo.Mensaje);

        var codigo = $"U{sufijo}";
        var usuario = await EnviarAsync(cliente, token, HttpMethod.Post, "/api/seguridad/usuarios",
            new SolicitudUsuarioCentral(codigo, "Usuario " + sufijo, "usuario@empresa.do", rol.Cuerpo.Id!.Value, Temporal));
        Assert.True(usuario.Cuerpo!.Exitosa, usuario.Cuerpo.Mensaje);

        return (rol.Cuerpo.Id.Value, codigo);
    }

    private static async Task<HttpStatusCode> EstadoSesionAsync(HttpClient cliente, string? token)
    {
        using var respuesta = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Get, "/api/sesion/actual", token));
        return respuesta.StatusCode;
    }

    private static async Task<(HttpStatusCode Estado, RespuestaAdministracion? Cuerpo)> EnviarAsync(HttpClient cliente, string token, HttpMethod metodo, string ruta, object? cuerpo = null)
    {
        using var respuesta = await cliente.SendAsync(CentralEnPruebas.Solicitud(metodo, ruta, token, cuerpo));
        var datos = respuesta.Content.Headers.ContentType?.MediaType == "application/json"
            ? await respuesta.Content.ReadFromJsonAsync<RespuestaAdministracion>(OpcionesJson.Predeterminadas)
            : null;
        return (respuesta.StatusCode, datos);
    }

    private static async Task<List<T>> ListarAsync<T>(HttpClient cliente, string token, string ruta)
    {
        using var respuesta = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Get, ruta, token));
        respuesta.EnsureSuccessStatusCode();
        return (await respuesta.Content.ReadFromJsonAsync<List<T>>(OpcionesJson.Predeterminadas))!;
    }
}
