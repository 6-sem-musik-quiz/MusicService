using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using MusicService.Domain.Dtos;
using MusicService.Domain.Exceptions;
using MusicService.Service.Interfaces;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace MusicService.Api.Tests;

// HTTP contract of POST /api/playlists/{id}/songs (SCRUM-93): multipart binding, auth and status codes.
// The service is faked; its rules are in SongServiceUploadTests.
public class PlaylistSongsUploadTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly SongDto Song = new(99, "Under Pressure", ["David Bowie", "Queen"], 1981, 248,
        "https://media.munkcloud.dk/songs/3f2c8a4e-1b7d-4c55-9a0e-6d2b1f8e7c10.mp3");

    private readonly ISongService _service = Substitute.For<ISongService>();

    private HttpClient Client(string? token) => TestApp.Client(factory, token, s => s.AddScoped(_ => _service));

    private static MultipartFormDataContent Form(bool withFile = true, string contentType = "audio/mpeg")
    {
        var form = new MultipartFormDataContent
        {
            { new StringContent("Under Pressure"), "title" },
            { new StringContent("10"), "artistIds" },
            { new StringContent("11"), "artistIds" },
            { new StringContent("1981"), "year" }
        };
        if (withFile)
        {
            var file = new ByteArrayContent([1, 2, 3, 4]);
            file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            form.Add(file, "file", "under-pressure.mp3");
        }
        return form;
    }

    [Fact]
    public async Task AsAdmin_Returns201WithSong()
    {
        _service.UploadAsync(1, Arg.Any<UploadSongRequest>(), Arg.Any<Stream>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Song);

        var response = await Client(TestTokens.Admin()).PostAsync("/api/playlists/1/songs", Form());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(Song.Id, (await response.Content.ReadFromJsonAsync<SongDto>())?.Id);
    }

    [Fact]
    public async Task BindsAllFormFields()
    {
        UploadSongRequest? request = null;
        string? contentType = null;
        byte[]? bytes = null;
        _service.UploadAsync(1, Arg.Do<UploadSongRequest>(r => request = r), Arg.Do<Stream>(s =>
            {
                using var copy = new MemoryStream();
                s.CopyTo(copy);
                bytes = copy.ToArray();
            }), Arg.Do<string?>(c => contentType = c), Arg.Any<CancellationToken>())
            .Returns(Song);

        await Client(TestTokens.Admin()).PostAsync("/api/playlists/1/songs", Form());

        Assert.Equal("Under Pressure", request?.Title);
        Assert.Equal([10, 11], request?.ArtistIds);
        Assert.Equal(1981, request?.Year);
        Assert.Equal("audio/mpeg", contentType);
        Assert.Equal([1, 2, 3, 4], bytes);
    }

    [Fact]
    public async Task MissingFile_Returns400()
    {
        var response = await Client(TestTokens.Admin()).PostAsync("/api/playlists/1/songs", Form(withFile: false));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await _service.DidNotReceiveWithAnyArgs().UploadAsync(default, default!, default!, default);
    }

    [Fact]
    public async Task ServiceValidationError_Returns400()
    {
        _service.UploadAsync(1, Arg.Any<UploadSongRequest>(), Arg.Any<Stream>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new MusicValidationException("file must be an mp3 (audio/mpeg)."));

        var response = await Client(TestTokens.Admin()).PostAsync("/api/playlists/1/songs", Form(contentType: "audio/wav"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UnknownPlaylist_Returns404()
    {
        _service.UploadAsync(1, Arg.Any<UploadSongRequest>(), Arg.Any<Stream>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new PlaylistNotFoundException(1));

        var response = await Client(TestTokens.Admin()).PostAsync("/api/playlists/1/songs", Form());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AsPlayer_Returns403()
    {
        var response = await Client(TestTokens.Player()).PostAsync("/api/playlists/1/songs", Form());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await _service.DidNotReceiveWithAnyArgs().UploadAsync(default, default!, default!, default);
    }

    [Fact]
    public async Task NoToken_Returns401()
    {
        var response = await Client(token: null).PostAsync("/api/playlists/1/songs", Form());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
