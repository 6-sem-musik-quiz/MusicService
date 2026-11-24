using Microsoft.EntityFrameworkCore;
using MusicService.Repository.Data;

namespace MusicService.Repository.Repositories;

internal static class ArtistCleanup
{
    // Deletes those of the given artists that have no songs any more. Only artists that just lost
    // a song are candidates, so an artist the admin created but has not used yet is kept.
    // Call after the song changes are saved, inside the same transaction.
    public static async Task DeleteArtistsWithoutSongsAsync(this AppDbContext db, IReadOnlyCollection<int> candidateIds,
        CancellationToken ct)
    {
        if (candidateIds.Count == 0)
            return;

        var unused = await db.Artists
            .Where(a => candidateIds.Contains(a.Id) && !a.SongArtists.Any())
            .ToListAsync(ct);
        if (unused.Count == 0)
            return;

        db.Artists.RemoveRange(unused);
        await db.SaveChangesAsync(ct);
    }
}
