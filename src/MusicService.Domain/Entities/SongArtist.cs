namespace MusicService.Domain.Entities;

// Join table: a song can have many artists, an artist has many songs.
public class SongArtist
{
    public int SongId { get; set; }
    public Song Song { get; set; } = null!;

    public int ArtistId { get; set; }
    public Artist Artist { get; set; } = null!;
}
