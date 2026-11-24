using MusicService.Domain.Dtos;
using MusicService.Domain.Exceptions;
using MusicService.Repository.Interfaces;
using MusicService.Service.Services;
using NSubstitute;

namespace MusicService.Service.Tests;

public class ArtistServiceTests
{
    private readonly IArtistRepository _repository = Substitute.For<IArtistRepository>();
    private readonly ArtistService _service;

    public ArtistServiceTests() => _service = new ArtistService(_repository);

    [Fact]
    public async Task Search_AsksForAtMost50()
    {
        await _service.SearchAsync("que");

        await _repository.Received(1).SearchAsync("que", 50, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_TrimsName()
    {
        _repository.TryAddAsync("Queen", Arg.Any<CancellationToken>()).Returns(3);

        Assert.Equal(new ArtistDto(3, "Queen"), await _service.CreateAsync(new ArtistNameRequest(" Queen ")));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData(null)]
    public async Task Create_RejectsEmptyName(string? name)
    {
        await Assert.ThrowsAsync<MusicValidationException>(() => _service.CreateAsync(new ArtistNameRequest(name!)));
        await _repository.DidNotReceiveWithAnyArgs().TryAddAsync(default!);
    }

    [Fact]
    public async Task Create_RejectsNameLongerThan200()
    {
        await Assert.ThrowsAsync<MusicValidationException>(() =>
            _service.CreateAsync(new ArtistNameRequest(new string('x', 201))));
    }

    [Fact]
    public async Task Create_DuplicateName_IsConflict()
    {
        _repository.TryAddAsync("Queen", Arg.Any<CancellationToken>()).Returns((int?)null);

        await Assert.ThrowsAsync<MusicConflictException>(() => _service.CreateAsync(new ArtistNameRequest("Queen")));
    }

    [Fact]
    public async Task Get_UnknownId_IsNotFound()
    {
        _repository.GetAsync(9, Arg.Any<CancellationToken>()).Returns((ArtistDto?)null);

        await Assert.ThrowsAsync<ArtistNotFoundException>(() => _service.GetAsync(9));
    }
}
