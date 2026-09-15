using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;

namespace CgPos.Interfaz.Servicios;

public static class ExtensionesColeccionServicios
{
    /// <summary>Registra MudBlazor y los servicios del kit visual CG-POS.</summary>
    public static IServiceCollection AgregarInterfazCgPos(this IServiceCollection servicios)
    {
        servicios.AddMudServices(configuracion =>
        {
            configuracion.SnackbarConfiguration.PositionClass = Defaults.Classes.Position.TopCenter;
            configuracion.SnackbarConfiguration.VisibleStateDuration = 4000;
            configuracion.SnackbarConfiguration.ShowCloseIcon = true;
        });

        servicios.AddScoped<ServicioAtajosTeclado>();
        return servicios;
    }
}
