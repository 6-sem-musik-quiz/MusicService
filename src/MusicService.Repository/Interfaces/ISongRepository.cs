using MusicService.Domain.Entities;

namespace MusicService.Repository.Interfaces;

public interface ISongRepository
{
    // Saves the song, puts it in the playlist and links it to the artists, in one transaction.
    // Returns the song with its artists loaded.
    Task<Song> AddAsync(Song song, int playlistId, IReadOnlyCollection<int> artistIds, CancellationToken ct = default);
}
