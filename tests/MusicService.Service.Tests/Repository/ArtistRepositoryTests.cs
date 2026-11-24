using MusicService.Domain.Entities;
using MusicService.Repository.Repositories;

namespace MusicService.Service.Tests.Repository;

public sealed class ArtistRepositoryTests : SqliteTestDb
{
    private async Task Seed(params string[] names)
    {
        await using var db = NewDb();
        db.Artists.AddRange(names.Select(n => new Artist { Name = n }));
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Search_MatchesPartOfNameIgnoringCase_SortedByName()
    {
        await Seed("Queen", "Queens of the Stone Age", "David Bowie", "Dua Lipa");
        await using var db = NewDb();

        var result = await new ArtistRepository(db).SearchAsync("QUEEN", 50);

        Assert.Equal(["Queen", "Queens of the Stone Age"], result.Select(a => a.Name));
    }

    [Fact]
    public async Task Search_WithoutText_ReturnsFirstByName()
    {
        await Seed("Queen", "ABBA", "Muse");
        await using var db = NewDb();

        var result = await new ArtistRepository(db).SearchAsync(null, 2);

        Assert.Equal(["ABBA", "Muse"], result.Select(a => a.Name));
    }

    [Fact]
    public async Task Search_ReturnsAtMostLimit()
    {
        await Seed(Enumerable.Range(1, 60).Select(i => $"Band {i:D2}").ToArray());
        await using var db = NewDb();

        var result = await new ArtistRepository(db).SearchAsync("band", 50);

        Assert.Equal(50, result.Count);
        Assert.Equal("Band 01", result[0].Name);
    }

    [Fact]
    public async Task TryAdd_ReturnsNullWhenNameIsTakenIgnoringCase()
    {
        await using var db = NewDb();
        var repository = new ArtistRepository(db);

        var id = await repository.TryAddAsync("Queen");

        Assert.NotNull(id);
        Assert.Null(await repository.TryAddAsync("queen"));
        Assert.Equal("Queen", (await repository.GetAsync(id.Value))?.Name);
    }

    [Fact]
    public async Task GetExistingIds_ReturnsOnlyKnownIds()
    {
        await Seed("Queen", "ABBA");
        await using var db = NewDb();
        var all = await new ArtistRepository(db).SearchAsync(null, 50);

        var existing = await new ArtistRepository(db).GetExistingIdsAsync([all[0].Id, all[1].Id, 999]);

        Assert.Equal(all.Select(a => a.Id).ToHashSet(), existing);
    }
}
