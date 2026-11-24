using Microsoft.EntityFrameworkCore;
using MusicService.Domain.Dtos;
using MusicService.Domain.Entities;
using MusicService.Repository.Data;
using MusicService.Repository.Interfaces;

namespace MusicService.Repository.Repositories;

public class ArtistRepository(AppDbContext db) : IArtistRepository
{
    public async Task<IReadOnlyList<ArtistDto>> SearchAsync(string? search, int limit, CancellationToken ct = default)
    {
        var query = db.Artists.AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var lower = search.Trim().ToLower();
            query = query.Where(a => a.Name.ToLower().Contains(lower));
        }

        return await query
            .OrderBy(a => a.Name)
            .Take(limit)
            .Select(a => new ArtistDto(a.Id, a.Name))
            .ToListAsync(ct);
    }

    public Task<ArtistDto?> GetAsync(int id, CancellationToken ct = default) =>
        db.Artists.Where(a => a.Id == id).Select(a => new ArtistDto(a.Id, a.Name)).SingleOrDefaultAsync(ct);

    public async Task<IReadOnlySet<int>> GetExistingIdsAsync(IReadOnlyCollection<int> ids, CancellationToken ct = default) =>
        (await db.Artists.Where(a => ids.Contains(a.Id)).Select(a => a.Id).ToListAsync(ct)).ToHashSet();

    public async Task<int?> TryAddAsync(string name, CancellationToken ct = default)
    {
        if (await NameTakenAsync(name, ct))
            return null;

        var artist = new Artist { Name = name };
        db.Artists.Add(artist);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Another request may have created the same name between the check and the insert.
            db.Entry(artist).State = EntityState.Detached;
            if (await NameTakenAsync(name, ct))
                return null;
            throw;
        }

        return artist.Id;
    }

    private Task<bool> NameTakenAsync(string name, CancellationToken ct)
    {
        var lower = name.ToLower();
        return db.Artists.AnyAsync(a => a.Name.ToLower() == lower, ct);
    }
}
