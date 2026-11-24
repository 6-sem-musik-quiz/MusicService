using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using MusicService.Domain.Dtos;
using MusicService.Domain.Exceptions;
using MusicService.Service.Interfaces;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace MusicService.Api.Tests;

// HTTP contract of GET /api/playlists/{id}/songs/random?count=n (SCRUM-95).
public class PlaylistSongsRandomTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly ISongService _service = Substitute.For<ISongService>();

    private HttpClient Client(string? token) => TestApp.Client(factory, token, s => s.AddScoped(_ => _service));

    [Fact]
    public async Task AsPlayer_Returns200WithSongs()
    {
        _service.GetRandomAsync(1, 2, Arg.Any<CancellationToken>()).Returns([
            new SongDto(3, "Take On Me", ["a-ha"], 1985, 225, "https://media.munkcloud.dk/songs/x.mp3"),
            new SongDto(1, "Under Pressure", ["David Bowie", "Queen"], 1981, 248, "https://media.munkcloud.dk/songs/y.mp3")
        ]);

        var response = await Client(TestTokens.Player()).GetAsync("/api/playlists/1/songs/random?count=2");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var songs = await response.Content.ReadFromJsonAsync<List<SongDto>>();
        Assert.Equal([3, 1], songs!.Select(s => s.Id));
        Assert.Equal(1985, songs[0].Year);
    }

    [Fact]
    public async Task TooFewSongs_Returns400()
    {
        _service.GetRandomAsync(1, 50, Arg.Any<CancellationToken>())
            .ThrowsAsync(new MusicValidationException("The playlist has 10 songs, but 50 were requested."));

        var response = await Client(TestTokens.Player()).GetAsync("/api/playlists/1/songs/random?count=50");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("has 10 songs", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task MissingCount_IsPassedAsZero()
    {
        _service.GetRandomAsync(1, 0, Arg.Any<CancellationToken>())
            .ThrowsAsync(new MusicValidationException("count must be between 1 and 100."));

        var response = await Client(TestTokens.Player()).GetAsync("/api/playlists/1/songs/random");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UnknownPlaylist_Returns404()
    {
        _service.GetRandomAsync(9, 5, Arg.Any<CancellationToken>()).ThrowsAsync(new PlaylistNotFoundException(9));

        var response = await Client(TestTokens.Player()).GetAsync("/api/playlists/9/songs/random?count=5");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task NoToken_Returns401()
    {
        var response = await Client(token: null).GetAsync("/api/playlists/1/songs/random?count=5");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
