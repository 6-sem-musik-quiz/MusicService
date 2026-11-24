namespace MusicService.Domain.Dtos;

// A playlist (the Category entity) as the API returns it. LobbyService reads the same shape.
public record PlaylistDto(int Id, string Name, int SongCount);

// Body of POST /api/playlists and PUT /api/playlists/{id}.
public record PlaylistNameRequest(string Name);
