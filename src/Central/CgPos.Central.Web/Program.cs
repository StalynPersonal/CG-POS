using CgPos.Central.Web;
using CgPos.Central.Web.Seguridad;
using CgPos.Contratos.Central;
using CgPos.Dominio.Globalizacion;
using CgPos.Dominio.Seguridad;
using CgPos.Interfaz.Servicios;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var constructor = WebAssemblyHostBuilder.CreateDefault(args);
constructor.RootComponents.Add<Aplicacion>("#aplicacion");
constructor.RootComponents.Add<HeadOutlet>("head::after");

constructor.Services.AgregarInterfazCgPos();

// Sesión y API del Central (mismo origen que el Central Manager).
var origen = new Uri(constructor.HostEnvironment.BaseAddress);
constructor.Services.AddSingleton<AlmacenSesionCentral>();
constructor.Services.AddSingleton<ServicioSesionCentral>();
constructor.Services.AddTransient<ManejadorTokenCentral>();
constructor.Services.AddHttpClient(ServicioSesionCentral.NombreHttpSinSesion, cliente => cliente.BaseAddress = origen);
constructor.Services.AddHttpClient(ServicioSesionCentral.NombreHttp, cliente => cliente.BaseAddress = origen).AddHttpMessageHandler<ManejadorTokenCentral>();
constructor.Services.AddScoped<ClienteCentral>();
constructor.Services.AddScoped<ClienteCuadre>();

// Una política por permiso del catálogo del Central, igual que en la API.
constructor.Services.AddAuthorizationCore(opciones =>
{
    foreach (var permiso in CatalogoPermisosCentral.Todos)
        opciones.AddPolicy(permiso.Codigo, politica => politica.RequireClaim(AtributosTokenCentral.Permiso, permiso.Codigo));
});
constructor.Services.AddCascadingAuthenticationState();
constructor.Services.AddScoped<AuthenticationStateProvider, EstadoAutenticacionCentral>();

var anfitrion = constructor.Build();

// Formato RD fijo (2,175.34 · dd/MM/yyyy), independiente del idioma del navegador.
CulturaRd.Aplicar(null);

// Recargar la página conserva la sesión: se renueva con el token de renovación guardado en esta pestaña.
await anfitrion.Services.GetRequiredService<ServicioSesionCentral>().RestaurarAsync();

await anfitrion.RunAsync();
