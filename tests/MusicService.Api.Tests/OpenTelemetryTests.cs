using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using MusicService.Service.Metrics;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace MusicService.Api.Tests;

// SCRUM-173: the app starts with OpenTelemetry traces and metrics, and the music meter.
public class OpenTelemetryTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public void App_StartsWithTracesAndMetrics()
    {
        using var app = factory.WithWebHostBuilder(b =>
        {
            b.UseSetting("Database:MigrateOnStartup", "false");
            b.UseSetting("Jwt:SigningKey", TestTokens.SigningKey);
        });
        using var client = app.CreateClient(); // starts Program, including the OpenTelemetry setup

        Assert.NotNull(app.Services.GetService<TracerProvider>());
        Assert.NotNull(app.Services.GetService<MeterProvider>());
        Assert.NotNull(app.Services.GetService<MusicMetrics>());
    }
}
