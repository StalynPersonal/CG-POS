using CgPos.Domain.Globalizacion;
using CgPos.Pos.Web;
using CgPos.UI.Kit.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddCgPosUi();

var host = builder.Build();

// Formato RD fijo (RD$ 2,175.34 · dd/MM/yyyy), independiente del idioma del navegador (RNF-27).
CulturaRd.Aplicar();

await host.RunAsync();
