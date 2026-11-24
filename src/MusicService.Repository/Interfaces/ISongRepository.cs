using MusicService.Domain.Entities;

namespace MusicService.Repository.Interfaces;

public interface ISongRepository
{
    // Saves the song, puts it in the playlist and links it to the artists, in one transaction.
    // Returns the song with its artists loaded.
    Task<Song> AddAsync(Song song, int playlistId, IReadOnlyCollection<int> artistIds, CancellationToken ct = default);

    // Ids of the songs in the playlist (unordered).
    Task<IReadOnlyList<int>> GetIdsInPlaylistAsync(int playlistId, CancellationToken ct = default);

    // The songs with these ids, with their artists loaded, in no particular order.
    Task<IReadOnlyList<Song>> GetWithArtistsAsync(IReadOnlyCollection<int> ids, CancellationToken ct = default);
}
