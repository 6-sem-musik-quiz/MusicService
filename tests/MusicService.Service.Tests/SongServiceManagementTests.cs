using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using MusicService.Domain.Dtos;
using MusicService.Domain.Entities;
using MusicService.Domain.Exceptions;
using MusicService.Repository.BlobStorage;
using MusicService.Repository.Interfaces;
using MusicService.Service.Interfaces;
using MusicService.Service.Services;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace MusicService.Service.Tests;

// SCRUM-94: list, edit and remove songs.
public class SongServiceManagementTests
{
    private readonly ISongRepository _songs = Substitute.For<ISongRepository>();
    private readonly IPlaylistRepository _playlists = Substitute.For<IPlaylistRepository>();
    private readonly IArtistRepository _artists = Substitute.For<IArtistRepository>();
    private readonly IAmazonS3 _s3 = Substitute.For<IAmazonS3>();
    private readonly SongService _service;

    public SongServiceManagementTests()
    {
        _service = new SongService(_songs, _playlists, _artists, Substitute.For<IAudioInspector>(), _s3,
            new BlobStorageOptions { Bucket = "media", PublicBaseUrl = "https://media.munkcloud.dk" },
            new FakeTimeProvider(new DateTimeOffset(2026, 11, 24, 0, 0, 0, TimeSpan.Zero)), new Random(1),
            NullLogger<SongService>.Instance);

        _playlists.GetAsync(1, Arg.Any<CancellationToken>()).Returns(new PlaylistDto(1, "80s", 2));
        _artists.GetExistingIdsAsync(Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<CancellationToken>())
            .Returns(c => c.Arg<IReadOnlyCollection<int>>().Where(id => id is 10 or 11).ToHashSet());
    }

    private static Song NewSong(int id, string title, params string[] artists) => new()
    {
        Id = id,
        Title = title,
        Year = 1985,
        DurationSeconds = 225,
        BlobKey = Guid.Parse("3f2c8a4e-1b7d-4c55-9a0e-6d2b1f8e7c10"),
        SongArtists = artists.Select(a => new SongArtist { Artist = new Artist { Name = a } }).ToList()
    };

    [Fact]
    public async Task GetInPlaylist_ReturnsSongsWithAudioUrl()
    {
        _songs.GetInPlaylistAsync(1, Arg.Any<CancellationToken>()).Returns([NewSong(5, "Take On Me", "a-ha")]);

        var songs = await _service.GetInPlaylistAsync(1);

        Assert.Equal(
            [new SongDto(5, "Take On Me", ["a-ha"], 1985, 225,
                "https://media.munkcloud.dk/songs/3f2c8a4e-1b7d-4c55-9a0e-6d2b1f8e7c10.mp3")],
            songs, new SongDtoComparer());
    }

    [Fact]
    public async Task GetInPlaylist_UnknownPlaylist_Is404()
    {
        await Assert.ThrowsAsync<PlaylistNotFoundException>(() => _service.GetInPlaylistAsync(2));
    }

    [Fact]
    public async Task Update_PassesTrimmedFieldsAndReturnsSong()
    {
        _songs.UpdateAsync(5, "Under Pressure", 1981, Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<CancellationToken>())
            .Returns(NewSong(5, "Under Pressure", "Queen", "David Bowie"));

        var song = await _service.UpdateAsync(5, new UpdateSongRequest(" Under Pressure ", [11, 10, 11], 1981));

        await _songs.Received(1).UpdateAsync(5, "Under Pressure", 1981,
            Arg.Is<IReadOnlyCollection<int>>(ids => ids.Order().SequenceEqual(new[] { 10, 11 })), Arg.Any<CancellationToken>());
        Assert.Equal(["David Bowie", "Queen"], song.Artists);
    }

    [Fact]
    public async Task Update_UnknownSong_Is404()
    {
        _songs.UpdateAsync(5, Arg.Any<string>(), Arg.Any<int>(), Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<CancellationToken>())
            .Returns((Song?)null);

        await Assert.ThrowsAsync<SongNotFoundException>(() => _service.UpdateAsync(5, new UpdateSongRequest("x", [10], 2000)));
    }

    [Theory]
    [InlineData("", 2000, new[] { 10 })]
    [InlineData("Title", 1899, new[] { 10 })]
    [InlineData("Title", 2027, new[] { 10 })]
    [InlineData("Title", 2000, new int[0])]
    [InlineData("Title", 2000, new[] { 10, 77 })]
    public async Task Update_InvalidInput_Is400AndChangesNothing(string title, int year, int[] artistIds)
    {
        await Assert.ThrowsAsync<MusicValidationException>(() =>
            _service.UpdateAsync(5, new UpdateSongRequest(title, artistIds, year)));

        await _songs.DidNotReceiveWithAnyArgs().UpdateAsync(default, default!, default, default!);
    }

    [Fact]
    public async Task Remove_NotInPlaylist_Is404()
    {
        _songs.RemoveFromPlaylistAsync(1, 5, Arg.Any<CancellationToken>()).Returns(new RemoveSongResult(false, null));

        await Assert.ThrowsAsync<MusicNotFoundException>(() => _service.RemoveFromPlaylistAsync(1, 5));
        await _s3.DidNotReceiveWithAnyArgs().DeleteObjectAsync(default(DeleteObjectRequest)!);
    }

    [Fact]
    public async Task Remove_SharedSong_KeepsTheFile()
    {
        _songs.RemoveFromPlaylistAsync(1, 5, Arg.Any<CancellationToken>()).Returns(new RemoveSongResult(true, null));

        await _service.RemoveFromPlaylistAsync(1, 5);

        await _s3.DidNotReceiveWithAnyArgs().DeleteObjectAsync(default(DeleteObjectRequest)!);
    }

    [Fact]
    public async Task Remove_DeletedSong_DeletesTheFile()
    {
        var blobKey = Guid.NewGuid();
        _songs.RemoveFromPlaylistAsync(1, 5, Arg.Any<CancellationToken>()).Returns(new RemoveSongResult(true, blobKey));

        await _service.RemoveFromPlaylistAsync(1, 5);

        await _s3.Received(1).DeleteObjectAsync(
            Arg.Is<DeleteObjectRequest>(r => r.BucketName == "media" && r.Key == $"songs/{blobKey}.mp3"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Remove_FileThatCannotBeDeleted_DoesNotFail()
    {
        _songs.RemoveFromPlaylistAsync(1, 5, Arg.Any<CancellationToken>()).Returns(new RemoveSongResult(true, Guid.NewGuid()));
        _s3.DeleteObjectAsync(Arg.Any<DeleteObjectRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new AmazonS3Exception("unavailable"));

        await _service.RemoveFromPlaylistAsync(1, 5);
    }

    // SongDto holds a list, so record equality compares references; compare the values instead.
    private sealed class SongDtoComparer : IEqualityComparer<SongDto>
    {
        public bool Equals(SongDto? x, SongDto? y) =>
            x is not null && y is not null && x with { Artists = [] } == y with { Artists = [] } && x.Artists.SequenceEqual(y.Artists);

        public int GetHashCode(SongDto obj) => obj.Id;
    }
}
