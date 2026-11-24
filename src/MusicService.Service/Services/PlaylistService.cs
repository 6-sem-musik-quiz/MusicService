using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;
using MusicService.Domain.Dtos;
using MusicService.Domain.Exceptions;
using MusicService.Repository.BlobStorage;
using MusicService.Repository.Interfaces;
using MusicService.Service.Interfaces;

namespace MusicService.Service.Services;

// Playlist rules: names are 1-60 characters and unique (case-insensitive). Deleting a playlist
// also deletes the songs that are in no other playlist, including their files in blob storage.
public class PlaylistService(
    IPlaylistRepository repository,
    IAmazonS3 s3,
    BlobStorageOptions blobOptions,
    ILogger<PlaylistService> logger) : IPlaylistService
{
    public const int MaxNameLength = 60;

    public Task<IReadOnlyList<PlaylistDto>> GetAllAsync(CancellationToken ct = default) =>
        repository.GetAllAsync(ct);

    public async Task<PlaylistDto> GetAsync(int id, CancellationToken ct = default) =>
        await repository.GetAsync(id, ct) ?? throw new PlaylistNotFoundException(id);

    public async Task<PlaylistDto> CreateAsync(PlaylistNameRequest request, CancellationToken ct = default)
    {
        var name = ValidName(request.Name);
        var id = await repository.TryAddAsync(name, ct)
                 ?? throw NameTaken(name);

        return new PlaylistDto(id, name, 0);
    }

    public async Task<PlaylistDto> RenameAsync(int id, PlaylistNameRequest request, CancellationToken ct = default)
    {
        var name = ValidName(request.Name);

        return await repository.TryRenameAsync(id, name, ct) switch
        {
            RenamePlaylistResult.NotFound => throw new PlaylistNotFoundException(id),
            RenamePlaylistResult.NameTaken => throw NameTaken(name),
            _ => await GetAsync(id, ct)
        };
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var deletedBlobKeys = await repository.DeleteAsync(id, ct) ?? throw new PlaylistNotFoundException(id);

        // The database is already committed. A file that cannot be deleted is only logged:
        // nothing points to it any more, so it is unreachable garbage, not a broken song.
        foreach (var blobKey in deletedBlobKeys)
        {
            var key = BlobStorageOptions.SongObjectKey(blobKey);
            try
            {
                await s3.DeleteObjectAsync(new DeleteObjectRequest { BucketName = blobOptions.Bucket, Key = key }, ct);
            }
            catch (Exception e) when (e is AmazonS3Exception or InvalidOperationException or HttpRequestException)
            {
                logger.LogWarning(e, "Could not delete {Key} from bucket {Bucket} after deleting playlist {PlaylistId}",
                    key, blobOptions.Bucket, id);
            }
        }
    }

    private static MusicConflictException NameTaken(string name) =>
        new($"A playlist named \"{name}\" already exists.");

    private static string ValidName(string? name)
    {
        var trimmed = name?.Trim() ?? string.Empty;
        if (trimmed.Length is 0 or > MaxNameLength)
            throw new MusicValidationException($"name must be between 1 and {MaxNameLength} characters.");
        return trimmed;
    }
}
