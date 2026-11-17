using MusicService.Repository.Data;
using MusicService.Repository.Interfaces;
using MusicService.Repository.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MusicService.Repository.Extensions;

public static class RepositoryLayerExtensions
{
    public static IServiceCollection AddRepositoryLayer(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));

        services.AddScoped<IExampleRepository, ExampleRepository>();

        return services;
    }
}
