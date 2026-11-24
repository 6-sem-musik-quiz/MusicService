using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging.Abstractions;
using MusicService.Domain.Dtos;
using MusicService.Domain.Exceptions;
using MusicService.Repository.BlobStorage;
using MusicService.Repository.Interfaces;
using MusicService.Service.Services;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace MusicService.Service.Tests;

public class PlaylistServiceTests
{
    private readonly IPlaylistRepository _repository = Substitute.For<IPlaylistRepository>();
    private readonly IAmazonS3 _s3 = Substitute.For<IAmazonS3>();
    private readonly PlaylistService _service;

    public PlaylistServiceTests()
    {
        _service = new PlaylistService(_repository, _s3, new BlobStorageOptions { Bucket = "media" },
            NullLogger<PlaylistService>.Instance);
    }

    [Fact]
    public async Task Create_TrimsNameAndReturnsEmptyPlaylist()
    {
        _repository.TryAddAsync("80s", Arg.Any<CancellationToken>()).Returns(7);

        var playlist = await _service.CreateAsync(new PlaylistNameRequest("  80s "));

        Assert.Equal(new PlaylistDto(7, "80s", 0), playlist);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task Create_RejectsEmptyName(string? name)
    {
        await Assert.ThrowsAsync<MusicValidationException>(() => _service.CreateAsync(new PlaylistNameRequest(name!)));
        await _repository.DidNotReceiveWithAnyArgs().TryAddAsync(default!);
    }

    [Fact]
    public async Task Create_RejectsNameLongerThan60()
    {
        await Assert.ThrowsAsync<MusicValidationException>(() =>
            _service.CreateAsync(new PlaylistNameRequest(new string('x', 61))));
    }

    [Fact]
    public async Task Create_Accepts60Characters()
    {
        var name = new string('x', 60);
        _repository.TryAddAsync(name, Arg.Any<CancellationToken>()).Returns(1);

        Assert.Equal(name, (await _service.CreateAsync(new PlaylistNameRequest(name))).Name);
    }

    [Fact]
    public async Task Create_DuplicateName_IsConflict()
    {
        _repository.TryAddAsync("Rock", Arg.Any<CancellationToken>()).Returns((int?)null);

        await Assert.ThrowsAsync<MusicConflictException>(() => _service.CreateAsync(new PlaylistNameRequest("Rock")));
    }

    [Fact]
    public async Task Get_UnknownId_IsNotFound()
    {
        _repository.GetAsync(5, Arg.Any<CancellationToken>()).Returns((PlaylistDto?)null);

        await Assert.ThrowsAsync<PlaylistNotFoundException>(() => _service.GetAsync(5));
    }

    [Fact]
    public async Task Rename_ReturnsUpdatedPlaylist()
    {
        _repository.TryRenameAsync(3, "Classic Rock", Arg.Any<CancellationToken>()).Returns(RenamePlaylistResult.Renamed);
        _repository.GetAsync(3, Arg.Any<CancellationToken>()).Returns(new PlaylistDto(3, "Classic Rock", 12));

        var playlist = await _service.RenameAsync(3, new PlaylistNameRequest("Classic Rock"));

        Assert.Equal(new PlaylistDto(3, "Classic Rock", 12), playlist);
    }

    [Fact]
    public async Task Rename_UnknownId_IsNotFound()
    {
        _repository.TryRenameAsync(3, "Jazz", Arg.Any<CancellationToken>()).Returns(RenamePlaylistResult.NotFound);

        await Assert.ThrowsAsync<PlaylistNotFoundException>(() => _service.RenameAsync(3, new PlaylistNameRequest("Jazz")));
    }

    [Fact]
    public async Task Rename_TakenName_IsConflict()
    {
        _repository.TryRenameAsync(3, "Pop", Arg.Any<CancellationToken>()).Returns(RenamePlaylistResult.NameTaken);

        await Assert.ThrowsAsync<MusicConflictException>(() => _service.RenameAsync(3, new PlaylistNameRequest("Pop")));
    }

    [Fact]
    public async Task Rename_InvalidName_IsValidationError()
    {
        await Assert.ThrowsAsync<MusicValidationException>(() => _service.RenameAsync(3, new PlaylistNameRequest("")));
    }

    [Fact]
    public async Task Delete_DeletesTheFilesOfTheDeletedSongs()
    {
        var onlyHere = Guid.NewGuid();
        _repository.DeleteAsync(4, Arg.Any<CancellationToken>()).Returns([onlyHere]);

        await _service.DeleteAsync(4);

        await _s3.Received(1).DeleteObjectAsync(
            Arg.Is<DeleteObjectRequest>(r => r.BucketName == "media" && r.Key == $"songs/{onlyHere}.mp3"),
            Arg.Any<CancellationToken>());
        await _s3.Received(1).DeleteObjectAsync(Arg.Any<DeleteObjectRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Delete_PlaylistWithOnlySharedSongs_DeletesNoFiles()
    {
        _repository.DeleteAsync(4, Arg.Any<CancellationToken>()).Returns([]);

        await _service.DeleteAsync(4);

        await _s3.DidNotReceiveWithAnyArgs().DeleteObjectAsync(default(DeleteObjectRequest)!);
    }

    [Fact]
    public async Task Delete_UnknownId_IsNotFoundAndTouchesNoFiles()
    {
        _repository.DeleteAsync(4, Arg.Any<CancellationToken>()).Returns((IReadOnlyList<Guid>?)null);

        await Assert.ThrowsAsync<PlaylistNotFoundException>(() => _service.DeleteAsync(4));
        await _s3.DidNotReceiveWithAnyArgs().DeleteObjectAsync(default(DeleteObjectRequest)!);
    }

    [Fact]
    public async Task Delete_FileThatCannotBeDeleted_DoesNotFailOrStopTheRest()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        _repository.DeleteAsync(4, Arg.Any<CancellationToken>()).Returns([first, second]);
        _s3.DeleteObjectAsync(Arg.Is<DeleteObjectRequest>(r => r.Key.Contains(first.ToString())), Arg.Any<CancellationToken>())
            .ThrowsAsync(new AmazonS3Exception("Access Denied"));

        await _service.DeleteAsync(4);

        await _s3.Received(1).DeleteObjectAsync(
            Arg.Is<DeleteObjectRequest>(r => r.Key == $"songs/{second}.mp3"), Arg.Any<CancellationToken>());
    }
}
