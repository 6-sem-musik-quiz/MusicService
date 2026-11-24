using MusicService.Domain.Dtos;

namespace MusicService.Repository.Interfaces;

public enum RenamePlaylistResult
{
    Renamed,
    NotFound,
    NameTaken
}

public interface IPlaylistRepository
{
    // All playlists with their song count, sorted by name.
    Task<IReadOnlyList<PlaylistDto>> GetAllAsync(CancellationToken ct = default);

    Task<PlaylistDto?> GetAsync(int id, CancellationToken ct = default);

    // Returns the new id, or null if another playlist already has the name (case-insensitive).
    Task<int?> TryAddAsync(string name, CancellationToken ct = default);

    Task<RenamePlaylistResult> TryRenameAsync(int id, string name, CancellationToken ct = default);

    // Deletes the playlist and the songs that are in no other playlist, in one transaction.
    // Returns the BlobKeys of the deleted songs, or null if the playlist does not exist.
    Task<IReadOnlyList<Guid>?> DeleteAsync(int id, CancellationToken ct = default);
}
