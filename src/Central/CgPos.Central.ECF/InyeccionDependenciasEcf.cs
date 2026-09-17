using CgPos.Central.Aplicacion.Dgii;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CgPos.Central.ECF;

/// <summary>
/// Facturación electrónica del Central (M09): el despacho de los e-CF recibidos a la DGII, su cliente y el trabajo en segundo plano.
/// Va en su propio ensamblado para poder actualizarla o certificarla sin tocar el resto del Central.
/// </summary>
public static class InyeccionDependenciasEcf
{
    /// <summary>Sin <c>Dgii:Cliente</c> se usa la DGII real; el simulador es solo para desarrollo.</summary>
    public static IServiceCollection AgregarEcfCentral(this IServiceCollection servicios, IConfiguration configuracion)
    {
        ArgumentNullException.ThrowIfNull(servicios);
        ArgumentNullException.ThrowIfNull(configuracion);

        var opciones = new OpcionesDgii(configuracion["Dgii:Cliente"], configuracion["Dgii:Certificado:Ruta"], configuracion["Dgii:Certificado:Pin"]);
        servicios.AddSingleton(opciones);
        servicios.AddScoped<IDespachadorDgii, DespachadorDgii>();
        servicios.AddScoped<IServicioAnulacionesEcf, ServicioAnulacionesEcf>();

        if (opciones.UsaSimulador)
        {
            servicios.AddSingleton<IClienteDgii, ClienteDgiiSimulado>();
        }
        else
        {
            servicios.AddSingleton<SesionDgii>();
            servicios.AddHttpClient<IClienteDgii, ClienteDgiiHttp>(cliente => cliente.Timeout = TimeSpan.FromSeconds(60));
        }

        if (!string.Equals(configuracion["Dgii:TrabajadorHabilitado"], "false", StringComparison.OrdinalIgnoreCase))
            servicios.AddHostedService<TrabajadorDgii>();

        return servicios;
    }
}
