using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using MusicService.Api.Auth;

namespace MusicService.Api.Tests;

// JWT validation and the AdminOnly policy, checked through two test-only endpoints
// (AuthProbeController below) until the first real admin endpoint exists.
public class AuthorizationTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private const string SigningKey = "test-signing-key-that-is-at-least-32-bytes-long";
    private const string Issuer = "slopster-userservice";
    private const string Audience = "slopster";

    private HttpClient CreateClient() => factory
        .WithWebHostBuilder(b =>
        {
            b.UseSetting("Database:MigrateOnStartup", "false");
            b.UseSetting("Jwt:SigningKey", SigningKey);
            b.ConfigureServices(s => s.AddControllers().AddApplicationPart(typeof(AuthProbeController).Assembly));
        })
        .CreateClient();

    [Theory]
    [InlineData("/test/auth/any")]
    [InlineData("/test/auth/admin")]
    public async Task NoToken_Returns401(string path)
    {
        var response = await CreateClient().GetAsync(path);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Player_OnAdminEndpoint_Returns403()
    {
        var response = await Get("/test/auth/admin", Token(role: "Player"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Player_OnLoggedInEndpoint_Returns200()
    {
        var response = await Get("/test/auth/any", Token(role: "Player"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("alice", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Admin_OnAdminEndpoint_Returns200()
    {
        var response = await Get("/test/auth/admin", Token(role: "Admin"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task TokenSignedWithOtherKey_Returns401()
    {
        var response = await Get("/test/auth/any", Token(role: "Admin", key: "another-key-that-is-also-at-least-32-bytes"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TokenFromOtherIssuer_Returns401()
    {
        var response = await Get("/test/auth/any", Token(role: "Admin", issuer: "someone-else"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ExpiredToken_Returns401()
    {
        var response = await Get("/test/auth/any", Token(role: "Admin", expires: DateTime.UtcNow.AddMinutes(-10)));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Health_NeedsNoToken()
    {
        var response = await CreateClient().GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task<HttpResponseMessage> Get(string path, string token)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await client.GetAsync(path);
    }

    // A token shaped like the ones UserService issues (sub, unique_name, role).
    private static string Token(string role, string key = SigningKey, string issuer = Issuer, DateTime? expires = null)
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

// Test-only endpoints: one for any logged-in user (fallback policy), one for admins.
[ApiController]
[Route("test/auth")]
public class AuthProbeController : ControllerBase
{
    [HttpGet("any")]
    public string Any() => User.Identity?.Name ?? "";

    [HttpGet("admin")]
    [Authorize(Policy = AuthPolicies.AdminOnly)]
    public IActionResult Admin() => Ok();
}
