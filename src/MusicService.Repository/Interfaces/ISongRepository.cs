using MusicService.Domain.Entities;

namespace MusicService.Repository.Interfaces;

// Found: the song was in the playlist. DeletedBlobKey: set when the song was in no other playlist
// and was deleted, so its file must be deleted too.
public record RemoveSongResult(bool Found, Guid? DeletedBlobKey);

public interface ISongRepository
{
    // Saves the song, puts it in the playlist and links it to the artists, in one transaction.
    // Returns the song with its artists loaded.
    Task<Song> AddAsync(Song song, int playlistId, IReadOnlyCollection<int> artistIds, CancellationToken ct = default);

    // Ids of the songs in the playlist (unordered).
    Task<IReadOnlyList<int>> GetIdsInPlaylistAsync(int playlistId, CancellationToken ct = default);

    // The songs with these ids, with their artists loaded, in no particular order.
    Task<IReadOnlyList<Song>> GetWithArtistsAsync(IReadOnlyCollection<int> ids, CancellationToken ct = default);

    // The songs in the playlist with their artists, sorted by title.
    Task<IReadOnlyList<Song>> GetInPlaylistAsync(int playlistId, CancellationToken ct = default);

    // Sets title, year and artists. Artists that lose their last song are deleted.
    // Returns the song with its artists, or null if it does not exist.
    Task<Song?> UpdateAsync(int songId, string title, int year, IReadOnlyCollection<int> artistIds,
        CancellationToken ct = default);

    // Removes the song from the playlist; deletes it if it is in no other playlist,
    // and then deletes artists that have no songs left.
    Task<RemoveSongResult> RemoveFromPlaylistAsync(int playlistId, int songId, CancellationToken ct = default);
}
