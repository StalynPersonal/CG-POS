using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using CgPos.Contratos.Seguridad;
using CgPos.Contratos.Ventas;
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
/// (usuarios C001/1111 cajero, S001/2222 supervisor, G001/3333 gerente) y maestros.
/// </summary>
public sealed class AgenteEnPruebas : IAsyncLifetime
{
    /// <summary>La caja 01 de la sucursal 01 de los datos de desarrollo.</summary>
    public const int SucursalDesarrollo = 1;

    public const int CajaDesarrollo = 1;

    private readonly BaseDatosPruebas _baseDatos = new();

    // Se usa un tipo público del ensamblado del Agente como punto de entrada (evita ambigüedad con otros "Program").
    public WebApplicationFactory<EmisorTokens>? Fabrica { get; private set; }

    public string? MotivoOmision => _baseDatos.MotivoOmision;

    public async Task InitializeAsync()
    {
        await _baseDatos.InitializeAsync();
        if (MotivoOmision is not null)
            return;

        // Los datos de prueba viven en el proyecto de pruebas del Central.
        var datosPruebas = Path.Combine(RutasPrueba.RaizRepositorio(), "pruebas", "CgPos.Central.Pruebas", "Datos");
        var datos = Path.Combine(RutasPrueba.RaizRepositorio(), "datos");
        var archivoLogs = Path.Combine(Path.GetTempPath(), "cgpos-pruebas", "agente-.log");

        Fabrica = new WebApplicationFactory<EmisorTokens>().WithWebHostBuilder(anfitrion =>
        {
            anfitrion.UseEnvironment("Pruebas");
            anfitrion.UseSetting($"ConnectionStrings:{InyeccionDependencias.NombreConexion}", _baseDatos.CadenaConexion);
            anfitrion.UseSetting("BaseDatos:NivelCompatibilidad", _baseDatos.NivelCompatibilidad);
            anfitrion.UseSetting("CargaInicial:Archivo", Path.Combine(datosPruebas, "carga-inicial.pruebas.json"));
            anfitrion.UseSetting("Maestros:Archivo", Path.Combine(datosPruebas, "maestros.pruebas.json"));
            // La caja ya no se identifica por archivo: se comprueba su dirección solo cuando el equipo la tiene de verdad.
            anfitrion.UseSetting("Caja:ValidarIpDelEquipo", "0");
            // Sin un Central de verdad, la configuración de la caja no se valida contra nadie.
            anfitrion.UseSetting("Central:Modo", "Simulado");
            anfitrion.UseSetting("Agente:ServirPantallas", "false");
            anfitrion.UseSetting("Perifericos:Impresora:Carpeta", _baseDatos.CarpetaImpresiones);
            anfitrion.UseSetting("Ecf:CarpetaXml", _baseDatos.CarpetaEcf);

            // El mantenimiento en segundo plano no respalda la base de pruebas ni consulta servidores de hora.
            anfitrion.UseSetting("Respaldo:Hora", string.Empty);
            anfitrion.UseSetting("Reloj:ServidorNtp", string.Empty);
            anfitrion.UseSetting("Ecf:Certificado:Ruta", _baseDatos.RutaCertificado);
            anfitrion.UseSetting("Serilog:WriteTo:1:Args:path", archivoLogs);
        });

        // Arranca el Agente: aplica migraciones, carga inicial y maestros.
        _ = Fabrica.Server;

        // Y se configura como lo haría el técnico en la pantalla de la caja: qué caja es, su dirección, su Central y su
        // credencial. Sin esto la caja no sabe cuál es, igual que una recién instalada.
        await ConfigurarCajaAsync();
    }

    /// <summary>Deja la caja configurada como la 01 de la sucursal 01, que es la de los datos de prueba.</summary>
    private async Task ConfigurarCajaAsync()
    {
        using var cliente = Fabrica!.CreateClient();
        var solicitud = new SolicitudConfigurarCajaPantalla(
            SucursalDesarrollo.ToString("00", System.Globalization.CultureInfo.InvariantCulture),
            CajaDesarrollo.ToString("00", System.Globalization.CultureInfo.InvariantCulture),
            "10.12.1.101",
            "http://central.pruebas/",
            "secreto-de-pruebas",
            "ADMIN",
            "Clave.Pruebas");

        using var respuesta = await cliente.PostAsJsonAsync("/api/configuracion", solicitud, OpcionesJson.Predeterminadas);
        respuesta.EnsureSuccessStatusCode();
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
    public async Task Ingreso_con_clave_entrega_un_token_valido_para_consultar_la_sesion()
    {
        Skip.If(agente.MotivoOmision is not null, agente.MotivoOmision);
        using var cliente = agente.Fabrica!.CreateClient();

        var ingreso = await IngresarAsync(cliente, "C001", "Cajero.2026");
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
    public async Task Clave_incorrecta_responde_401_con_mensaje_generico()
    {
        Skip.If(agente.MotivoOmision is not null, agente.MotivoOmision);
        using var cliente = agente.Fabrica!.CreateClient();

        using var respuesta = await cliente.PostAsJsonAsync("/api/sesion/ingreso", new SolicitudIngreso("G001", "0000"), OpcionesJson.Predeterminadas);
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<RespuestaIngreso>(OpcionesJson.Predeterminadas);

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
        Assert.False(cuerpo!.Exitoso);
        Assert.Equal("Usuario o clave incorrectos.", cuerpo.Mensaje);
        Assert.Null(cuerpo.Token);

        // Un ingreso correcto reinicia el contador de intentos.
        Assert.True((await IngresarAsync(cliente, "G001", "Gerente.2026")).Exitoso);
    }

    [SkippableFact]
    public async Task Supervisor_autoriza_por_api_una_operacion_del_cajero()
    {
        Skip.If(agente.MotivoOmision is not null, agente.MotivoOmision);
        using var cliente = agente.Fabrica!.CreateClient();
        var cajero = await IngresarAsync(cliente, "C001", "Cajero.2026");
        var solicitud = new SolicitudAutorizacion(CatalogoPermisos.EliminarLinea, "Artículo duplicado", "S001", "Supervisor.2026");

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
        var cajero = await IngresarAsync(cliente, "C001", "Cajero.2026");

        var emisor = agente.Fabrica.Services.GetRequiredService<EmisorTokens>();
        var validacion = await new JsonWebTokenHandler().ValidateTokenAsync(cajero.Token, emisor.ParametrosValidacion());
        Assert.True(validacion.IsValid, validacion.Exception?.Message);

        var usuario = new ClaimsPrincipal(validacion.ClaimsIdentity);
        var autorizacion = agente.Fabrica.Services.GetRequiredService<IAuthorizationService>();

        Assert.True((await autorizacion.AuthorizeAsync(usuario, CatalogoPermisos.RegistrarVenta)).Succeeded);
        Assert.False((await autorizacion.AuthorizeAsync(usuario, CatalogoPermisos.EliminarLinea)).Succeeded);
    }

    private static async Task<RespuestaIngreso> IngresarAsync(HttpClient cliente, string codigo, string clave)
    {
        using var respuesta = await cliente.PostAsJsonAsync("/api/sesion/ingreso", new SolicitudIngreso(codigo, clave), OpcionesJson.Predeterminadas);
        return (await respuesta.Content.ReadFromJsonAsync<RespuestaIngreso>(OpcionesJson.Predeterminadas))!;
    }
}
