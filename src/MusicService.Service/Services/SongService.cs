using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;
using MusicService.Domain.Dtos;
using MusicService.Domain.Entities;
using MusicService.Domain.Exceptions;
using MusicService.Repository.BlobStorage;
using MusicService.Repository.Interfaces;
using MusicService.Service.Interfaces;

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
    ILogger<SongService> logger) : ISongService
{
    public const int MaxTitleLength = 200;
    public const int MinYear = 1900;
    public const string Mp3ContentType = "audio/mpeg";

    public async Task<SongDto> UploadAsync(int playlistId, UploadSongRequest request, Stream file, string? contentType,
        CancellationToken ct = default)
    {
        var title = ValidTitle(request.Title);
        ValidateYear(request.Year);
        var artistIds = request.ArtistIds?.Distinct().ToList() ?? [];
        if (artistIds.Count == 0)
            throw new MusicValidationException("artistIds must contain at least one artist.");
        if (!IsMp3ContentType(contentType))
            throw new MusicValidationException("file must be an mp3 (audio/mpeg).");

        if (await playlists.GetAsync(playlistId, ct) is null)
            throw new PlaylistNotFoundException(playlistId);

        var existing = await artists.GetExistingIdsAsync(artistIds, ct);
        var unknown = artistIds.Where(id => !existing.Contains(id)).ToList();
        if (unknown.Count > 0)
            throw new MusicValidationException($"Unknown artistIds: {string.Join(", ", unknown)}.");

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

    // Used when the database save failed after the upload. Not cancelled with the request,
    // so a client that gives up does not leave the file behind.
    private async Task DeleteObjectQuietlyAsync(string key)
    {
        try
        {
            await s3.DeleteObjectAsync(new DeleteObjectRequest { BucketName = blobOptions.Bucket, Key = key });
        }
        catch (Exception e)
        {
            logger.LogWarning(e, "Could not delete {Key} from bucket {Bucket} after a failed save", key, blobOptions.Bucket);
        }
    }
}
