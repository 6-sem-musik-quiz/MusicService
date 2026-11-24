using MusicService.Service.Interfaces;

namespace MusicService.Service.Services;

// Reads the mp3 header with TagLibSharp. Only the MPEG parser is tried, so another format
// (wav, flac, ...) is not accepted just because it has a duration.
public class TagLibAudioInspector : IAudioInspector
{
    public int? GetMp3DurationSeconds(Stream audio)
    {
        try
        {
            using var file = TagLib.File.Create(new StreamAbstraction(audio), "audio/mpeg", TagLib.ReadStyle.Average);
            var duration = file.Properties?.Duration ?? TimeSpan.Zero;
            if (file.Properties?.MediaTypes.HasFlag(TagLib.MediaTypes.Audio) != true || duration <= TimeSpan.Zero)
                return null;

            return Math.Max(1, (int)Math.Round(duration.TotalSeconds));
        }
        catch (Exception e) when (e is TagLib.CorruptFileException or TagLib.UnsupportedFormatException
                                      or EndOfStreamException or ArgumentException)
        {
            return null;
        }
    }

    // Lets TagLib read the upload stream directly, without a file on disk. It never closes the stream.
    private sealed class StreamAbstraction(Stream stream) : TagLib.File.IFileAbstraction
    {
        public string Name => "upload.mp3";
        public Stream ReadStream => stream;
        public Stream WriteStream => stream;
        public void CloseStream(Stream s) { }
    }
}
