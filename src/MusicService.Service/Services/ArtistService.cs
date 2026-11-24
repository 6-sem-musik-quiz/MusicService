using MusicService.Domain.Dtos;
using MusicService.Domain.Exceptions;
using MusicService.Repository.Interfaces;
using MusicService.Service.Interfaces;

namespace MusicService.Service.Services;

// Artists are picked by id when a song is uploaded. Names are 1-200 characters and unique
// (case-insensitive), so the admin finds the existing artist instead of making a duplicate.
public class ArtistService(IArtistRepository repository) : IArtistService
{
    public const int MaxNameLength = 200;
    public const int MaxSearchResults = 50;

    public Task<IReadOnlyList<ArtistDto>> SearchAsync(string? search, CancellationToken ct = default) =>
        repository.SearchAsync(search, MaxSearchResults, ct);

    public async Task<ArtistDto> GetAsync(int id, CancellationToken ct = default) =>
        await repository.GetAsync(id, ct) ?? throw new ArtistNotFoundException(id);

    public async Task<ArtistDto> CreateAsync(ArtistNameRequest request, CancellationToken ct = default)
    {
        var name = request.Name?.Trim() ?? string.Empty;
        if (name.Length is 0 or > MaxNameLength)
            throw new MusicValidationException($"name must be between 1 and {MaxNameLength} characters.");

        var id = await repository.TryAddAsync(name, ct)
                 ?? throw new MusicConflictException($"An artist named \"{name}\" already exists.");

        return new ArtistDto(id, name);
    }
}
