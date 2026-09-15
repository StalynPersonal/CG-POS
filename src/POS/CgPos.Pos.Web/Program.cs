using CgPos.Contratos.Seguridad;
using CgPos.Dominio.Globalizacion;
using CgPos.Dominio.Seguridad;
using CgPos.Interfaz.Servicios;
using CgPos.Pos.Web;
using CgPos.Pos.Web.Seguridad;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var constructor = WebAssemblyHostBuilder.CreateDefault(args);
constructor.RootComponents.Add<Aplicacion>("#aplicacion");
constructor.RootComponents.Add<HeadOutlet>("head::after");

constructor.Services.AgregarInterfazCgPos();

// Sesión y comunicación con CG-POS Agente (mismo origen que la pantalla).
constructor.Services.AddSingleton<AlmacenSesion>();
constructor.Services.AddTransient<ManejadorTokenAgente>();
constructor.Services
    .AddHttpClient(ClienteAgente.NombreHttp, cliente => cliente.BaseAddress = new Uri(constructor.HostEnvironment.BaseAddress))
    .AddHttpMessageHandler<ManejadorTokenAgente>();
constructor.Services.AddScoped<ClienteAgente>();
constructor.Services.AddScoped<ServicioAutorizacionPantalla>();

// Una política por permiso del catálogo, igual que en el Agente.
constructor.Services.AddAuthorizationCore(opciones =>
{
    foreach (var permiso in CatalogoPermisos.Todos)
        opciones.AddPolicy(permiso.Codigo, politica => politica.RequireClaim(AtributosToken.Permiso, permiso.Codigo));
});
constructor.Services.AddCascadingAuthenticationState();
constructor.Services.AddScoped<AuthenticationStateProvider, EstadoAutenticacionCaja>();

var anfitrion = constructor.Build();

// Formato RD fijo (2,175.34 · dd/MM/yyyy), independiente del idioma del navegador (RNF-27), con el símbolo
// de la moneda local que el Central configuró para esta caja.
await using (var ambito = anfitrion.Services.CreateAsyncScope())
{
    var estado = await ambito.ServiceProvider.GetRequiredService<ClienteAgente>().ObtenerEstadoCajaAsync();
    CulturaRd.Aplicar(estado?.MonedaLocal?.Simbolo);
}

await anfitrion.RunAsync();
