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

### Adding a new feature

1. Entity in `Domain/Entities`, request/response DTOs in `Domain/DTOs`
2. EF configuration in `Repository/Data/Configurations`, `DbSet` in `AppDbContext`
3. `I<Name>Repository` + implementation, register in `RepositoryLayerExtensions`
4. `I<Name>Service` + implementation + mappings, register in `ServiceLayerExtensions`
5. Controller in `Api/Controllers`
6. Tests

## Running locally

Locally the API always runs against the **real** Postgres in the cluster
(`quiz-postgres.tail5a1bc5.ts.net`, Tailscale on). **This is live data.**
Migrations are turned off locally (`Database:MigrateOnStartup=false`), so a branch can never
change the shared schema; schema changes reach the database only through a deploy from `main`.

The password is a secret, so it is not in Git. Get it with
`bao kv get -field=musicservice kv/quiz/postgres` (or ask Asger).

**Docker:** keep the password in a local `.env` file (in `.gitignore`). Compose stops with a
clear message if it is missing.

```powershell
Copy-Item .env.example .env      # once, then fill in DB_PASSWORD
docker compose up --build
```

**From your IDE:** `appsettings.Development.json` has the host and user but no password;
Npgsql reads it from `PGPASSWORD`.

```powershell
$env:PGPASSWORD = "<password>"
dotnet run --project src/MusicService.Api
```

Without Tailscale: `kubectl -n quiz port-forward svc/postgresql 15432:5432`, then set
`DB_HOST=host.docker.internal` and `DB_PORT=15432` in `.env` (see `.env.example`).

- API: http://localhost:5002
- Swagger: http://localhost:5002/swagger
- Health: http://localhost:5002/health
- Ready (also checks Postgres and blob storage): http://localhost:5002/health/ready

## Authentication

Every endpoint except `/health` and `/health/ready` needs a JWT from UserService
(`Authorization: Bearer <token>`). MusicService validates it the same way as the other services:
HS256 with `Jwt:SigningKey`, issuer `slopster-userservice`, audience `slopster`. The key is the one
UserService signs with, OpenBao `kv/quiz/jwt` (property `signing-key`); in Kubernetes it comes in as
`Jwt__SigningKey`. Without a key every protected endpoint returns 401.

Admin endpoints add `[Authorize(Policy = AuthPolicies.AdminOnly)]`, which needs the token claim
`role = Admin`. A logged-in player gets 403 there.

Locally, put the key in `.env` as `JWT_SIGNING_KEY` (Docker), or set `$env:Jwt__SigningKey` (IDE).
Get it with `bao kv get -field=signing-key kv/quiz/jwt`.

## Endpoints

Through the edge they are `/api/musicservice/...`; the edge strips that prefix.

| Endpoint | Who | Result |
|---|---|---|
| `GET /api/playlists` | logged in | 200 `[{ id, name, songCount }]`, sorted by name |
| `GET /api/playlists/{id}` | logged in | 200 `{ id, name, songCount }`; 404 |
| `POST /api/playlists` `{ name }` | admin | 201 + `Location`; 400 (name not 1-60 chars); 409 (name taken, ignoring case) |
| `PUT /api/playlists/{id}` `{ name }` | admin | 200 `{ id, name, songCount }`; 400; 404; 409 |
| `DELETE /api/playlists/{id}` | admin | 204; 404. Songs in no other playlist are deleted too, with their files in blob storage; shared songs are kept |

Errors are ProblemDetails. LobbyService reads `GET /api/playlists/{id}` when a lobby is created.

## Blob storage (song files)

The song files live in Garage, an S3-compatible object store in the cluster, with
Cloudflare CDN in front (`https://media.munkcloud.dk/<key>`). See the gitops repo,
README "BlobStorage". MusicService talks to it with `AWSSDK.S3`, registered by
`AddBlobStorage` in `Repository/Extensions/BlobStorageExtensions.cs` as `IAmazonS3`.

Config section `BlobStorage` (in Kubernetes as `BlobStorage__<Name>` env vars):

| Key | In the cluster |
|---|---|
| `ServiceUrl` | `http://garage.quiz.svc.cluster.local:3900` |
| `Bucket` | `media` (everything in it is public through the CDN) |
| `Region` | `garage` (default) |
| `AccessKeyId`, `SecretAccessKey` | OpenBao `kv/quiz/blobstorage` |
| `PublicBaseUrl` | `https://media.munkcloud.dk` (the CDN) |

A song file is stored as `songs/{BlobKey}.mp3` (`BlobStorageOptions.SongObjectKey`), and the browser
fetches it from `PublicBaseUrl` + `/` + that key (`PublicSongUrl`). `BlobKey` is a Guid, so the URL
does not give away the answer.

`/health/ready` lists one object in the bucket to prove the connection and key work.
Without the config the app still starts; only that check is unhealthy.

Locally, forward the S3 port and set the env vars (the key from OpenBao or the cluster Secret):

```powershell
kubectl -n quiz port-forward svc/garage 13900:3900
$env:BlobStorage__ServiceUrl = "http://localhost:13900"
$env:BlobStorage__AccessKeyId = "<access-key-id>"
$env:BlobStorage__SecretAccessKey = "<secret-access-key>"
```

## Database migrations

The API applies pending migrations when it starts, so a deploy from `main` updates the schema by itself. EF skips migrations already listed in `__EFMigrationsHistory`, and locks the database while migrating, so several pods starting at once is fine. Turn it off with `Database__MigrateOnStartup=false`.

To add a migration:

```bash
dotnet tool install --global dotnet-ef
dotnet ef migrations add <Name> --project src/MusicService.Repository --startup-project src/MusicService.Api
```

Do **not** run `dotnet ef database update` locally: your connection string points at the real
database, so it would apply your branch's migrations to the shared schema. Merge to `main`
and the deploy applies them.

## Tests

```bash
dotnet test
```

## Service ports

| Service | API |
|---|---|
| UserService | 5001 |
| MusicService | 5002 |
| StatisticService | 5003 |
| LobbyService | 5004 |
| GameService | 5005 |
