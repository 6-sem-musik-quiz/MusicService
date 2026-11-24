using Microsoft.EntityFrameworkCore;
using MusicService.Domain.Dtos;
using MusicService.Domain.Entities;
using MusicService.Repository.Data;
using MusicService.Repository.Interfaces;

namespace MusicService.Repository.Repositories;

public class PlaylistRepository(AppDbContext db) : IPlaylistRepository
{
    public async Task<IReadOnlyList<PlaylistDto>> GetAllAsync(CancellationToken ct = default) =>
        await db.Categories
            .OrderBy(c => c.Name)
            .Select(c => new PlaylistDto(c.Id, c.Name, c.CategorySongs.Count))
            .ToListAsync(ct);

    public Task<PlaylistDto?> GetAsync(int id, CancellationToken ct = default) =>
        db.Categories
            .Where(c => c.Id == id)
            .Select(c => new PlaylistDto(c.Id, c.Name, c.CategorySongs.Count))
            .SingleOrDefaultAsync(ct);

    public async Task<int?> TryAddAsync(string name, CancellationToken ct = default)
    {
        if (await NameTakenAsync(name, exceptId: null, ct))
            return null;

        var category = new Category { Name = name };
        db.Categories.Add(category);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Another request may have created the same name between the check and the insert.
            db.Entry(category).State = EntityState.Detached;
            if (await NameTakenAsync(name, exceptId: null, ct))
                return null;
            throw;
        }

        return category.Id;
    }

    public async Task<RenamePlaylistResult> TryRenameAsync(int id, string name, CancellationToken ct = default)
    {
        var category = await db.Categories.SingleOrDefaultAsync(c => c.Id == id, ct);
        if (category is null)
            return RenamePlaylistResult.NotFound;
        if (await NameTakenAsync(name, exceptId: id, ct))
            return RenamePlaylistResult.NameTaken;

        category.Name = name;
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Another request may have taken the name between the check and the update.
            db.Entry(category).State = EntityState.Detached;
            if (await NameTakenAsync(name, exceptId: id, ct))
                return RenamePlaylistResult.NameTaken;
            throw;
        }

        return RenamePlaylistResult.Renamed;
    }

    public async Task<IReadOnlyList<Guid>?> DeleteAsync(int id, CancellationToken ct = default)
    {
        var category = await db.Categories.SingleOrDefaultAsync(c => c.Id == id, ct);
        if (category is null)
            return null;

        // Songs in this playlist and in no other. Shared songs only lose their link (cascade).
        var orphans = await db.Songs
            .Where(s => s.CategorySongs.Any(cs => cs.CategoryId == id)
                        && s.CategorySongs.All(cs => cs.CategoryId == id))
            .ToListAsync(ct);

        db.Songs.RemoveRange(orphans);
        db.Categories.Remove(category);
        await db.SaveChangesAsync(ct); // one SaveChanges = one transaction

        return orphans.Select(s => s.BlobKey).ToList();
    }

    private Task<bool> NameTakenAsync(string name, int? exceptId, CancellationToken ct)
    {
        var lower = name.ToLower();
        return db.Categories.AnyAsync(c => c.Name.ToLower() == lower && c.Id != exceptId, ct);
    }
}
