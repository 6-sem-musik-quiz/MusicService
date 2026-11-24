using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MusicService.Api.Auth;
using MusicService.Domain.Dtos;
using MusicService.Service.Interfaces;

namespace MusicService.Api.Controllers;

// The songs in one playlist.
[ApiController]
[Route("api/playlists/{playlistId:int}/songs")]
public class PlaylistSongsController(ISongService songs) : ControllerBase
{
    // `count` different songs in random order, for GameService to start a game.
    // Any valid token (a player's or a service token); no admin role needed.
    [HttpGet("random")]
    [ProducesResponseType<IReadOnlyList<SongDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<SongDto>>> GetRandom(int playlistId, [FromQuery] int count, CancellationToken ct) =>
        Ok(await songs.GetRandomAsync(playlistId, count, ct));

    // multipart/form-data: file (mp3), title, artistIds (repeated field), year.
    // No size limit: a whole album track can be large, and only the admin can call this.
    [HttpPost]
    [Authorize(Policy = AuthPolicies.AdminOnly)]
    [Consumes("multipart/form-data")]
    [DisableRequestSizeLimit]
    [RequestFormLimits(MultipartBodyLengthLimit = long.MaxValue)]
    [ProducesResponseType<SongDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SongDto>> Upload(int playlistId, [FromForm] UploadSongForm form, CancellationToken ct)
    {
        await using var stream = form.File!.OpenReadStream();
        var song = await songs.UploadAsync(playlistId,
            new UploadSongRequest(form.Title ?? string.Empty, form.ArtistIds, form.Year),
            stream, form.File.ContentType, ct);

        return StatusCode(StatusCodes.Status201Created, song);
    }
}

public class UploadSongForm
{
    [System.ComponentModel.DataAnnotations.Required]
    public IFormFile? File { get; set; }

    public string? Title { get; set; }

    public List<int> ArtistIds { get; set; } = [];

    public int Year { get; set; }
}
