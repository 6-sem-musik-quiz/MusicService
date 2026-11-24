using Microsoft.EntityFrameworkCore;
using MusicService.Domain.Entities;
using MusicService.Repository.Repositories;

namespace MusicService.Service.Tests.Repository;

// SCRUM-94 on SQLite: list, edit and remove songs, and deleting artists that lose their last song.
public sealed class SongManagementRepositoryTests : SqliteTestDb
{
    private int _eighties, _synthpop, _queen, _bowie, _aha, _unused;

    private async Task Seed()
    {
        await using var db = NewDb();
        var eighties = new Category { Name = "80s" };
        var synthpop = new Category { Name = "Synthpop" };
        var queen = new Artist { Name = "Queen" };
        var bowie = new Artist { Name = "David Bowie" };
        var aha = new Artist { Name = "a-ha" };
        var unused = new Artist { Name = "Created but not used yet" };
        db.AddRange(eighties, synthpop, queen, bowie, aha, unused);
        await db.SaveChangesAsync();
        (_eighties, _synthpop, _queen, _bowie, _aha, _unused) = (eighties.Id, synthpop.Id, queen.Id, bowie.Id, aha.Id, unused.Id);
    }

    private async Task<Song> AddSong(string title, int[] playlists, int[] artists)
    {
        await using var db = NewDb();
        var song = await new SongRepository(db).AddAsync(
            new Song { Title = title, Year = 1985, DurationSeconds = 200, BlobKey = Guid.NewGuid() }, playlists[0], artists);
        foreach (var other in playlists.Skip(1))
            db.CategorySongs.Add(new CategorySong { CategoryId = other, SongId = song.Id });
        await db.SaveChangesAsync();
        return song;
    }

    private async Task<List<string>> ArtistNames()
    {
        await using var db = NewDb();
        // Sorted here, not in SQL: SQLite sorts case-sensitively, Postgres by collation.
        var names = await db.Artists.Select(a => a.Name).ToListAsync();
        return names.Order(StringComparer.OrdinalIgnoreCase).ToList();
    }

    [Fact]
    public async Task GetInPlaylist_IsSortedByTitleWithArtists()
    {
        await Seed();
        await AddSong("Under Pressure", [_eighties], [_queen, _bowie]);
        await AddSong("Take On Me", [_eighties, _synthpop], [_aha]);
        await AddSong("Not in 80s", [_synthpop], [_aha]);

        await using var db = NewDb();
        var songs = await new SongRepository(db).GetInPlaylistAsync(_eighties);

        Assert.Equal(["Take On Me", "Under Pressure"], songs.Select(s => s.Title));
        Assert.Equal(["David Bowie", "Queen"], songs[1].SongArtists.Select(sa => sa.Artist.Name).Order());
    }

    [Fact]
    public async Task Update_ChangesFieldsAndArtists_AndDeletesArtistThatLostItsLastSong()
    {
        await Seed();
        var pressure = await AddSong("Under Presure", [_eighties], [_queen, _aha]);
        await AddSong("Take On Me", [_eighties], [_aha]);
        await AddSong("Bohemian Rhapsody", [_eighties], [_queen]);
        await AddSong("Heroes", [_eighties], [_bowie]);

        // Fix the title and year, swap a-ha for Bowie. Queen keeps a song, so nobody is orphaned yet.
        await using (var db = NewDb())
        {
            var updated = await new SongRepository(db).UpdateAsync(pressure.Id, "Under Pressure", 1981, [_queen, _bowie]);
            Assert.Equal("Under Pressure", updated?.Title);
            Assert.Equal(1981, updated?.Year);
            Assert.Equal(["David Bowie", "Queen"], updated!.SongArtists.Select(sa => sa.Artist.Name).Order());
        }
        Assert.Contains("a-ha", await ArtistNames()); // still has Take On Me

        // Give Take On Me to Queen: a-ha loses its last song and is deleted; the unused artist is kept.
        await using (var db = NewDb())
        {
            var takeOnMe = await db.Songs.SingleAsync(s => s.Title == "Take On Me");
            await new SongRepository(db).UpdateAsync(takeOnMe.Id, "Take On Me", 1985, [_queen]);
        }
        Assert.DoesNotContain("a-ha", await ArtistNames());
        Assert.Contains("Created but not used yet", await ArtistNames());
    }

    [Fact]
    public async Task Update_UnknownSong_ReturnsNull()
    {
        await Seed();
        await using var db = NewDb();

        Assert.Null(await new SongRepository(db).UpdateAsync(999, "x", 2000, [_queen]));
    }

    [Fact]
    public async Task Remove_SharedSong_OnlyUnlinksIt()
    {
        await Seed();
        var song = await AddSong("Take On Me", [_eighties, _synthpop], [_aha]);

        await using (var db = NewDb())
        {
            var result = await new SongRepository(db).RemoveFromPlaylistAsync(_eighties, song.Id);
            Assert.True(result.Found);
            Assert.Null(result.DeletedBlobKey);
        }

        await using var check = NewDb();
        Assert.Equal([_synthpop], await check.CategorySongs.Select(cs => cs.CategoryId).ToListAsync());
        Assert.Equal(1, await check.Songs.CountAsync());
        Assert.Contains("a-ha", await ArtistNames());
    }

    [Fact]
    public async Task Remove_SongOnlyHere_DeletesSongAndArtistsWithoutSongs()
    {
        await Seed();
        var pressure = await AddSong("Under Pressure", [_eighties], [_queen, _bowie]);
        await AddSong("Bohemian Rhapsody", [_synthpop], [_queen]);

        await using (var db = NewDb())
        {
            var result = await new SongRepository(db).RemoveFromPlaylistAsync(_eighties, pressure.Id);
            Assert.True(result.Found);
            Assert.Equal(pressure.BlobKey, result.DeletedBlobKey);
        }

        await using var check = NewDb();
        Assert.Equal(["Bohemian Rhapsody"], await check.Songs.Select(s => s.Title).ToListAsync());
        Assert.Equal(["a-ha", "Created but not used yet", "Queen"], await ArtistNames()); // Bowie lost his only song
    }

    [Fact]
    public async Task Remove_SongNotInThatPlaylist_IsNotFound()
    {
        await Seed();
        var song = await AddSong("Take On Me", [_synthpop], [_aha]);

        await using var db = NewDb();
        var repository = new SongRepository(db);

        Assert.False((await repository.RemoveFromPlaylistAsync(_eighties, song.Id)).Found);
        Assert.False((await repository.RemoveFromPlaylistAsync(_synthpop, 999)).Found);
        Assert.Equal(1, await db.Songs.CountAsync());
    }

    [Fact]
    public async Task DeletePlaylist_AlsoDeletesArtistsWithoutSongs()
    {
        await Seed();
        await AddSong("Under Pressure", [_eighties], [_queen, _bowie]);
        await AddSong("Bohemian Rhapsody", [_synthpop], [_queen]);

        await using (var db = NewDb())
            await new PlaylistRepository(db).DeleteAsync(_eighties);

        Assert.Equal(["a-ha", "Created but not used yet", "Queen"], await ArtistNames());
    }
}
