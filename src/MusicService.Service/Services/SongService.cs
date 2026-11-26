using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;
using MusicService.Domain.Dtos;
using MusicService.Domain.Entities;
using MusicService.Domain.Exceptions;
using MusicService.Repository.BlobStorage;
using MusicService.Repository.Interfaces;
using MusicService.Service.Interfaces;

using MusicService.Service.Metrics;

namespace MusicService.Service.Services;

// Song rules: title 1-200 characters, year 1900 to this year, one or more existing artists,
// and the file must be an mp3 whose length can be read. The file goes to blob storage first;
// if the database save then fails, the file is deleted again.
public class SongService(
    ISongRepository songs,
    IPlaylistRepository playlists,
    IArtistRepository artists,
    IAudioInspector audioInspector,
    IAmazonS3 s3,
    BlobStorageOptions blobOptions,
    TimeProvider timeProvider,
    Random random,
    ILogger<SongService> logger,
    MusicMetrics? metrics = null) : ISongService // SCRUM-173; null in tests that do not look
{
    public const int MaxTitleLength = 200;
    public const int MinYear = 1900;
    public const string Mp3ContentType = "audio/mpeg";
    public const int MaxRandomCount = 100;

    public async Task<SongDto> UploadAsync(int playlistId, UploadSongRequest request, Stream file, string? contentType,
        CancellationToken ct = default)
    {
        var title = ValidTitle(request.Title);
        ValidateYear(request.Year);
        var artistIds = NonEmptyArtistIds(request.ArtistIds);
        if (!IsMp3ContentType(contentType))
            throw new MusicValidationException("file must be an mp3 (audio/mpeg).");

        if (await playlists.GetAsync(playlistId, ct) is null)
            throw new PlaylistNotFoundException(playlistId);

        await EnsureArtistsExistAsync(artistIds, ct);

        if (!file.CanSeek)
            throw new InvalidOperationException("The upload stream must be seekable.");
        var duration = audioInspector.GetMp3DurationSeconds(file)
                       ?? throw new MusicValidationException("file is not an mp3, or its length cannot be read.");
        file.Position = 0;

        var song = new Song
        {
            Title = title,
            Year = request.Year,
            DurationSeconds = duration,
            BlobKey = Guid.NewGuid()
        };
        var key = BlobStorageOptions.SongObjectKey(song.BlobKey);

        try
        {
            await s3.PutObjectAsync(new PutObjectRequest
            {
                BucketName = blobOptions.Bucket,
                Key = key,
                InputStream = file,
                AutoCloseStream = false,
                ContentType = Mp3ContentType,
                // The key is a new Guid for every file, so the CDN and browsers may cache it forever.
                Headers = { CacheControl = "public, max-age=31536000, immutable" }
            }, ct);
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            metrics?.BlobError("upload");
            throw;
        }

        try
        {
            var saved = await songs.AddAsync(song, playlistId, artistIds, ct);
            return saved.ToDto(blobOptions);
        }
        catch
        {
            await DeleteObjectQuietlyAsync(key);
            throw;
        }
    }

    public async Task<IReadOnlyList<SongDto>> GetRandomAsync(int playlistId, int count, CancellationToken ct = default)
    {
        if (count is < 1 or > MaxRandomCount)
            throw new MusicValidationException($"count must be between 1 and {MaxRandomCount}.");
        if (await playlists.GetAsync(playlistId, ct) is null)
            throw new PlaylistNotFoundException(playlistId);

        var ids = (await songs.GetIdsInPlaylistAsync(playlistId, ct)).ToArray();
        if (ids.Length < count)
            throw new MusicValidationException(
                $"The playlist has {ids.Length} songs, but {count} were requested.");

        // Shuffle all ids and take the first `count`: distinct songs, every order equally likely.
        random.Shuffle(ids);
        var picked = ids[..count];
        var byId = (await songs.GetWithArtistsAsync(picked, ct)).ToDictionary(s => s.Id);
        metrics?.SongDraw(playlistId);

        return picked.Select(id => byId[id].ToDto(blobOptions)).ToList();
    }

    public async Task<IReadOnlyList<SongDto>> GetInPlaylistAsync(int playlistId, CancellationToken ct = default)
    {
        if (await playlists.GetAsync(playlistId, ct) is null)
            throw new PlaylistNotFoundException(playlistId);

        return (await songs.GetInPlaylistAsync(playlistId, ct)).Select(s => s.ToDto(blobOptions)).ToList();
    }

    public async Task<SongDto> UpdateAsync(int songId, UpdateSongRequest request, CancellationToken ct = default)
    {
        var title = ValidTitle(request.Title);
        ValidateYear(request.Year);
        var artistIds = NonEmptyArtistIds(request.ArtistIds);
        await EnsureArtistsExistAsync(artistIds, ct);

        var song = await songs.UpdateAsync(songId, title, request.Year, artistIds, ct)
                   ?? throw new SongNotFoundException(songId);
        return song.ToDto(blobOptions);
    }

    public async Task RemoveFromPlaylistAsync(int playlistId, int songId, CancellationToken ct = default)
    {
        var result = await songs.RemoveFromPlaylistAsync(playlistId, songId, ct);
        if (!result.Found)
            throw new MusicNotFoundException($"Song {songId} is not in playlist {playlistId}.");

        // The song was in no other playlist, so it is gone; its file is too.
        if (result.DeletedBlobKey is { } blobKey)
            await DeleteObjectQuietlyAsync(BlobStorageOptions.SongObjectKey(blobKey));
    }

    private static List<int> NonEmptyArtistIds(IReadOnlyList<int>? artistIds)
    {
        var ids = artistIds?.Distinct().ToList() ?? [];
        if (ids.Count == 0)
            throw new MusicValidationException("artistIds must contain at least one artist.");
        return ids;
    }

    private async Task EnsureArtistsExistAsync(IReadOnlyCollection<int> artistIds, CancellationToken ct)
    {
        var existing = await artists.GetExistingIdsAsync(artistIds, ct);
        var unknown = artistIds.Where(id => !existing.Contains(id)).ToList();
        if (unknown.Count > 0)
            throw new MusicValidationException($"Unknown artistIds: {string.Join(", ", unknown)}.");
    }

    private static bool IsMp3ContentType(string? contentType) =>
        contentType?.Split(';')[0].Trim().ToLowerInvariant() is Mp3ContentType or "audio/mp3";

    private static string ValidTitle(string? title)
    {
        var trimmed = title?.Trim() ?? string.Empty;
        if (trimmed.Length is 0 or > MaxTitleLength)
            throw new MusicValidationException($"title must be between 1 and {MaxTitleLength} characters.");
        return trimmed;
    }

    private void ValidateYear(int year)
    {
        var thisYear = timeProvider.GetUtcNow().Year;
        if (year < MinYear || year > thisYear)
            throw new MusicValidationException($"year must be between {MinYear} and {thisYear}.");
    }

    // Deletes a file whose song is no longer in the database (a failed save, or a deleted song).
    // A failure is only logged: nothing points to the file, so it is unreachable, not broken.
    // Not cancelled with the request, so a client that gives up does not leave the file behind.
    private async Task DeleteObjectQuietlyAsync(string key)
    {
        try
        {
            await s3.DeleteObjectAsync(new DeleteObjectRequest { BucketName = blobOptions.Bucket, Key = key });
        }
        catch (Exception e)
        {
            metrics?.BlobError("delete");
            logger.LogWarning(e, "Could not delete {Key} from bucket {Bucket}", key, blobOptions.Bucket);
        }
    }
}
