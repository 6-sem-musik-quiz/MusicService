# MusicService

Part of the **6-sem-musik-quiz** system. ASP.NET Core (.NET 10) Web API using MVC controllers and a layered architecture.

## Architecture

```
Api  ──►  Service  ──►  Repository
  └─────────┴──────────────┴──►  Domain
```

| Project | Responsibility |
|---|---|
| `src/MusicService.Api` | Controllers, middleware, `Program.cs`. Thin: validate input, call a service, return HTTP result. |
| `src/MusicService.Service` | Business logic. Talks to repositories through interfaces, returns DTOs. |
| `src/MusicService.Repository` | Data access with EF Core (PostgreSQL). The only layer that knows about the database. |
| `src/MusicService.Domain` | Entities and DTOs. No dependencies. |
| `tests/MusicService.Service.Tests` | Unit tests for services (xUnit + NSubstitute). |
| `tests/MusicService.Api.Tests` | Integration tests using `WebApplicationFactory`. |

`Example*` files are placeholders showing the pattern end to end. Replace them with real entities.

### Adding a new feature

1. Entity in `Domain/Entities`, request/response DTOs in `Domain/DTOs`
2. EF configuration in `Repository/Data/Configurations`, `DbSet` in `AppDbContext`
3. `I<Name>Repository` + implementation, register in `RepositoryLayerExtensions`
4. `I<Name>Service` + implementation + mappings, register in `ServiceLayerExtensions`
5. Controller in `Api/Controllers`
6. Tests

## Running locally

**Everything in Docker:**

```bash
docker compose up --build
```

**API from your IDE, database in Docker:**

```bash
docker compose up db
dotnet run --project src/MusicService.Api
```

- API: http://localhost:5002
- Swagger: http://localhost:5002/swagger
- Health: http://localhost:5002/health
- Postgres: `localhost:5442` (postgres / postgres)

## Database migrations

```bash
dotnet tool install --global dotnet-ef
dotnet ef migrations add <Name> --project src/MusicService.Repository --startup-project src/MusicService.Api
dotnet ef database update --project src/MusicService.Repository --startup-project src/MusicService.Api
```

## Tests

```bash
dotnet test
```

## Service ports

| Service | API | Postgres |
|---|---|---|
| UserService | 5001 | 5441 |
| MusicService | 5002 | 5442 |
| StatisticService | 5003 | 5443 |
| LobbyService | 5004 | 5444 |
| GameService | 5005 | 5445 |
