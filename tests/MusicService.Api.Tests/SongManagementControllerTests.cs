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

// HTTP contract of SCRUM-94: GET /api/playlists/{id}/songs, PUT /api/songs/{id},
// DELETE /api/playlists/{id}/songs/{songId}. All admin only.
public class SongManagementControllerTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly SongDto Song = new(5, "Take On Me", ["a-ha"], 1985, 225, "https://media.munkcloud.dk/songs/x.mp3");

    private readonly ISongService _service = Substitute.For<ISongService>();

    private HttpClient Client(string? token) => TestApp.Client(factory, token, s => s.AddScoped(_ => _service));

    [Fact]
    public async Task List_AsAdmin_Returns200WithAudioUrl()
    {
        _service.GetInPlaylistAsync(1, Arg.Any<CancellationToken>()).Returns([Song]);

        var json = await Client(TestTokens.Admin()).GetStringAsync("/api/playlists/1/songs");

        Assert.Equal(
            """[{"id":5,"title":"Take On Me","artists":["a-ha"],"year":1985,"durationSeconds":225,"audioUrl":"https://media.munkcloud.dk/songs/x.mp3"}]""",
            json);
    }

    [Fact]
    public async Task List_UnknownPlaylist_Returns404()
    {
        _service.GetInPlaylistAsync(9, Arg.Any<CancellationToken>()).ThrowsAsync(new PlaylistNotFoundException(9));

        var response = await Client(TestTokens.Admin()).GetAsync("/api/playlists/9/songs");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Update_AsAdmin_Returns200()
    {
        _service.UpdateAsync(5, Arg.Is<UpdateSongRequest>(r => r.Title == "Take On Me" && r.Year == 1985 && r.ArtistIds.SequenceEqual(new[] { 3 })),
            Arg.Any<CancellationToken>()).Returns(Song);

        var response = await Client(TestTokens.Admin()).PutAsJsonAsync("/api/songs/5", new { title = "Take On Me", artistIds = new[] { 3 }, year = 1985 });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(5, (await response.Content.ReadFromJsonAsync<SongDto>())?.Id);
    }

    [Theory]
    [InlineData(400)]
    [InlineData(404)]
    public async Task Update_Errors(int expected)
    {
        Exception error = expected == 400 ? new MusicValidationException("bad") : new SongNotFoundException(5);
        _service.UpdateAsync(5, Arg.Any<UpdateSongRequest>(), Arg.Any<CancellationToken>()).ThrowsAsync(error);

        var response = await Client(TestTokens.Admin()).PutAsJsonAsync("/api/songs/5", new { title = "x", artistIds = new[] { 3 }, year = 1985 });

        Assert.Equal(expected, (int)response.StatusCode);
    }

    [Fact]
    public async Task Remove_AsAdmin_Returns204()
    {
        var response = await Client(TestTokens.Admin()).DeleteAsync("/api/playlists/1/songs/5");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        await _service.Received(1).RemoveFromPlaylistAsync(1, 5, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Remove_NotInPlaylist_Returns404()
    {
        _service.RemoveFromPlaylistAsync(1, 5, Arg.Any<CancellationToken>())
            .ThrowsAsync(new MusicNotFoundException("Song 5 is not in playlist 1."));

        var response = await Client(TestTokens.Admin()).DeleteAsync("/api/playlists/1/songs/5");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("GET", "/api/playlists/1/songs")]
    [InlineData("PUT", "/api/songs/5")]
    [InlineData("DELETE", "/api/playlists/1/songs/5")]
    public async Task AsPlayer_Returns403(string method, string path)
    {
        var response = await Client(TestTokens.Player()).SendAsync(Request(method, path));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("GET", "/api/playlists/1/songs")]
    [InlineData("PUT", "/api/songs/5")]
    [InlineData("DELETE", "/api/playlists/1/songs/5")]
    public async Task NoToken_Returns401(string method, string path)
    {
        var response = await Client(token: null).SendAsync(Request(method, path));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static HttpRequestMessage Request(string method, string path) => new(new HttpMethod(method), path)
    {
        Content = method == "PUT" ? JsonContent.Create(new { title = "x", artistIds = new[] { 3 }, year = 1985 }) : null
    };
}
