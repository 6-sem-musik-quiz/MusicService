using System.Net.Http.Headers;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace MusicService.Api.Tests;

public static class TestApp
{
    // A client for the API without Postgres, with the test signing key, optional fakes and a bearer token.
    public static HttpClient Client(WebApplicationFactory<Program> factory, string? token,
        Action<IServiceCollection>? services = null)
    {
        var client = factory
            .WithWebHostBuilder(b =>
            {
                b.UseSetting("Database:MigrateOnStartup", "false");
                b.UseSetting("Jwt:SigningKey", TestTokens.SigningKey);
                if (services is not null)
                    b.ConfigureTestServices(services);
            })
            .CreateClient();
        if (token is not null)
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
