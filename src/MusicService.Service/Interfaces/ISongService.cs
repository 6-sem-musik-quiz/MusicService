using MusicService.Domain.Dtos;

namespace MusicService.Service.Interfaces;

public interface ISongService
{
    // Stores the mp3 in blob storage and the song in the playlist. `file` must be seekable.
    Task<SongDto> UploadAsync(int playlistId, UploadSongRequest request, Stream file, string? contentType,
        CancellationToken ct = default);
}
