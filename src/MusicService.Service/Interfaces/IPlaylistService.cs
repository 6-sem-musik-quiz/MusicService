using MusicService.Domain.Dtos;

namespace MusicService.Service.Interfaces;

public interface IPlaylistService
{
    Task<IReadOnlyList<PlaylistDto>> GetAllAsync(CancellationToken ct = default);
    Task<PlaylistDto> GetAsync(int id, CancellationToken ct = default);
    Task<PlaylistDto> CreateAsync(PlaylistNameRequest request, CancellationToken ct = default);
    Task<PlaylistDto> RenameAsync(int id, PlaylistNameRequest request, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}
