using MusicService.Domain.Dtos;

namespace MusicService.Service.Interfaces;

public interface IArtistService
{
    Task<IReadOnlyList<ArtistDto>> SearchAsync(string? search, CancellationToken ct = default);
    Task<ArtistDto> GetAsync(int id, CancellationToken ct = default);
    Task<ArtistDto> CreateAsync(ArtistNameRequest request, CancellationToken ct = default);
}
