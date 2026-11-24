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

public class SongServiceUploadTests
{
    private readonly ISongRepository _songs = Substitute.For<ISongRepository>();
    private readonly IPlaylistRepository _playlists = Substitute.For<IPlaylistRepository>();
    private readonly IArtistRepository _artists = Substitute.For<IArtistRepository>();
    private readonly IAudioInspector _inspector = Substitute.For<IAudioInspector>();
    private readonly IAmazonS3 _s3 = Substitute.For<IAmazonS3>();
    private readonly SongService _service;

    public SongServiceUploadTests()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 11, 24, 12, 0, 0, TimeSpan.Zero));
        _service = new SongService(_songs, _playlists, _artists, _inspector, _s3,
            new BlobStorageOptions { Bucket = "media", PublicBaseUrl = "https://media.munkcloud.dk" },
            time, new Random(1), NullLogger<SongService>.Instance);

        _playlists.GetAsync(1, Arg.Any<CancellationToken>()).Returns(new PlaylistDto(1, "80s", 0));
        _artists.GetExistingIdsAsync(Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<CancellationToken>())
            .Returns(c => c.Arg<IReadOnlyCollection<int>>().Where(id => id is 10 or 11).ToHashSet());
        _inspector.GetMp3DurationSeconds(Arg.Any<Stream>()).Returns(225);
        _songs.AddAsync(Arg.Any<Song>(), Arg.Any<int>(), Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<CancellationToken>())
            .Returns(c => Saved(c.Arg<Song>()));
    }

    private static Song Saved(Song song)
    {
        song.Id = 99;
        song.SongArtists =
        [
            new SongArtist { Artist = new Artist { Id = 11, Name = "Queen" } },
            new SongArtist { Artist = new Artist { Id = 10, Name = "David Bowie" } }
        ];
        return song;
    }

    private static UploadSongRequest Valid(string title = "Under Pressure", int year = 1981, IReadOnlyList<int>? artistIds = null) =>
        new(title, artistIds ?? [10, 11], year);

    private Task<SongDto> Upload(UploadSongRequest request, string? contentType = "audio/mpeg", Stream? file = null) =>
        _service.UploadAsync(1, request, file ?? new MemoryStream(new byte[100]), contentType);

    [Fact]
    public async Task ValidUpload_StoresFileThenSongAndReturnsDto()
    {
        var song = await Upload(Valid(title: "  Under Pressure "));

        await _s3.Received(1).PutObjectAsync(Arg.Is<PutObjectRequest>(r =>
            r.BucketName == "media"
            && r.Key.StartsWith("songs/") && r.Key.EndsWith(".mp3")
            && r.ContentType == "audio/mpeg"), Arg.Any<CancellationToken>());
        await _songs.Received(1).AddAsync(
            Arg.Is<Song>(s => s.Title == "Under Pressure" && s.Year == 1981 && s.DurationSeconds == 225),
            1, Arg.Is<IReadOnlyCollection<int>>(ids => ids.Order().SequenceEqual(new[] { 10, 11 })),
            Arg.Any<CancellationToken>());

        Assert.Equal(99, song.Id);
        Assert.Equal("Under Pressure", song.Title);
        Assert.Equal(["David Bowie", "Queen"], song.Artists);
        Assert.Equal(225, song.DurationSeconds);
        Assert.Matches(@"^https://media\.munkcloud\.dk/songs/[0-9a-f-]{36}\.mp3$", song.AudioUrl);
    }

    [Fact]
    public async Task FileIsUploadedUnderTheSongsBlobKey()
    {
        Song? saved = null;
        await _songs.AddAsync(Arg.Do<Song>(s => saved = s), Arg.Any<int>(), Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<CancellationToken>());
        PutObjectRequest? put = null;
        await _s3.PutObjectAsync(Arg.Do<PutObjectRequest>(r => put = r), Arg.Any<CancellationToken>());

        await Upload(Valid());

        Assert.Equal($"songs/{saved!.BlobKey}.mp3", put!.Key);
    }

    [Fact]
    public async Task StreamIsRewoundAfterReadingTheLength()
    {
        var file = new MemoryStream(new byte[100]);
        _inspector.GetMp3DurationSeconds(file).Returns(c => { c.Arg<Stream>().Position = 100; return 225; });
        long positionAtUpload = -1;
        await _s3.PutObjectAsync(Arg.Do<PutObjectRequest>(r => positionAtUpload = r.InputStream.Position), Arg.Any<CancellationToken>());

        await Upload(Valid(), file: file);

        Assert.Equal(0, positionAtUpload);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task EmptyTitle_Is400(string title)
    {
        await Assert.ThrowsAsync<MusicValidationException>(() => Upload(Valid(title: title)));
        await AssertNothingStored();
    }

    [Fact]
    public async Task TitleLongerThan200_Is400()
    {
        await Assert.ThrowsAsync<MusicValidationException>(() => Upload(Valid(title: new string('x', 201))));
    }

    [Theory]
    [InlineData(1899)]
    [InlineData(2027)]
    [InlineData(0)]
    public async Task YearOutsideRange_Is400(int year)
    {
        await Assert.ThrowsAsync<MusicValidationException>(() => Upload(Valid(year: year)));
        await AssertNothingStored();
    }

    [Theory]
    [InlineData(1900)]
    [InlineData(2026)]
    public async Task YearAtTheEdges_IsAccepted(int year)
    {
        await Upload(Valid(year: year));
    }

    [Fact]
    public async Task NoArtists_Is400()
    {
        await Assert.ThrowsAsync<MusicValidationException>(() => Upload(Valid(artistIds: [])));
        await AssertNothingStored();
    }

    [Fact]
    public async Task UnknownArtist_Is400AndNamesTheId()
    {
        var e = await Assert.ThrowsAsync<MusicValidationException>(() => Upload(Valid(artistIds: [10, 77])));

        Assert.Contains("77", e.Message);
        await AssertNothingStored();
    }

    [Theory]
    [InlineData("audio/wav")]
    [InlineData("application/octet-stream")]
    [InlineData(null)]
    public async Task WrongContentType_Is400(string? contentType)
    {
        await Assert.ThrowsAsync<MusicValidationException>(() => Upload(Valid(), contentType));
        await AssertNothingStored();
    }

    [Fact]
    public async Task FileThatIsNotAnMp3_Is400()
    {
        _inspector.GetMp3DurationSeconds(Arg.Any<Stream>()).Returns((int?)null);

        await Assert.ThrowsAsync<MusicValidationException>(() => Upload(Valid()));
        await AssertNothingStored();
    }

    [Fact]
    public async Task UnknownPlaylist_Is404()
    {
        await Assert.ThrowsAsync<PlaylistNotFoundException>(() =>
            _service.UploadAsync(2, Valid(), new MemoryStream(new byte[100]), "audio/mpeg"));
        await AssertNothingStored();
    }

    [Fact]
    public async Task DatabaseFailure_DeletesTheUploadedFileAndRethrows()
    {
        PutObjectRequest? put = null;
        await _s3.PutObjectAsync(Arg.Do<PutObjectRequest>(r => put = r), Arg.Any<CancellationToken>());
        _songs.AddAsync(Arg.Any<Song>(), Arg.Any<int>(), Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("database down"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => Upload(Valid()));

        await _s3.Received(1).DeleteObjectAsync(
            Arg.Is<DeleteObjectRequest>(r => r.BucketName == "media" && r.Key == put!.Key), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UploadFailure_SavesNoSong()
    {
        _s3.PutObjectAsync(Arg.Any<PutObjectRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new AmazonS3Exception("unavailable"));

        await Assert.ThrowsAsync<AmazonS3Exception>(() => Upload(Valid()));

        await _songs.DidNotReceiveWithAnyArgs().AddAsync(default!, default, default!);
    }

    private async Task AssertNothingStored()
    {
        await _s3.DidNotReceiveWithAnyArgs().PutObjectAsync(default(PutObjectRequest)!);
        await _songs.DidNotReceiveWithAnyArgs().AddAsync(default!, default, default!);
    }
}
