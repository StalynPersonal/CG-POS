using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using CgPos.Central.Api.Seguridad;
using CgPos.Central.Aplicacion.Abstracciones;
using CgPos.Central.Aplicacion.Dgii;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection.Extensions;
using CgPos.Central.Infraestructura;
using CgPos.Central.Infraestructura.Persistencia;
using CgPos.Contratos.Central;
using CgPos.Contratos.Serializacion;
using CgPos.Dominio.Organizacion;
using CgPos.Dominio.Seguridad;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CgPos.Central.Pruebas.Soporte;

/// <summary>
/// CG-POS Central completo en memoria con una base SQL Server temporal y los datos de <c>Datos/</c> de este proyecto
/// (ADMIN / Admin.Central2026). Exige HTTPS como en producción: los clientes usan https://localhost.
/// Configuración: user-secrets compartidos con CgPos.Pos.Pruebas o variables de entorno CGPOS_; sin servidor, las pruebas se omiten.
/// </summary>
public sealed class CentralEnPruebas : IAsyncLifetime
{
    public const string ContrasenaAdministrador = "Admin.Central2026";
    /// <summary>Sucursal 01 y sus cajas 01 y 02 de los datos de desarrollo, con los Id que les dio el Central al cargarlos.</summary>
    public static int CajaUno { get; private set; }
    public static int CajaDos { get; private set; }
    public static int Sucursal { get; private set; }

    private static CentralEnPruebas? _instancia;

    // Se usa un tipo público del ensamblado de la API como punto de entrada.
    public WebApplicationFactory<EmisorTokensCentral>? Fabrica { get; private set; }

    public string? MotivoOmision { get; private set; }

    public async Task InitializeAsync()
    {
        var configuracion = new ConfigurationBuilder()
            .AddUserSecrets<CentralEnPruebas>(optional: true)
            .AddEnvironmentVariables("CGPOS_")
            .Build();

        var servidor = configuracion.GetConnectionString("ServidorPruebas");
        if (string.IsNullOrWhiteSpace(servidor))
        {
            MotivoOmision = "No hay servidor SQL de pruebas configurado (ConnectionStrings:ServidorPruebas).";
            return;
        }

        var cadenaConexion = new SqlConnectionStringBuilder(servidor) { InitialCatalog = $"CgPosCentralPruebas_{Guid.NewGuid():N}" }.ConnectionString;

        // La base se crea con el mismo script que se usa en producción: así el script queda probado en cada corrida.
        await CgPos.Pruebas.Compartido.EsquemaBaseDatosPruebas.CrearAsync(
            cadenaConexion, CgPos.Pruebas.Compartido.EsquemaBaseDatosPruebas.ScriptCentral(RutasPrueba.RaizRepositorio()),
            sinAdministrador: true);
        var archivoLogs = Path.Combine(Path.GetTempPath(), "cgpos-pruebas", "central-.log");

        Fabrica = new WebApplicationFactory<EmisorTokensCentral>().WithWebHostBuilder(anfitrion =>
        {
            anfitrion.UseEnvironment("Pruebas");
            anfitrion.UseSetting($"ConnectionStrings:{InyeccionDependencias.NombreConexion}", cadenaConexion);
            anfitrion.UseSetting("BaseDatos:NivelCompatibilidad", configuracion["BaseDatos:NivelCompatibilidad"]);
            anfitrion.UseSetting("CargaInicial:Archivo", RutasPrueba.Datos("central.pruebas.json"));
            anfitrion.UseSetting("CargaInicialCajas:Archivo", RutasPrueba.Datos("carga-inicial.pruebas.json"));
            anfitrion.UseSetting("Maestros:Archivo", RutasPrueba.Datos("maestros.pruebas.json"));
            anfitrion.UseSetting(EmisorTokensCentral.ClaveConfiguracion, Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
            anfitrion.UseSetting(ExtensionesSeguridadCentral.ClaveExigirHttps, "true");
            anfitrion.UseSetting("Central:ServirManager", "false");
            anfitrion.UseSetting("Serilog:WriteTo:1:Args:path", archivoLogs);

            // Las pruebas ejecutan el despacho a la DGII cuando lo necesitan, contra una DGII de prueba.
            anfitrion.UseSetting("Dgii:TrabajadorHabilitado", "false");
            anfitrion.ConfigureTestServices(servicios =>
            {
                servicios.RemoveAll<IClienteDgii>();
                servicios.AddSingleton<ClienteDgiiPrueba>();
                servicios.AddSingleton<IClienteDgii>(proveedor => proveedor.GetRequiredService<ClienteDgiiPrueba>());
            });
        });

        // Arranca el Central: verifica la base y aplica la carga inicial.
        _ = Fabrica.Server;
        _instancia = this;

        (Sucursal, CajaUno, CajaDos) = await UsarContextoAsync(async contexto =>
        {
            var sucursal = await contexto.Sucursales.Where(s => s.Codigo == "01").Select(s => s.Id).SingleAsync();
            var cajas = await contexto.Cajas.Where(c => c.SucursalId == sucursal).ToDictionaryAsync(c => c.Codigo, c => c.Id);
            return (sucursal, cajas["01"], cajas["02"]);
        });
    }

    public async Task DisposeAsync()
    {
        if (Fabrica is null)
            return;

        await using (var ambito = Fabrica.Services.CreateAsyncScope())
        {
            await ambito.ServiceProvider.GetRequiredService<ContextoDatosCentral>().Database.EnsureDeletedAsync();
        }

        await Fabrica.DisposeAsync();
    }

    public ClienteDgiiPrueba Dgii => Fabrica!.Services.GetRequiredService<ClienteDgiiPrueba>();

    public async Task<ResultadoCicloDgii> ProcesarDgiiAsync()
    {
        await using var ambito = Fabrica!.Services.CreateAsyncScope();
        return await ambito.ServiceProvider.GetRequiredService<IDespachadorDgii>().ProcesarAsync();
    }

    public HttpClient CrearCliente(bool https = true) =>
        Fabrica!.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri(https ? "https://localhost" : "http://localhost") });

    public async Task<T> UsarContextoAsync<T>(Func<ContextoDatosCentral, Task<T>> accion)
    {
        await using var ambito = Fabrica!.Services.CreateAsyncScope();
        return await accion(ambito.ServiceProvider.GetRequiredService<ContextoDatosCentral>());
    }

    /// <summary>Crea un usuario del Central con un rol propio que tiene exactamente <paramref name="permisos"/>.</summary>
    public async Task CrearUsuarioAsync(string codigo, string contrasena, bool debeCambiarContrasena, params string[] permisos)
    {
        await using var ambito = Fabrica!.Services.CreateAsyncScope();
        var contexto = ambito.ServiceProvider.GetRequiredService<ContextoDatosCentral>();
        var hash = ambito.ServiceProvider.GetRequiredService<IHashContrasenas>();

        var rol = RolCentral.Crear($"R-{codigo}", $"Rol de {codigo}");
        foreach (var permiso in permisos)
            rol.AsignarPermiso(permiso);

        contexto.RolesCentral.Add(rol);
        contexto.UsuariosCentral.Add(UsuarioCentral.Crear(codigo, $"Usuario {codigo}", null, rol.Id, hash.Hash(contrasena), debeCambiarContrasena));
        await contexto.SaveChangesAsync();
    }

    /// <summary>Cambia (o quita, con <c>null</c>) un parámetro general y devuelve el valor anterior.</summary>
    public Task<string?> CambiarParametroAsync(string clave, string? valor) =>
        UsarContextoAsync(async contexto =>
        {
            var parametro = await contexto.Parametros.SingleOrDefaultAsync(p => p.Clave == clave && p.SucursalId == null && p.CajaId == null);
            var anterior = parametro?.Valor;

            if (valor is null && parametro is not null)
                contexto.Parametros.Remove(parametro);
            else if (valor is not null && parametro is null)
                contexto.Parametros.Add(Parametro.Crear(clave, valor));
            else
                parametro?.CambiarValor(valor!);

            await contexto.SaveChangesAsync();
            return anterior;
        });

    public static async Task<(HttpResponseMessage Respuesta, RespuestaSesionCentral? Cuerpo)> IngresarAsync(HttpClient cliente, string usuario, string contrasena)
    {
        var respuesta = await cliente.PostAsJsonAsync("/api/sesion/ingreso", new SolicitudIngresoCentral(usuario, contrasena), OpcionesJson.Predeterminadas);
        var cuerpo = respuesta.Content.Headers.ContentType?.MediaType == "application/json"
            ? await respuesta.Content.ReadFromJsonAsync<RespuestaSesionCentral>(OpcionesJson.Predeterminadas)
            : null;
        return (respuesta, cuerpo);
    }

    public static async Task<string> TokenAdministradorAsync(HttpClient cliente) =>
        (await IngresarAsync(cliente, "ADMIN", ContrasenaAdministrador)).Cuerpo!.TokenAcceso!;

    /// <summary>
    /// Deja la caja con una credencial recién emitida y devuelve su secreto. Si ya tenía una, la revoca primero: el
    /// Central no emite dos credenciales vigentes para la misma caja.
    /// </summary>
    public static async Task<string> EmitirCredencialAsync(HttpClient cliente, int cajaId)
    {
        var token = await TokenAdministradorAsync(cliente);
        using var primera = await cliente.SendAsync(Solicitud(HttpMethod.Post, $"/api/cajas/{cajaId}/credencial", token));
        if (primera.StatusCode == HttpStatusCode.Conflict)
        {
            using var revocada = await cliente.SendAsync(Solicitud(HttpMethod.Post, $"/api/cajas/{cajaId}/credencial/revocar", token,
                new SolicitudRevocacionCredencial("Credencial nueva para la prueba")));
            // Responde 204 al revocar y 404 si ya no había ninguna vigente: las dos dejan la caja lista para la nueva.
            Assert.True(revocada.IsSuccessStatusCode || revocada.StatusCode == HttpStatusCode.NotFound);

            using var segunda = await cliente.SendAsync(Solicitud(HttpMethod.Post, $"/api/cajas/{cajaId}/credencial", token));
            segunda.EnsureSuccessStatusCode();
            return (await segunda.Content.ReadFromJsonAsync<DatosCredencialDispositivo>(OpcionesJson.Predeterminadas))!.Secreto;
        }

        primera.EnsureSuccessStatusCode();
        return (await primera.Content.ReadFromJsonAsync<DatosCredencialDispositivo>(OpcionesJson.Predeterminadas))!.Secreto;
    }

    /// <summary>Códigos de sucursal y caja con que se identifica una caja del Central (así viajan sus mensajes).</summary>
    public static (string Sucursal, string Caja) CodigosCaja(int cajaId)
    {
        var (sucursal, caja, _) = IdentidadCaja(cajaId);
        return (sucursal, caja);
    }

    /// <summary>Los tres datos con los que una caja se identifica ante el Central, además de su credencial.</summary>
    public static (string Sucursal, string Caja, string DireccionIp) IdentidadCaja(int cajaId) =>
        _instancia!.UsarContextoAsync(async contexto =>
        {
            var caja = await contexto.Cajas.AsNoTracking().SingleAsync(c => c.Id == cajaId);
            var sucursal = await contexto.Sucursales.AsNoTracking().Where(s => s.Id == caja.SucursalId).Select(s => s.Codigo).SingleAsync();
            return (sucursal, caja.Codigo, caja.DireccionIp);
        }).GetAwaiter().GetResult();

    /// <summary>Número de documento nuevo de esa caja (sucursal + caja + tipo + secuencia aleatoria), como los que numera la caja.</summary>
    public static string NumeroDocumento(int cajaId, CgPos.Dominio.Comun.TipoDocumentoNumerado tipo)
    {
        var (sucursal, caja) = CodigosCaja(cajaId);
        return CgPos.Dominio.Comun.NumeroDocumento.Formatear(sucursal, caja, tipo, Random.Shared.NextInt64(1, 999_999_999_999), 12);
    }

    public static async Task<string> TokenCajaAsync(HttpClient cliente, int cajaId)
    {
        var secreto = await EmitirCredencialAsync(cliente, cajaId);
        var (sucursal, caja, direccionIp) = IdentidadCaja(cajaId);
        using var respuesta = await cliente.PostAsJsonAsync("/api/dispositivos/token", new SolicitudTokenDispositivo(sucursal, caja, secreto, direccionIp),
            OpcionesJson.Predeterminadas);
        return (await respuesta.Content.ReadFromJsonAsync<RespuestaTokenDispositivo>(OpcionesJson.Predeterminadas))!.Token!;
    }

    public static HttpRequestMessage Solicitud(HttpMethod metodo, string ruta, string? token, object? cuerpo = null)
    {
        var solicitud = new HttpRequestMessage(metodo, ruta);
        if (token is not null)
            solicitud.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (cuerpo is not null)
            solicitud.Content = JsonContent.Create(cuerpo, cuerpo.GetType(), options: OpcionesJson.Predeterminadas);
        return solicitud;
    }
}

/// <summary>Todas las pruebas de API comparten un solo Central en memoria (Serilog no admite varios anfitriones en el mismo proceso).</summary>
[CollectionDefinition(Nombre)]
public sealed class ColeccionCentral : ICollectionFixture<CentralEnPruebas>
{
    public const string Nombre = "Central en pruebas";
}

public static class RutasPrueba
{
    /// <summary>Archivo de datos de las pruebas del Central (viven en este proyecto, no en el sistema).</summary>
    public static string Datos(string archivo) =>
        Path.Combine(RaizRepositorio(), "pruebas", "CgPos.Central.Pruebas", "Datos", archivo);

    public static string RaizRepositorio()
    {
        for (var carpeta = new DirectoryInfo(AppContext.BaseDirectory); carpeta is not null; carpeta = carpeta.Parent)
        {
            if (File.Exists(Path.Combine(carpeta.FullName, "CgPos.slnx")))
                return carpeta.FullName;
        }

        throw new InvalidOperationException("No se encontró la raíz del repositorio (CgPos.slnx).");
    }
}
