namespace MusicService.Service.Interfaces;

public interface IAudioInspector
{
    // Length of an mp3 in whole seconds, or null if the stream is not an mp3 whose length can be read.
    // Reads from the current position; the caller resets the position afterwards.
    int? GetMp3DurationSeconds(Stream audio);
}
