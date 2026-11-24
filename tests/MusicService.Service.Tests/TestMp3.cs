namespace MusicService.Service.Tests;

// A silent mp3 built from scratch: MPEG-1 Layer III frames at 128 kbit/s, 44.1 kHz, no padding.
// Each frame is 417 bytes (144 * 128000 / 44100) with a 4-byte header and a zeroed body.
// No real song is used in tests.
public static class TestMp3
{
    private const int FrameLength = 417;
    private static readonly byte[] FrameHeader = [0xFF, 0xFB, 0x90, 0x00];

    // Roughly `seconds` long: 128 kbit/s is 16000 bytes per second.
    public static byte[] Create(int seconds)
    {
        var frames = (int)Math.Ceiling(seconds * 16000.0 / FrameLength);
        var bytes = new byte[frames * FrameLength];
        for (var i = 0; i < frames; i++)
            FrameHeader.CopyTo(bytes, i * FrameLength);
        return bytes;
    }
}
