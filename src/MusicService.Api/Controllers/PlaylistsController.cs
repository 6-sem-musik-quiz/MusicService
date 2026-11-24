using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MusicService.Api.Auth;
using MusicService.Domain.Dtos;
using MusicService.Service.Interfaces;

namespace MusicService.Api.Controllers;

// Playlists (categories). Everyone logged in can read them (players pick one when creating a lobby);
// only the admin can create, rename and delete them.
[ApiController]
[Route("api/playlists")]
public class PlaylistsController(IPlaylistService playlists) : ControllerBase
{
    // Sorted by name.
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<PlaylistDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<PlaylistDto>>> GetAll(CancellationToken ct) =>
        Ok(await playlists.GetAllAsync(ct));

    [HttpGet("{id:int}")]
    [ProducesResponseType<PlaylistDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PlaylistDto>> GetById(int id, CancellationToken ct) =>
        Ok(await playlists.GetAsync(id, ct));

    [HttpPost]
    [Authorize(Policy = AuthPolicies.AdminOnly)]
    [ProducesResponseType<PlaylistDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PlaylistDto>> Create(PlaylistNameRequest request, CancellationToken ct)
    {
        var playlist = await playlists.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = playlist.Id }, playlist);
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = AuthPolicies.AdminOnly)]
    [ProducesResponseType<PlaylistDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PlaylistDto>> Rename(int id, PlaylistNameRequest request, CancellationToken ct) =>
        Ok(await playlists.RenameAsync(id, request, ct));

    // Also deletes the songs that are in no other playlist, and their files.
    [HttpDelete("{id:int}")]
    [Authorize(Policy = AuthPolicies.AdminOnly)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await playlists.DeleteAsync(id, ct);
        return NoContent();
    }
}
