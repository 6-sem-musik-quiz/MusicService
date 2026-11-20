using Microsoft.Extensions.DependencyInjection;

namespace MusicService.Service.Extensions;

public static class ServiceLayerExtensions
{
    public static IServiceCollection AddServiceLayer(this IServiceCollection services)
    {
        // Register services here, e.g. services.AddScoped<ICategoryService, CategoryService>();

        return services;
    }
}
