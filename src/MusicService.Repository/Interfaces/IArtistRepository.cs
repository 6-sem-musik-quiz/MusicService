using MusicService.Domain.Dtos;

namespace MusicService.Repository.Interfaces;

public interface IArtistRepository
{
    // Artists whose name contains the search text (ignoring case), sorted by name, at most `limit`.
    // Without search text, the first `limit` artists by name.
    Task<IReadOnlyList<ArtistDto>> SearchAsync(string? search, int limit, CancellationToken ct = default);

    Task<ArtistDto?> GetAsync(int id, CancellationToken ct = default);

    // The ids from the list that exist.
    Task<IReadOnlySet<int>> GetExistingIdsAsync(IReadOnlyCollection<int> ids, CancellationToken ct = default);

    // Returns the new id, or null if another artist already has the name (case-insensitive).
    Task<int?> TryAddAsync(string name, CancellationToken ct = default);
}
