using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using MusicService.Domain.Dtos;
using MusicService.Domain.Exceptions;
using MusicService.Service.Interfaces;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace MusicService.Api.Tests;

// HTTP contract of /api/playlists (SCRUM-92): status codes, auth and error mapping.
// The service is faked; its rules are tested in PlaylistServiceTests.
public class PlaylistsControllerTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly IPlaylistService _service = Substitute.For<IPlaylistService>();

    private HttpClient Client(string? token)
    {
        var client = factory
            .WithWebHostBuilder(b =>
            {
                b.UseSetting("Database:MigrateOnStartup", "false");
                b.UseSetting("Jwt:SigningKey", TestTokens.SigningKey);
                b.ConfigureTestServices(s => s.AddScoped(_ => _service));
            })
            .CreateClient();
        if (token is not null)
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    [Fact]
    public async Task GetAll_AsPlayer_Returns200WithList()
    {
        _service.GetAllAsync(Arg.Any<CancellationToken>()).Returns([new PlaylistDto(1, "80s", 12)]);

        var response = await Client(TestTokens.Player()).GetAsync("/api/playlists");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal([new PlaylistDto(1, "80s", 12)], await response.Content.ReadFromJsonAsync<List<PlaylistDto>>());
    }

    [Fact]
    public async Task GetAll_UsesCamelCaseLikeLobbyServiceExpects()
    {
        _service.GetAllAsync(Arg.Any<CancellationToken>()).Returns([new PlaylistDto(1, "80s", 12)]);

        var json = await Client(TestTokens.Player()).GetStringAsync("/api/playlists");

        Assert.Equal("""[{"id":1,"name":"80s","songCount":12}]""", json);
    }

    [Theory]
    [InlineData("GET", "/api/playlists")]
    [InlineData("GET", "/api/playlists/1")]
    [InlineData("POST", "/api/playlists")]
    [InlineData("PUT", "/api/playlists/1")]
    [InlineData("DELETE", "/api/playlists/1")]
    public async Task NoToken_Returns401(string method, string path)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (method is "POST" or "PUT")
            request.Content = JsonContent.Create(new PlaylistNameRequest("80s"));

        var response = await Client(token: null).SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetById_Returns200()
    {
        _service.GetAsync(1, Arg.Any<CancellationToken>()).Returns(new PlaylistDto(1, "80s", 12));

        var response = await Client(TestTokens.Player()).GetAsync("/api/playlists/1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(new PlaylistDto(1, "80s", 12), await response.Content.ReadFromJsonAsync<PlaylistDto>());
    }

    [Fact]
    public async Task GetById_Unknown_Returns404ProblemDetails()
    {
        _service.GetAsync(9, Arg.Any<CancellationToken>()).ThrowsAsync(new PlaylistNotFoundException(9));

        var response = await Client(TestTokens.Player()).GetAsync("/api/playlists/9");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("Playlist 9 was not found.", problem?.Detail);
    }

    [Fact]
    public async Task Create_AsAdmin_Returns201WithLocation()
    {
        _service.CreateAsync(new PlaylistNameRequest("80s"), Arg.Any<CancellationToken>()).Returns(new PlaylistDto(5, "80s", 0));

        var response = await Client(TestTokens.Admin()).PostAsJsonAsync("/api/playlists", new PlaylistNameRequest("80s"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("/api/playlists/5", response.Headers.Location?.AbsolutePath);
        Assert.Equal(new PlaylistDto(5, "80s", 0), await response.Content.ReadFromJsonAsync<PlaylistDto>());
    }

    [Fact]
    public async Task Create_AsPlayer_Returns403()
    {
        var response = await Client(TestTokens.Player()).PostAsJsonAsync("/api/playlists", new PlaylistNameRequest("80s"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await _service.DidNotReceiveWithAnyArgs().CreateAsync(default!);
    }

    [Fact]
    public async Task Create_InvalidName_Returns400()
    {
        _service.CreateAsync(Arg.Any<PlaylistNameRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new MusicValidationException("name must be between 1 and 60 characters."));

        var response = await Client(TestTokens.Admin()).PostAsJsonAsync("/api/playlists", new PlaylistNameRequest(""));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_MissingBody_Returns400()
    {
        var response = await Client(TestTokens.Admin()).PostAsync("/api/playlists",
            new StringContent("{}", System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_DuplicateName_Returns409()
    {
        _service.CreateAsync(Arg.Any<PlaylistNameRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new MusicConflictException("A playlist named \"80s\" already exists."));

        var response = await Client(TestTokens.Admin()).PostAsJsonAsync("/api/playlists", new PlaylistNameRequest("80s"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Rename_AsAdmin_Returns200()
    {
        _service.RenameAsync(5, new PlaylistNameRequest("Eighties"), Arg.Any<CancellationToken>())
            .Returns(new PlaylistDto(5, "Eighties", 3));

        var response = await Client(TestTokens.Admin()).PutAsJsonAsync("/api/playlists/5", new PlaylistNameRequest("Eighties"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(new PlaylistDto(5, "Eighties", 3), await response.Content.ReadFromJsonAsync<PlaylistDto>());
    }

    [Theory]
    [InlineData(400)]
    [InlineData(404)]
    [InlineData(409)]
    public async Task Rename_Errors(int expectedStatus)
    {
        Exception error = expectedStatus switch
        {
            400 => new MusicValidationException("bad"),
            404 => new PlaylistNotFoundException(5),
            _ => new MusicConflictException("taken")
        };
        _service.RenameAsync(5, Arg.Any<PlaylistNameRequest>(), Arg.Any<CancellationToken>()).ThrowsAsync(error);

        var response = await Client(TestTokens.Admin()).PutAsJsonAsync("/api/playlists/5", new PlaylistNameRequest("x"));

        Assert.Equal(expectedStatus, (int)response.StatusCode);
    }

    [Fact]
    public async Task Rename_AsPlayer_Returns403()
    {
        var response = await Client(TestTokens.Player()).PutAsJsonAsync("/api/playlists/5", new PlaylistNameRequest("x"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Delete_AsAdmin_Returns204()
    {
        var response = await Client(TestTokens.Admin()).DeleteAsync("/api/playlists/5");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        await _service.Received(1).DeleteAsync(5, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Delete_Unknown_Returns404()
    {
        _service.DeleteAsync(5, Arg.Any<CancellationToken>()).ThrowsAsync(new PlaylistNotFoundException(5));

        var response = await Client(TestTokens.Admin()).DeleteAsync("/api/playlists/5");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Delete_AsPlayer_Returns403()
    {
        var response = await Client(TestTokens.Player()).DeleteAsync("/api/playlists/5");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await _service.DidNotReceiveWithAnyArgs().DeleteAsync(default);
    }
}
