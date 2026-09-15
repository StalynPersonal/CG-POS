using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using CgPos.Contratos.Seguridad;
using CgPos.Contratos.Serializacion;
using CgPos.Dominio.Seguridad;
using CgPos.Pos.Agente.Seguridad;
using CgPos.Pos.Infraestructura;
using CgPos.Pos.Pruebas.Infraestructura;
using CgPos.Pos.Pruebas.Soporte;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;

namespace CgPos.Pos.Pruebas.Api;

/// <summary>
/// CG-POS Agente completo en memoria, con base temporal y los archivos de desarrollo: carga inicial
/// (usuarios C001/1111 cajero, S001/2222 supervisor, G001/3333 gerente), maestros y padrón DGII.
/// </summary>
public sealed class AgenteEnPruebas : IAsyncLifetime
{
    public const string CajaDesarrollo = "01990000-0000-7000-8000-000000000201";

    private readonly BaseDatosPruebas _baseDatos = new();

    // Se usa un tipo público del ensamblado del Agente como punto de entrada (evita ambigüedad con otros "Program").
    public WebApplicationFactory<EmisorTokens>? Fabrica { get; private set; }

    public string? MotivoOmision => _baseDatos.MotivoOmision;

    public async Task InitializeAsync()
    {
        await _baseDatos.InitializeAsync();
        if (MotivoOmision is not null)
            return;

        var datos = Path.Combine(RutasPrueba.RaizRepositorio(), "datos");
        var archivoLogs = Path.Combine(Path.GetTempPath(), "cgpos-pruebas", "agente-.log");

        Fabrica = new WebApplicationFactory<EmisorTokens>().WithWebHostBuilder(anfitrion =>
        {
            anfitrion.UseEnvironment("Pruebas");
            anfitrion.UseSetting($"ConnectionStrings:{InyeccionDependencias.NombreConexion}", _baseDatos.CadenaConexion);
            anfitrion.UseSetting("BaseDatos:NivelCompatibilidad", _baseDatos.NivelCompatibilidad);
            anfitrion.UseSetting("CargaInicial:Archivo", Path.Combine(datos, "carga-inicial.desarrollo.json"));
            anfitrion.UseSetting("Maestros:Archivo", Path.Combine(datos, "maestros.desarrollo.json"));
            anfitrion.UseSetting("Maestros:PadronDgii", Path.Combine(datos, "padron-dgii.desarrollo.txt"));
            anfitrion.UseSetting("Caja:Id", CajaDesarrollo);
            anfitrion.UseSetting("Agente:ServirPantallas", "false");
            anfitrion.UseSetting("Serilog:WriteTo:1:Args:path", archivoLogs);
        });

        // Arranca el Agente: aplica migraciones, carga inicial, maestros y padrón.
        _ = Fabrica.Server;
    }

    public async Task DisposeAsync()
    {
        if (Fabrica is not null)
            await Fabrica.DisposeAsync();

        await _baseDatos.DisposeAsync();
    }
}

[Collection(ColeccionAgente.Nombre)]
public class ApiSeguridadPruebas(AgenteEnPruebas agente)
{
    [SkippableFact]
    public async Task Estado_de_la_caja_es_publico_y_muestra_la_caja_configurada()
    {
        Skip.If(agente.MotivoOmision is not null, agente.MotivoOmision);
        using var cliente = agente.Fabrica!.CreateClient();

        var estado = await cliente.GetFromJsonAsync<DatosEstadoCaja>("/api/caja/estado", OpcionesJson.Predeterminadas);

        Assert.NotNull(estado);
        Assert.True(estado.Habilitada, estado.Problema);
        Assert.Equal("01", estado.CajaCodigo);
    }

    [SkippableFact]
    public async Task Ingreso_con_pin_entrega_un_token_valido_para_consultar_la_sesion()
    {
        Skip.If(agente.MotivoOmision is not null, agente.MotivoOmision);
        using var cliente = agente.Fabrica!.CreateClient();

        var ingreso = await IngresarAsync(cliente, "C001", "1111");
        Assert.True(ingreso.Exitoso, ingreso.Mensaje);
        Assert.False(string.IsNullOrEmpty(ingreso.Token));

        using var sinToken = await cliente.GetAsync("/api/sesion/actual");
        Assert.Equal(HttpStatusCode.Unauthorized, sinToken.StatusCode);

        using var solicitud = new HttpRequestMessage(HttpMethod.Get, "/api/sesion/actual");
        solicitud.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ingreso.Token);
        using var conToken = await cliente.SendAsync(solicitud);
        Assert.Equal(HttpStatusCode.OK, conToken.StatusCode);

        var sesion = await conToken.Content.ReadFromJsonAsync<DatosSesion>(OpcionesJson.Predeterminadas);
        Assert.Equal("C001", sesion!.Codigo);
        Assert.Contains(CatalogoPermisos.RegistrarVenta, sesion.Permisos);
    }

    [SkippableFact]
    public async Task Pin_incorrecto_responde_401_con_mensaje_generico()
    {
        Skip.If(agente.MotivoOmision is not null, agente.MotivoOmision);
        using var cliente = agente.Fabrica!.CreateClient();

        using var respuesta = await cliente.PostAsJsonAsync("/api/sesion/pin", new SolicitudIngresoPin("G001", "0000"), OpcionesJson.Predeterminadas);
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<RespuestaIngreso>(OpcionesJson.Predeterminadas);

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
        Assert.False(cuerpo!.Exitoso);
        Assert.Equal("Usuario o PIN incorrecto.", cuerpo.Mensaje);
        Assert.Null(cuerpo.Token);

        // Un ingreso correcto reinicia el contador de intentos.
        Assert.True((await IngresarAsync(cliente, "G001", "3333")).Exitoso);
    }

    [SkippableFact]
    public async Task Supervisor_autoriza_por_api_una_operacion_del_cajero()
    {
        Skip.If(agente.MotivoOmision is not null, agente.MotivoOmision);
        using var cliente = agente.Fabrica!.CreateClient();
        var cajero = await IngresarAsync(cliente, "C001", "1111");
        var solicitud = new SolicitudAutorizacion(CatalogoPermisos.EliminarLinea, "Artículo duplicado", "S001", "2222");

        using var sinToken = await cliente.PostAsJsonAsync("/api/autorizaciones", solicitud, OpcionesJson.Predeterminadas);
        Assert.Equal(HttpStatusCode.Unauthorized, sinToken.StatusCode);

        using var mensaje = new HttpRequestMessage(HttpMethod.Post, "/api/autorizaciones")
        {
            Content = JsonContent.Create(solicitud, options: OpcionesJson.Predeterminadas),
        };
        mensaje.Headers.Authorization = new AuthenticationHeaderValue("Bearer", cajero.Token);
        using var respuesta = await cliente.SendAsync(mensaje);
        var autorizacion = await respuesta.Content.ReadFromJsonAsync<RespuestaAutorizacion>(OpcionesJson.Predeterminadas);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.True(autorizacion!.Concedida, autorizacion.Mensaje);
        Assert.True(autorizacion.RequirioSupervisor);
        Assert.Equal("Supervisor Desarrollo", autorizacion.SupervisorNombre);
    }

    [SkippableFact]
    public async Task Politicas_por_permiso_se_evaluan_con_los_atributos_del_token()
    {
        Skip.If(agente.MotivoOmision is not null, agente.MotivoOmision);
        using var cliente = agente.Fabrica!.CreateClient();
        var cajero = await IngresarAsync(cliente, "C001", "1111");

        var emisor = agente.Fabrica.Services.GetRequiredService<EmisorTokens>();
        var validacion = await new JsonWebTokenHandler().ValidateTokenAsync(cajero.Token, emisor.ParametrosValidacion());
        Assert.True(validacion.IsValid, validacion.Exception?.Message);

        var usuario = new ClaimsPrincipal(validacion.ClaimsIdentity);
        var autorizacion = agente.Fabrica.Services.GetRequiredService<IAuthorizationService>();

        Assert.True((await autorizacion.AuthorizeAsync(usuario, CatalogoPermisos.RegistrarVenta)).Succeeded);
        Assert.False((await autorizacion.AuthorizeAsync(usuario, CatalogoPermisos.EliminarLinea)).Succeeded);
    }

    private static async Task<RespuestaIngreso> IngresarAsync(HttpClient cliente, string codigo, string pin)
    {
        using var respuesta = await cliente.PostAsJsonAsync("/api/sesion/pin", new SolicitudIngresoPin(codigo, pin), OpcionesJson.Predeterminadas);
        return (await respuesta.Content.ReadFromJsonAsync<RespuestaIngreso>(OpcionesJson.Predeterminadas))!;
    }
}
