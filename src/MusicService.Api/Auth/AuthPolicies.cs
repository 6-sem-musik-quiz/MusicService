namespace MusicService.Api.Auth;

public static class AuthPolicies
{
    // Only the admin account (token claim role = Admin) may change playlists, songs and artists.
    // Use as [Authorize(Policy = AuthPolicies.AdminOnly)].
    public const string AdminOnly = "AdminOnly";
}
