using System.Text;
using MusicService.Service.Services;

namespace MusicService.Service.Tests;

public class TagLibAudioInspectorTests
{
    private readonly TagLibAudioInspector _inspector = new();

    [Theory]
    [InlineData(2)]
    [InlineData(30)]
    public void Mp3_ReturnsLengthInSeconds(int seconds)
    {
        using var stream = new MemoryStream(TestMp3.Create(seconds));

        Assert.Equal(seconds, _inspector.GetMp3DurationSeconds(stream));
    }

    [Fact]
    public void Mp3_DoesNotCloseTheStream()
    {
        using var stream = new MemoryStream(TestMp3.Create(2));

        _inspector.GetMp3DurationSeconds(stream);

        Assert.True(stream.CanRead);
    }

    [Fact]
    public void TextFile_ReturnsNull()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("This is not an mp3, just some text."));

        Assert.Null(_inspector.GetMp3DurationSeconds(stream));
    }

    [Fact]
    public void EmptyFile_ReturnsNull()
    {
        using var stream = new MemoryStream();

        Assert.Null(_inspector.GetMp3DurationSeconds(stream));
    }
}
