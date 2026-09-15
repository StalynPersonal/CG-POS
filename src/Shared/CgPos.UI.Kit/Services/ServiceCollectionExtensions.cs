using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;

namespace CgPos.UI.Kit.Services;

public static class ServiceCollectionExtensions
{
    /// <summary>Registra MudBlazor y los servicios del kit visual CG-POS.</summary>
    public static IServiceCollection AddCgPosUi(this IServiceCollection services)
    {
        services.AddMudServices(config =>
        {
            config.SnackbarConfiguration.PositionClass = Defaults.Classes.Position.TopCenter;
            config.SnackbarConfiguration.VisibleStateDuration = 4000;
            config.SnackbarConfiguration.ShowCloseIcon = true;
        });

        services.AddScoped<AtajosTecladoService>();
        return services;
    }
}
