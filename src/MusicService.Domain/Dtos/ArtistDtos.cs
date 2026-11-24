namespace MusicService.Domain.Dtos;

public record ArtistDto(int Id, string Name);

// Body of POST /api/artists.
public record ArtistNameRequest(string Name);
