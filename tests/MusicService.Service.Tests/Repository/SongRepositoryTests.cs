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
}
