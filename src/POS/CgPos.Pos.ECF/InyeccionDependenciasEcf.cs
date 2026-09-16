using CgPos.Pos.Aplicacion.Ecf;
using CgPos.Pos.Aplicacion.Organizacion;
using CgPos.Pos.Infraestructura.Persistencia;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CgPos.Pos.ECF;

/// <summary>
/// Facturación electrónica de la caja (M09): certificado, emisión y firma del e-CF, estado de las secuencias y regularización de
/// las ventas cobradas en contingencia. Va en su propio ensamblado para poder actualizarla o certificarla sin tocar el resto de la caja.
/// </summary>
public static class InyeccionDependenciasEcf
{
    public static IServiceCollection AgregarEcfPos(this IServiceCollection servicios, IConfiguration configuracion)
    {
        ArgumentNullException.ThrowIfNull(servicios);
        ArgumentNullException.ThrowIfNull(configuracion);

        // El certificado vive en memoria mientras corre el Agente: su PIN se digita al iniciar la jornada.
        servicios.AddSingleton<ICertificadoCaja>(proveedor => new CertificadoCaja(configuracion, proveedor.GetRequiredService<ILogger<CertificadoCaja>>()));
        servicios.AddScoped<IEmisorComprobantes>(proveedor => new EmisionComprobantes(proveedor.GetRequiredService<ContextoDatosPos>(),
            proveedor.GetRequiredService<ICertificadoCaja>(), proveedor.GetRequiredService<IParametros>(), configuracion,
            proveedor.GetRequiredService<TimeProvider>()));
        servicios.AddScoped<IServicioEcf, ServicioEcf>();
        servicios.AddScoped<IRegularizacionContingencia, RegularizacionContingencia>();

        return servicios;
    }
}
