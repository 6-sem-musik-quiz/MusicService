namespace MusicService.Domain.Entities;

public class Song
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public int Year { get; set; }
    public int DurationSeconds { get; set; }

    // File name in blob storage. A Guid, so the URL does not give away the answer.
    public Guid BlobKey { get; set; }

    public List<CategorySong> CategorySongs { get; set; } = [];
    public List<SongArtist> SongArtists { get; set; } = [];
}
