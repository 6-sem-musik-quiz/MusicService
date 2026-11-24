namespace MusicService.Domain.Exceptions;

// Invalid input. Mapped to 400 by the API.
public class MusicValidationException(string message) : Exception(message);

// Conflicts with what is stored, e.g. a name that is already taken. Mapped to 409.
public class MusicConflictException(string message) : Exception(message);

// Something that does not exist. Mapped to 404.
public class MusicNotFoundException(string message) : Exception(message);

public class PlaylistNotFoundException(int playlistId) : MusicNotFoundException($"Playlist {playlistId} was not found.")
{
    public int PlaylistId { get; } = playlistId;
}

public class ArtistNotFoundException(int artistId) : MusicNotFoundException($"Artist {artistId} was not found.")
{
    public int ArtistId { get; } = artistId;
}
