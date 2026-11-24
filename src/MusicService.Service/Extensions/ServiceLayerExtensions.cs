using Microsoft.Extensions.DependencyInjection;
using MusicService.Service.Interfaces;
using MusicService.Service.Services;

namespace MusicService.Service.Extensions;

public static class ServiceLayerExtensions
{
    public static IServiceCollection AddServiceLayer(this IServiceCollection services)
    {
        services.AddScoped<IPlaylistService, PlaylistService>();
        services.AddScoped<IArtistService, ArtistService>();
        services.AddScoped<ISongService, SongService>();
        services.AddSingleton<IAudioInspector, TagLibAudioInspector>();
        services.AddSingleton(TimeProvider.System);

        return services;
    }
}
