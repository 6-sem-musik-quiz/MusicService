namespace MusicService.Domain.Dtos;

// A song as the API returns it. AudioUrl is the public CDN address of the whole mp3.
public record SongDto(
    int Id,
    string Title,
    IReadOnlyList<string> Artists,
    int Year,
    int DurationSeconds,
    string AudioUrl);

// The text fields of POST /api/playlists/{id}/songs (the file comes separately).
public record UploadSongRequest(string Title, IReadOnlyList<int> ArtistIds, int Year);
