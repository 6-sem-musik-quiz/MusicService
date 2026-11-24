using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MusicService.Domain.Entities;
using MusicService.Repository.Data;
using MusicService.Repository.Interfaces;
using MusicService.Repository.Repositories;

namespace MusicService.Service.Tests.Repository;

// PlaylistRepository against SQLite in memory, so unique indexes and cascades are enforced.
public sealed class PlaylistRepositoryTests : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly DbContextOptions<AppDbContext> _options;

    public PlaylistRepositoryTests()
    {
        _connection.Open();
        _options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
        using var db = NewDb();
        db.Database.EnsureCreated();
    }

    public void Dispose() => _connection.Dispose();

    private AppDbContext NewDb() => new(_options);

    [Fact]
    public async Task GetAll_IsSortedByNameWithSongCount()
    {
        await using (var db = NewDb())
        {
            db.Categories.AddRange(
                new Category { Name = "Rock", CategorySongs = [new CategorySong { Song = NewSong() }, new CategorySong { Song = NewSong() }] },
                new Category { Name = "80s" });
            await db.SaveChangesAsync();
        }

        await using var read = NewDb();
        var playlists = await new PlaylistRepository(read).GetAllAsync();

        Assert.Equal(["80s", "Rock"], playlists.Select(p => p.Name));
        Assert.Equal([0, 2], playlists.Select(p => p.SongCount));
    }

    [Fact]
    public async Task Get_ReturnsNullForUnknownId()
    {
        await using var db = NewDb();

        Assert.Null(await new PlaylistRepository(db).GetAsync(42));
    }

    [Fact]
    public async Task TryAdd_ReturnsNewId()
    {
        await using var db = NewDb();
        var repository = new PlaylistRepository(db);

        var id = await repository.TryAddAsync("80s");

        Assert.NotNull(id);
        Assert.Equal("80s", (await repository.GetAsync(id.Value))?.Name);
    }

    [Fact]
    public async Task TryAdd_ReturnsNullWhenNameIsTakenIgnoringCase()
    {
        await using var db = NewDb();
        var repository = new PlaylistRepository(db);
        await repository.TryAddAsync("Rock");

        Assert.Null(await repository.TryAddAsync("ROCK"));
        Assert.Single(await repository.GetAllAsync());
    }

    [Fact]
    public async Task TryRename_ResultsForNotFoundTakenAndRenamed()
    {
        await using var db = NewDb();
        var repository = new PlaylistRepository(db);
        var rock = (await repository.TryAddAsync("Rock"))!.Value;
        await repository.TryAddAsync("Pop");

        Assert.Equal(RenamePlaylistResult.NotFound, await repository.TryRenameAsync(999, "Jazz"));
        Assert.Equal(RenamePlaylistResult.NameTaken, await repository.TryRenameAsync(rock, "pop"));
        // Changing only the case of its own name is allowed.
        Assert.Equal(RenamePlaylistResult.Renamed, await repository.TryRenameAsync(rock, "ROCK"));
        Assert.Equal(RenamePlaylistResult.Renamed, await repository.TryRenameAsync(rock, "Classic Rock"));
        Assert.Equal("Classic Rock", (await repository.GetAsync(rock))?.Name);
    }

    [Fact]
    public async Task Delete_RemovesOnlySongsThatAreInNoOtherPlaylist()
    {
        var shared = NewSong();
        var onlyHere = NewSong();
        onlyHere.SongArtists = [new SongArtist { Artist = new Artist { Name = "a-ha" } }];
        int eightiesId;
        await using (var db = NewDb())
        {
            var eighties = new Category
            {
                Name = "80s",
                CategorySongs = [new CategorySong { Song = shared }, new CategorySong { Song = onlyHere }]
            };
            db.Categories.AddRange(eighties, new Category { Name = "Synthpop", CategorySongs = [new CategorySong { Song = shared }] });
            await db.SaveChangesAsync();
            eightiesId = eighties.Id;
        }

        IReadOnlyList<Guid>? deleted;
        await using (var db = NewDb())
            deleted = await new PlaylistRepository(db).DeleteAsync(eightiesId);

        Assert.Equal([onlyHere.BlobKey], deleted);
        await using var check = NewDb();
        Assert.Equal(["Synthpop"], await check.Categories.Select(c => c.Name).ToListAsync());
        Assert.Equal([shared.BlobKey], await check.Songs.Select(s => s.BlobKey).ToListAsync());
        Assert.Equal(1, await check.CategorySongs.CountAsync());
        Assert.Equal(0, await check.SongArtists.CountAsync());
    }

    [Fact]
    public async Task Delete_ReturnsNullForUnknownId()
    {
        await using var db = NewDb();

        Assert.Null(await new PlaylistRepository(db).DeleteAsync(42));
    }

    private static Song NewSong() => new()
    {
        Title = "Song",
        Year = 1985,
        DurationSeconds = 200,
        BlobKey = Guid.NewGuid()
    };
}
