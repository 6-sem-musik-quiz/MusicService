namespace MusicService.Domain.Entities;

// A playlist the quiz can be played on.
public class Category
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    public List<CategorySong> CategorySongs { get; set; } = [];
}
