using System.Diagnostics.Metrics;

namespace MusicService.Service.Metrics;

// SCRUM-173: MusicService's own instruments (meter "Slopster.Music"), exported over OTLP.
//   slopster_song_draws_total{playlist_id}         one per random draw of songs for a game, by playlist
//   slopster_blob_errors_total{operation=upload|delete}  failed calls to blob storage (Garage, S3 API)
public sealed class MusicMetrics : IDisposable
{
    public const string MeterName = "Slopster.Music";
    public const string SongDrawsName = "slopster_song_draws_total";
    public const string BlobErrorsName = "slopster_blob_errors_total";

    private readonly Counter<long> _draws;
    private readonly Counter<long> _blobErrors;

    public MusicMetrics()
    {
        Meter = new Meter(MeterName);
        _draws = Meter.CreateCounter<long>(SongDrawsName, description: "Random song draws for a game, by playlist.");
        _blobErrors = Meter.CreateCounter<long>(BlobErrorsName, description: "Failed calls to blob storage, by operation.");
    }

    public Meter Meter { get; }

    public void SongDraw(int playlistId) =>
        _draws.Add(1, new KeyValuePair<string, object?>("playlist_id", playlistId));

    public void BlobError(string operation) =>
        _blobErrors.Add(1, new KeyValuePair<string, object?>("operation", operation));

    public void Dispose() => Meter.Dispose();
}
