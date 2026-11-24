using MusicService.Repository.BlobStorage;

namespace MusicService.Service.Tests.Repository;

public class BlobStorageOptionsTests
{
    private static readonly Guid BlobKey = Guid.Parse("3f2c8a4e-1b7d-4c55-9a0e-6d2b1f8e7c10");

    [Fact]
    public void SongObjectKey_IsSongsFolderAndMp3()
    {
        Assert.Equal("songs/3f2c8a4e-1b7d-4c55-9a0e-6d2b1f8e7c10.mp3", BlobStorageOptions.SongObjectKey(BlobKey));
    }

    [Theory]
    [InlineData("https://media.munkcloud.dk")]
    [InlineData("https://media.munkcloud.dk/")]
    public void PublicSongUrl_IsBaseUrlSlashKey(string baseUrl)
    {
        var options = new BlobStorageOptions { PublicBaseUrl = baseUrl };

        Assert.Equal(
            "https://media.munkcloud.dk/songs/3f2c8a4e-1b7d-4c55-9a0e-6d2b1f8e7c10.mp3",
            options.PublicSongUrl(BlobKey));
    }
}
