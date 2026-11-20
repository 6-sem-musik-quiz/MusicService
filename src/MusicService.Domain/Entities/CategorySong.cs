namespace MusicService.Domain.Entities;

// Join table: a song can be in many categories, a category has many songs.
public class CategorySong
{
    public int CategoryId { get; set; }
    public Category Category { get; set; } = null!;

    public int SongId { get; set; }
    public Song Song { get; set; } = null!;
}
