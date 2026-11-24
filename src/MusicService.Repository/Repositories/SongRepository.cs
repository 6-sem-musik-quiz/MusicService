using Microsoft.EntityFrameworkCore;
using MusicService.Domain.Entities;
using MusicService.Repository.Data;
using MusicService.Repository.Interfaces;

namespace MusicService.Repository.Repositories;

public class SongRepository(AppDbContext db) : ISongRepository
{
    public async Task<Song> AddAsync(Song song, int playlistId, IReadOnlyCollection<int> artistIds,
        CancellationToken ct = default)
    {
        song.CategorySongs = [new CategorySong { CategoryId = playlistId }];
        song.SongArtists = artistIds.Distinct().Select(id => new SongArtist { ArtistId = id }).ToList();
        db.Songs.Add(song);
        await db.SaveChangesAsync(ct); // one SaveChanges = one transaction

        return await WithArtists().SingleAsync(s => s.Id == song.Id, ct);
    }

    public async Task<IReadOnlyList<int>> GetIdsInPlaylistAsync(int playlistId, CancellationToken ct = default) =>
        await db.CategorySongs.Where(cs => cs.CategoryId == playlistId).Select(cs => cs.SongId).ToListAsync(ct);

    public async Task<IReadOnlyList<Song>> GetWithArtistsAsync(IReadOnlyCollection<int> ids, CancellationToken ct = default) =>
        await WithArtists().Where(s => ids.Contains(s.Id)).ToListAsync(ct);

    public async Task<IReadOnlyList<Song>> GetInPlaylistAsync(int playlistId, CancellationToken ct = default) =>
        await WithArtists()
            .Where(s => s.CategorySongs.Any(cs => cs.CategoryId == playlistId))
            .OrderBy(s => s.Title).ThenBy(s => s.Id)
            .ToListAsync(ct);

    public async Task<Song?> UpdateAsync(int songId, string title, int year, IReadOnlyCollection<int> artistIds,
        CancellationToken ct = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var song = await db.Songs.Include(s => s.SongArtists).SingleOrDefaultAsync(s => s.Id == songId, ct);
        if (song is null)
            return null;

        var wanted = artistIds.ToHashSet();
        var removed = song.SongArtists.Where(sa => !wanted.Contains(sa.ArtistId)).ToList();
        var added = wanted.Except(song.SongArtists.Select(sa => sa.ArtistId)).ToList();

        song.Title = title;
        song.Year = year;
        foreach (var link in removed)
            song.SongArtists.Remove(link);
        foreach (var artistId in added)
            song.SongArtists.Add(new SongArtist { SongId = song.Id, ArtistId = artistId });
        await db.SaveChangesAsync(ct);

        await db.DeleteArtistsWithoutSongsAsync(removed.Select(sa => sa.ArtistId).ToList(), ct);
        await transaction.CommitAsync(ct);

        return await WithArtists().SingleAsync(s => s.Id == songId, ct);
    }

    public async Task<RemoveSongResult> RemoveFromPlaylistAsync(int playlistId, int songId, CancellationToken ct = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var link = await db.CategorySongs.SingleOrDefaultAsync(cs => cs.CategoryId == playlistId && cs.SongId == songId, ct);
        if (link is null)
            return new RemoveSongResult(Found: false, DeletedBlobKey: null);

        var inOtherPlaylists = await db.CategorySongs.AnyAsync(cs => cs.SongId == songId && cs.CategoryId != playlistId, ct);
        Guid? deletedBlobKey = null;
        var artistIds = new List<int>();
        if (inOtherPlaylists)
        {
            db.CategorySongs.Remove(link);
        }
        else
        {
            var song = await db.Songs.Include(s => s.SongArtists).SingleAsync(s => s.Id == songId, ct);
            artistIds = song.SongArtists.Select(sa => sa.ArtistId).ToList();
            deletedBlobKey = song.BlobKey;
            db.Songs.Remove(song); // its playlist and artist links go with it (cascade)
        }
        await db.SaveChangesAsync(ct);

        await db.DeleteArtistsWithoutSongsAsync(artistIds, ct);
        await transaction.CommitAsync(ct);

        return new RemoveSongResult(Found: true, deletedBlobKey);
    }

    private IQueryable<Song> WithArtists() =>
        db.Songs.AsNoTracking().Include(s => s.SongArtists).ThenInclude(sa => sa.Artist);
}
