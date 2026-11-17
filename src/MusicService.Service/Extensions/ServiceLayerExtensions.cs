using MusicService.Service.Interfaces;
using MusicService.Service.Services;
using Microsoft.Extensions.DependencyInjection;

namespace MusicService.Service.Extensions;

public static class ServiceLayerExtensions
{
    public static IServiceCollection AddServiceLayer(this IServiceCollection services)
    {
        services.AddScoped<IExampleService, ExampleService>();

        return services;
    }
}
