using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MusicService.Api.Auth;
using MusicService.Domain.Dtos;
using MusicService.Service.Interfaces;

namespace MusicService.Api.Controllers;

// Artists for the upload form: search for an existing one, or create a new one (admin).
[ApiController]
[Route("api/artists")]
public class ArtistsController(IArtistService artists) : ControllerBase
{
    // Name contains `search` (ignoring case), sorted by name, at most 50.
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ArtistDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<ArtistDto>>> Search([FromQuery] string? search, CancellationToken ct) =>
        Ok(await artists.SearchAsync(search, ct));

    // Target of the Location header from POST.
    [HttpGet("{id:int}")]
    [ProducesResponseType<ArtistDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ArtistDto>> GetById(int id, CancellationToken ct) =>
        Ok(await artists.GetAsync(id, ct));

    [HttpPost]
    [Authorize(Policy = AuthPolicies.AdminOnly)]
    [ProducesResponseType<ArtistDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ArtistDto>> Create(ArtistNameRequest request, CancellationToken ct)
    {
        var artist = await artists.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = artist.Id }, artist);
    }
}
