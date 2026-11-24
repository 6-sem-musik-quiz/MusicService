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
| `GET /api/artists?search={text}` | logged in | 200 `[{ id, name }]`, name contains `text` ignoring case, sorted by name, at most 50 |
| `GET /api/artists/{id}` | logged in | 200 `{ id, name }`; 404 |
| `POST /api/artists` `{ name }` | admin | 201 + `Location`; 400 (name not 1-200 chars); 409 (name taken, ignoring case) |
| `POST /api/playlists/{id}/songs` (multipart) | admin | 201 `SongDto`; 400; 404 (playlist). See "Uploading a song" |
| `GET /api/playlists/{id}/songs` | admin | 200 `[SongDto]` sorted by title; 404 |
| `PUT /api/songs/{id}` `{ title, artistIds, year }` | admin | 200 `SongDto`; 400 (same rules as upload); 404. The song is the same in every playlist it is in |
| `DELETE /api/playlists/{id}/songs/{songId}` | admin | 204; 404 if the playlist or song does not exist or the song is not in the playlist. A song in no other playlist is deleted, with its file |
| `GET /api/playlists/{id}/songs/random?count={n}` | logged in (GameService) | 200 `[SongDto]`: `n` different songs in random order, 1 ≤ n ≤ 100; 400 if `n` is invalid or the playlist has fewer songs (the message says how many); 404 |

Errors are ProblemDetails. LobbyService reads `GET /api/playlists/{id}` when a lobby is created.

`SongDto` is `{ id, title, artists: string[], year, durationSeconds, audioUrl }`; `audioUrl` is the
public CDN address of the whole mp3, and `artists` are sorted by name.

When a song is deleted (removed from its last playlist, or its playlist is deleted) or an artist is
taken off a song, artists that no longer have any songs are deleted. An artist that was created but
never used is kept.

### Uploading a song

`POST /api/playlists/{id}/songs` takes `multipart/form-data`:

| Field | Rule |
|---|---|
| `file` | mp3, content type `audio/mpeg` (or `audio/mp3`). The length is read from the file with TagLibSharp; a file that is not an mp3, or whose length cannot be read, gives 400 |
| `title` | 1-200 characters |
| `artistIds` | one or more ids of existing artists (repeat the field). Create artists first with `POST /api/artists`; an unknown id gives 400 and nothing is saved |
| `year` | 1900 to the current year |

The file is uploaded to `songs/{BlobKey}.mp3` before the song is saved. If saving fails, the file is
deleted again. MusicService itself has no size limit on this endpoint, but the edge (Kestrel default,
about 28.6 MiB) and Cloudflare (100 MB on the free plan) still have theirs.

```powershell
curl.exe -X POST http://localhost:5002/api/playlists/1/songs `
  -H "Authorization: Bearer $token" `
  -F "file=@song.mp3;type=audio/mpeg" -F "title=Under Pressure" `
  -F "artistIds=1" -F "artistIds=2" -F "year=1981"
```

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

### CORS on the bucket

The frontend fetches the mp3 files from the CDN into memory before a round (SCRUM-112), so the bucket
must allow `GET`/`HEAD` from the frontend's origin. It is set once on the bucket (not by the app):

```powershell
kubectl -n quiz port-forward svc/garage 13900:3900
@'
{ "CORSRules": [ { "AllowedOrigins": ["https://quiz.munkcloud.dk"],
                   "AllowedMethods": ["GET", "HEAD"],
                   "AllowedHeaders": ["*"],
                   "MaxAgeSeconds": 3600 } ] }
'@ | Set-Content cors.json
$env:AWS_ACCESS_KEY_ID = "<access-key-id>"; $env:AWS_SECRET_ACCESS_KEY = "<secret-access-key>"
aws s3api put-bucket-cors --endpoint-url http://localhost:13900 --region garage --bucket media --cors-configuration file://cors.json
aws s3api get-bucket-cors --endpoint-url http://localhost:13900 --region garage --bucket media
```

Check it through the CDN (`Access-Control-Allow-Origin` must be in the response):

```powershell
curl.exe -I -H "Origin: https://quiz.munkcloud.dk" https://media.munkcloud.dk/songs/<BlobKey>.mp3
```

Cloudflare caches the first response for a file. If that request had no `Origin` header, the cached
copy has no CORS header either; purge the file in Cloudflare if the check fails.

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
