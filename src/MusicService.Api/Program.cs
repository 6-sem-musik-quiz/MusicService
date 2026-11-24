using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using MusicService.Api.Auth;
using MusicService.Api.HealthChecks;
using MusicService.Api.Middleware;
using MusicService.Repository.BlobStorage;
using MusicService.Repository.Data;
using MusicService.Repository.Extensions;
using MusicService.Service.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
// /health is liveness (the process is up); /health/ready also checks
// that the database and the blob storage answer.
builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>("postgres", tags: ["ready"])
    .AddCheck<BlobStorageHealthCheck>("blobstorage", tags: ["ready"]);

builder.Services.AddJwtAuthentication();

builder.Services.AddRepositoryLayer(
    builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("Connection string 'Default' is not configured."));
builder.Services.AddBlobStorage(
    builder.Configuration.GetSection(BlobStorageOptions.SectionName).Get<BlobStorageOptions>() ?? new BlobStorageOptions());
builder.Services.AddServiceLayer();

var app = builder.Build();

// Bring the database schema up to date before taking traffic. Tests turn this off
// with Database:MigrateOnStartup=false when they have no Postgres.
if (app.Configuration.GetValue("Database:MigrateOnStartup", true))
    app.Services.MigrateDatabase();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health", new HealthCheckOptions { Predicate = _ => false }).AllowAnonymous();
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = c => c.Tags.Contains("ready") }).AllowAnonymous();

app.Run();

// Exposed so integration tests can use WebApplicationFactory<Program>.
public partial class Program;
