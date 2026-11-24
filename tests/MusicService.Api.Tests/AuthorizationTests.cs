using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using MusicService.Api.Auth;

namespace MusicService.Api.Tests;

// JWT validation and the AdminOnly policy, checked through two test-only endpoints
// (AuthProbeController below), independent of any real controller.
public class AuthorizationTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private HttpClient CreateClient() => factory
        .WithWebHostBuilder(b =>
        {
            b.UseSetting("Database:MigrateOnStartup", "false");
            b.UseSetting("Jwt:SigningKey", TestTokens.SigningKey);
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
        var response = await Get("/test/auth/admin", TestTokens.Create(role: "Player"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Player_OnLoggedInEndpoint_Returns200()
    {
        var response = await Get("/test/auth/any", TestTokens.Create(role: "Player"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("alice", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Admin_OnAdminEndpoint_Returns200()
    {
        var response = await Get("/test/auth/admin", TestTokens.Create(role: "Admin"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task TokenSignedWithOtherKey_Returns401()
    {
        var response = await Get("/test/auth/any", TestTokens.Create(role: "Admin", key: "another-key-that-is-also-at-least-32-bytes"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TokenFromOtherIssuer_Returns401()
    {
        var response = await Get("/test/auth/any", TestTokens.Create(role: "Admin", issuer: "someone-else"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ExpiredToken_Returns401()
    {
        var response = await Get("/test/auth/any", TestTokens.Create(role: "Admin", expires: DateTime.UtcNow.AddMinutes(-10)));

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
