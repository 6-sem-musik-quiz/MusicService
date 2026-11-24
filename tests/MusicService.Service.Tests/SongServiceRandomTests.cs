using Amazon.S3;
using Microsoft.Extensions.Logging.Abstractions;
using MusicService.Domain.Dtos;
using MusicService.Domain.Entities;
using MusicService.Domain.Exceptions;
using MusicService.Repository.BlobStorage;
using MusicService.Repository.Interfaces;
using MusicService.Service.Interfaces;
using MusicService.Service.Services;
using NSubstitute;

namespace MusicService.Service.Tests;

public class SongServiceRandomTests
{
    private const int Seed = 42;
    private static readonly int[] PlaylistSongIds = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10];

    private readonly ISongRepository _songs = Substitute.For<ISongRepository>();
    private readonly IPlaylistRepository _playlists = Substitute.For<IPlaylistRepository>();
    private readonly SongService _service;

    public SongServiceRandomTests()
    {
        _service = new SongService(_songs, _playlists, Substitute.For<IArtistRepository>(),
            Substitute.For<IAudioInspector>(), Substitute.For<IAmazonS3>(),
            new BlobStorageOptions { PublicBaseUrl = "https://media.munkcloud.dk" },
            TimeProvider.System, new Random(Seed), NullLogger<SongService>.Instance);

        _playlists.GetAsync(1, Arg.Any<CancellationToken>()).Returns(new PlaylistDto(1, "80s", PlaylistSongIds.Length));
        _songs.GetIdsInPlaylistAsync(1, Arg.Any<CancellationToken>()).Returns(PlaylistSongIds);
        // Returned in id order on purpose: the service must restore the random order itself.
        _songs.GetWithArtistsAsync(Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<CancellationToken>())
            .Returns(c => c.Arg<IReadOnlyCollection<int>>().Order().Select(NewSong).ToList());
    }

    private static Song NewSong(int id) => new()
    {
        Id = id,
        Title = $"Song {id}",
        Year = 1980 + id,
        DurationSeconds = 200,
        BlobKey = Guid.NewGuid(),
        SongArtists = [new SongArtist { Artist = new Artist { Name = $"Artist {id}" } }]
    };

    [Fact]
    public async Task ReturnsCountDistinctSongsInTheShuffledOrder()
    {
        var expected = PlaylistSongIds.ToArray();
        new Random(Seed).Shuffle(expected);

        var songs = await _service.GetRandomAsync(1, 5);

        Assert.Equal(expected[..5], songs.Select(s => s.Id));
        Assert.Equal(5, songs.Select(s => s.Id).Distinct().Count());
    }

    [Fact]
    public async Task SongsHaveYearArtistsAndFullAudioUrl()
    {
        var song = (await _service.GetRandomAsync(1, 1))[0];

        Assert.Equal(1980 + song.Id, song.Year);
        Assert.Equal([$"Artist {song.Id}"], song.Artists);
        Assert.StartsWith("https://media.munkcloud.dk/songs/", song.AudioUrl);
    }

    [Fact]
    public async Task AllSongsOfThePlaylist_IsAllowed()
    {
        var songs = await _service.GetRandomAsync(1, PlaylistSongIds.Length);

        Assert.Equal(PlaylistSongIds, songs.Select(s => s.Id).Order());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)]
    public async Task CountOutsideRange_Is400(int count)
    {
        await Assert.ThrowsAsync<MusicValidationException>(() => _service.GetRandomAsync(1, count));
    }

    [Fact]
    public async Task TooFewSongs_Is400AndSaysHowManyThereAre()
    {
        var e = await Assert.ThrowsAsync<MusicValidationException>(() => _service.GetRandomAsync(1, 11));

        Assert.Contains("has 10 songs", e.Message);
    }

    [Fact]
    public async Task UnknownPlaylist_Is404()
    {
        await Assert.ThrowsAsync<PlaylistNotFoundException>(() => _service.GetRandomAsync(2, 5));
    }
}
