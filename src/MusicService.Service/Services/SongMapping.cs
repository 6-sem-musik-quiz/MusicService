using MusicService.Domain.Dtos;
using MusicService.Domain.Entities;
using MusicService.Repository.BlobStorage;

namespace MusicService.Service.Services;

public static class SongMapping
{
    // The song must have SongArtists.Artist loaded. Artists are sorted by name.
    public static SongDto ToDto(this Song song, BlobStorageOptions blobOptions) => new(
        song.Id,
        song.Title,
        song.SongArtists.Select(sa => sa.Artist.Name).Order(StringComparer.OrdinalIgnoreCase).ToList(),
        song.Year,
        song.DurationSeconds,
        blobOptions.PublicSongUrl(song.BlobKey));
}
