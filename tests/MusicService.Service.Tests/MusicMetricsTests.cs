using System.Diagnostics.Metrics;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging.Abstractions;
using MusicService.Domain.Dtos;
using MusicService.Domain.Entities;
using MusicService.Repository.BlobStorage;
using MusicService.Repository.Interfaces;
using MusicService.Service.Interfaces;
using MusicService.Service.Metrics;
using MusicService.Service.Services;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace MusicService.Service.Tests;

// SCRUM-173: slopster_song_draws_total counts the random draws for a game, by playlist, and
// slopster_blob_errors_total the failed calls to blob storage.
public class MusicMetricsTests : IDisposable
{
    private readonly ISongRepository _songs = Substitute.For<ISongRepository>();
    private readonly IPlaylistRepository _playlists = Substitute.For<IPlaylistRepository>();
    private readonly MusicMetrics _metrics = new();
    private readonly MeterListener _listener = new();
    private readonly List<(string Name, object? PlaylistId)> _measurements = [];
    private readonly SongService _service;

    public MusicMetricsTests()
    {
        _service = new SongService(_songs, _playlists, Substitute.For<IArtistRepository>(),
            Substitute.For<IAudioInspector>(), Substitute.For<IAmazonS3>(),
            new BlobStorageOptions { PublicBaseUrl = "https://media.munkcloud.dk" },
            TimeProvider.System, new Random(1), NullLogger<SongService>.Instance, _metrics);
        foreach (var id in new[] { 1, 2 })
        {
            _playlists.GetAsync(id, Arg.Any<CancellationToken>()).Returns(new PlaylistDto(id, $"P{id}", 3));
            _songs.GetIdsInPlaylistAsync(id, Arg.Any<CancellationToken>()).Returns([1, 2, 3]);
        }
        _songs.GetWithArtistsAsync(Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<CancellationToken>())
            .Returns(c => c.Arg<IReadOnlyCollection<int>>().Select(i => new Song
            {
                Id = i, Title = $"Song {i}", Year = 1980 + i, DurationSeconds = 200, BlobKey = Guid.NewGuid(), SongArtists = []
            }).ToList());

        // Only this test's meter: other tests may create their own at the same time.
        _listener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter == _metrics.Meter) listener.EnableMeasurementEvents(instrument);
        };
        _listener.SetMeasurementEventCallback<long>((instrument, _, tags, _) =>
        {
            object? playlist = null;
            foreach (var tag in tags)
                if (tag.Key == "playlist_id") playlist = tag.Value;
            _measurements.Add((instrument.Name, playlist));
        });
        _listener.Start();
    }

    public void Dispose()
    {
        _listener.Dispose();
        _metrics.Dispose();
    }

    [Fact]
    public async Task EachRandomDraw_IsCountedForItsPlaylist()
    {
        await _service.GetRandomAsync(1, 3);
        await _service.GetRandomAsync(2, 2);
        await _service.GetRandomAsync(1, 1);

        Assert.Equal([(MusicMetrics.SongDrawsName, (object?)1), (MusicMetrics.SongDrawsName, 2), (MusicMetrics.SongDrawsName, 1)],
            _measurements);
    }

    [Fact]
    public async Task AFailedDraw_IsNotCounted()
    {
        await Assert.ThrowsAnyAsync<Exception>(() => _service.GetRandomAsync(1, 10)); // only 3 songs

        Assert.Empty(_measurements);
    }

    [Fact]
    public async Task AFailedUpload_IsCountedAsABlobError()
    {
        var s3 = Substitute.For<IAmazonS3>();
        s3.PutObjectAsync(Arg.Any<PutObjectRequest>(), Arg.Any<CancellationToken>()).ThrowsAsync(new AmazonS3Exception("unavailable"));
        var artists = Substitute.For<IArtistRepository>();
        artists.GetExistingIdsAsync(Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<CancellationToken>()).Returns(new HashSet<int> { 10 });
        var inspector = Substitute.For<IAudioInspector>();
        inspector.GetMp3DurationSeconds(Arg.Any<Stream>()).Returns(200);
        var service = new SongService(_songs, _playlists, artists, inspector, s3,
            new BlobStorageOptions { Bucket = "media", PublicBaseUrl = "https://media.munkcloud.dk" },
            TimeProvider.System, new Random(1), NullLogger<SongService>.Instance, _metrics);

        await Assert.ThrowsAsync<AmazonS3Exception>(() =>
            service.UploadAsync(1, new UploadSongRequest("Under Pressure", [10], 1981), new MemoryStream(new byte[100]), "audio/mpeg"));

        Assert.Equal([(MusicMetrics.BlobErrorsName, (object?)null)], _measurements);
    }
}
