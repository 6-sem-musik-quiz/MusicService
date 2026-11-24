using Microsoft.EntityFrameworkCore;
using MusicService.Domain.Entities;
using MusicService.Repository.Repositories;

namespace MusicService.Service.Tests.Repository;

public sealed class SongRepositoryTests : SqliteTestDb
{
    private async Task<(int PlaylistId, int QueenId, int BowieId)> Seed()
    {
        await using var db = NewDb();
        var playlist = new Category { Name = "80s" };
        var queen = new Artist { Name = "Queen" };
        var bowie = new Artist { Name = "David Bowie" };
        db.AddRange(playlist, queen, bowie);
        await db.SaveChangesAsync();
        return (playlist.Id, queen.Id, bowie.Id);
    }

    [Fact]
    public async Task Add_LinksSongToPlaylistAndArtists()
    {
        var (playlistId, queenId, bowieId) = await Seed();
        var blobKey = Guid.NewGuid();

        Song saved;
        await using (var db = NewDb())
        {
            saved = await new SongRepository(db).AddAsync(
                new Song { Title = "Under Pressure", Year = 1981, DurationSeconds = 248, BlobKey = blobKey },
                playlistId, [queenId, bowieId, queenId]);
        }

        Assert.True(saved.Id > 0);
        Assert.Equal(["David Bowie", "Queen"], saved.SongArtists.Select(sa => sa.Artist.Name).Order());

        await using var check = NewDb();
        var song = await check.Songs.Include(s => s.CategorySongs).Include(s => s.SongArtists).SingleAsync();
        Assert.Equal(blobKey, song.BlobKey);
        Assert.Equal([playlistId], song.CategorySongs.Select(cs => cs.CategoryId));
        Assert.Equal(2, song.SongArtists.Count);
    }

    [Fact]
    public async Task GetIdsInPlaylist_AndGetWithArtists()
    {
        var (playlistId, queenId, bowieId) = await Seed();
        int inPlaylist, alsoInPlaylist;
        await using (var db = NewDb())
        {
            var repository = new SongRepository(db);
            inPlaylist = (await repository.AddAsync(NewSong(), playlistId, [queenId])).Id;
            alsoInPlaylist = (await repository.AddAsync(NewSong(), playlistId, [queenId, bowieId])).Id;
            var other = new Category { Name = "Other" };
            db.Categories.Add(other);
            await db.SaveChangesAsync();
            await repository.AddAsync(NewSong(), other.Id, [bowieId]);
        }

        await using var read = NewDb();
        var reader = new SongRepository(read);
        var ids = await reader.GetIdsInPlaylistAsync(playlistId);
        var songs = await reader.GetWithArtistsAsync([alsoInPlaylist]);

        Assert.Equal([inPlaylist, alsoInPlaylist], ids.Order());
        Assert.Equal(["David Bowie", "Queen"], songs.Single().SongArtists.Select(sa => sa.Artist.Name).Order());
    }

    private static Song NewSong() => new() { Title = "Song", Year = 1985, DurationSeconds = 200, BlobKey = Guid.NewGuid() };
}
