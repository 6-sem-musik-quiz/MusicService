using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MusicService.Api.Auth;
using MusicService.Domain.Dtos;
using MusicService.Service.Interfaces;

namespace MusicService.Api.Controllers;

// A song itself, independent of the playlists it is in.
[ApiController]
[Route("api/songs")]
[Authorize(Policy = AuthPolicies.AdminOnly)]
public class SongsController(ISongService songs) : ControllerBase
{
    // Changes title, artists (ids) and year. The change applies in every playlist the song is in.
    [HttpPut("{id:int}")]
    [ProducesResponseType<SongDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SongDto>> Update(int id, UpdateSongRequest request, CancellationToken ct) =>
        Ok(await songs.UpdateAsync(id, request, ct));
}
