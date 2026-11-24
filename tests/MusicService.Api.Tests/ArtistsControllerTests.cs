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

// HTTP contract of /api/artists (SCRUM-123). The service is faked; its rules are in ArtistServiceTests.
public class ArtistsControllerTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly IArtistService _service = Substitute.For<IArtistService>();

    private HttpClient Client(string? token) => TestApp.Client(factory, token, s => s.AddScoped(_ => _service));

    [Fact]
    public async Task Search_AsPlayer_Returns200()
    {
        _service.SearchAsync("que", Arg.Any<CancellationToken>()).Returns([new ArtistDto(1, "Queen")]);

        var json = await Client(TestTokens.Player()).GetStringAsync("/api/artists?search=que");

        Assert.Equal("""[{"id":1,"name":"Queen"}]""", json);
    }

    [Fact]
    public async Task Search_WithoutSearch_PassesNull()
    {
        _service.SearchAsync(null, Arg.Any<CancellationToken>()).Returns([]);

        var response = await Client(TestTokens.Player()).GetAsync("/api/artists");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await _service.Received(1).SearchAsync(null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Search_NoToken_Returns401()
    {
        var response = await Client(token: null).GetAsync("/api/artists");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Create_AsAdmin_Returns201WithLocation()
    {
        _service.CreateAsync(new ArtistNameRequest("Queen"), Arg.Any<CancellationToken>()).Returns(new ArtistDto(4, "Queen"));

        var response = await Client(TestTokens.Admin()).PostAsJsonAsync("/api/artists", new ArtistNameRequest("Queen"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("/api/artists/4", response.Headers.Location?.AbsolutePath);
        Assert.Equal(new ArtistDto(4, "Queen"), await response.Content.ReadFromJsonAsync<ArtistDto>());
    }

    [Fact]
    public async Task Create_AsPlayer_Returns403()
    {
        var response = await Client(TestTokens.Player()).PostAsJsonAsync("/api/artists", new ArtistNameRequest("Queen"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Create_NoToken_Returns401()
    {
        var response = await Client(token: null).PostAsJsonAsync("/api/artists", new ArtistNameRequest("Queen"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Create_InvalidName_Returns400()
    {
        _service.CreateAsync(Arg.Any<ArtistNameRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new MusicValidationException("name must be between 1 and 200 characters."));

        var response = await Client(TestTokens.Admin()).PostAsJsonAsync("/api/artists", new ArtistNameRequest(""));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_Duplicate_Returns409()
    {
        _service.CreateAsync(Arg.Any<ArtistNameRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new MusicConflictException("taken"));

        var response = await Client(TestTokens.Admin()).PostAsJsonAsync("/api/artists", new ArtistNameRequest("Queen"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task GetById_Unknown_Returns404()
    {
        _service.GetAsync(9, Arg.Any<CancellationToken>()).ThrowsAsync(new ArtistNotFoundException(9));

        var response = await Client(TestTokens.Player()).GetAsync("/api/artists/9");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
