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

    private IQueryable<Song> WithArtists() =>
        db.Songs.AsNoTracking().Include(s => s.SongArtists).ThenInclude(sa => sa.Artist);
}
