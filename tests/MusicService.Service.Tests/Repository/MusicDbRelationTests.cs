using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MusicService.Domain.Entities;
using MusicService.Repository.Data;

namespace MusicService.Service.Tests.Repository;

// Checks the many-to-many mappings in AppDbContext against a real relational database
// (SQLite in memory), so keys, foreign keys and cascades are enforced like in Postgres.
public sealed class MusicDbRelationTests : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly DbContextOptions<AppDbContext> _options;

    public MusicDbRelationTests()
    {
        _connection.Open();
        _options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
        using var db = new AppDbContext(_options);
        db.Database.EnsureCreated();
    }

    public void Dispose() => _connection.Dispose();

    [Fact]
    public async Task Song_CanBeInTwoPlaylists()
    {
        await using (var db = new AppDbContext(_options))
        {
            var song = NewSong("Take On Me");
            db.Categories.AddRange(
                new Category { Name = "80s", CategorySongs = [new CategorySong { Song = song }] },
                new Category { Name = "Synthpop", CategorySongs = [new CategorySong { Song = song }] });
            await db.SaveChangesAsync();
        }

        await using (var db = new AppDbContext(_options))
        {
            var song = await db.Songs
                .Include(s => s.CategorySongs).ThenInclude(cs => cs.Category)
                .SingleAsync();

            Assert.Equal(["80s", "Synthpop"], song.CategorySongs.Select(cs => cs.Category.Name).Order());
        }
    }

    [Fact]
    public async Task Song_CanHaveTwoArtists()
    {
        await using (var db = new AppDbContext(_options))
        {
            var song = NewSong("Under Pressure");
            song.SongArtists =
            [
                new SongArtist { Artist = new Artist { Name = "Queen" } },
                new SongArtist { Artist = new Artist { Name = "David Bowie" } }
            ];
            db.Songs.Add(song);
            await db.SaveChangesAsync();
        }

        await using (var db = new AppDbContext(_options))
        {
            var song = await db.Songs
                .Include(s => s.SongArtists).ThenInclude(sa => sa.Artist)
                .SingleAsync();

            Assert.Equal(["David Bowie", "Queen"], song.SongArtists.Select(sa => sa.Artist.Name).Order());
        }
    }

    [Fact]
    public async Task DeletingPlaylist_KeepsSongThatIsInAnotherPlaylist()
    {
        await using (var db = new AppDbContext(_options))
        {
            var song = NewSong("Take On Me");
            db.Categories.AddRange(
                new Category { Name = "80s", CategorySongs = [new CategorySong { Song = song }] },
                new Category { Name = "Synthpop", CategorySongs = [new CategorySong { Song = song }] });
            await db.SaveChangesAsync();
        }

        await using (var db = new AppDbContext(_options))
        {
            db.Categories.Remove(await db.Categories.SingleAsync(c => c.Name == "80s"));
            await db.SaveChangesAsync();
        }

        await using (var db = new AppDbContext(_options))
        {
            var link = await db.CategorySongs.Include(cs => cs.Category).SingleAsync();
            Assert.Equal("Synthpop", link.Category.Name);
            Assert.Equal(1, await db.Songs.CountAsync());
        }
    }

    [Fact]
    public async Task SameSongTwiceInOnePlaylist_IsRejected()
    {
        await using var db = new AppDbContext(_options);
        var song = NewSong("Take On Me");
        db.Categories.Add(new Category { Name = "80s", CategorySongs = [new CategorySong { Song = song }] });
        await db.SaveChangesAsync();

        db.ChangeTracker.Clear();
        db.CategorySongs.Add(new CategorySong { CategoryId = song.CategorySongs[0].CategoryId, SongId = song.Id });

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task PlaylistName_IsUnique()
    {
        await using var db = new AppDbContext(_options);
        db.Categories.AddRange(new Category { Name = "80s" }, new Category { Name = "80s" });

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    private static Song NewSong(string title) => new()
    {
        Title = title,
        Year = 1985,
        DurationSeconds = 225,
        BlobKey = Guid.NewGuid()
    };
}
