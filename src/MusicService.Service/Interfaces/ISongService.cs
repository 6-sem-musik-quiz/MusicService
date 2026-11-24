using MusicService.Domain.Dtos;

namespace MusicService.Service.Interfaces;

public interface ISongService
{
    // Stores the mp3 in blob storage and the song in the playlist. `file` must be seekable.
    Task<SongDto> UploadAsync(int playlistId, UploadSongRequest request, Stream file, string? contentType,
        CancellationToken ct = default);

    // `count` different songs from the playlist in random order (for GameService).
    Task<IReadOnlyList<SongDto>> GetRandomAsync(int playlistId, int count, CancellationToken ct = default);

    // All songs in the playlist, sorted by title.
    Task<IReadOnlyList<SongDto>> GetInPlaylistAsync(int playlistId, CancellationToken ct = default);

    // Changes title, artists and year. The song is the same in every playlist it is in.
    Task<SongDto> UpdateAsync(int songId, UpdateSongRequest request, CancellationToken ct = default);

    // Takes the song out of the playlist. If it is in no other playlist it is deleted, with its file.
    Task RemoveFromPlaylistAsync(int playlistId, int songId, CancellationToken ct = default);
}
