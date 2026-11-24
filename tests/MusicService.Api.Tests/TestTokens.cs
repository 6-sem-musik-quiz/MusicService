using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace MusicService.Api.Tests;

// Tokens shaped like the ones UserService issues (sub, unique_name, role).
public static class TestTokens
{
    public const string SigningKey = "test-signing-key-that-is-at-least-32-bytes-long";
    public const string Issuer = "slopster-userservice";
    public const string Audience = "slopster";

    public static string Admin() => Create(role: "Admin");

    public static string Player() => Create(role: "Player");

    public static string Create(string role, string key = SigningKey, string issuer = Issuer, DateTime? expires = null)
    {
        var expiresAt = expires ?? DateTime.UtcNow.AddMinutes(30);
        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = Audience,
            Subject = new ClaimsIdentity([
                new Claim("sub", "1"),
                new Claim("unique_name", "alice"),
                new Claim("role", role)
            ]),
            NotBefore = expiresAt.AddHours(-1),
            IssuedAt = expiresAt.AddHours(-1),
            Expires = expiresAt,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256)
        });
    }
}
