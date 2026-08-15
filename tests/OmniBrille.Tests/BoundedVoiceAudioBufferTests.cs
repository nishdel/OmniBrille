using OmniBrille.Infrastructure.Voice;

namespace OmniBrille.Tests;

public sealed class BoundedVoiceAudioBufferTests
{
    [Fact]
    public void Append_EnforcesDurationAndEvenPcmBoundary()
    {
        using var buffer = new BoundedVoiceAudioBuffer(8_000, 1);
        var bytes = Enumerable.Repeat((byte)0x7F, 20_001).ToArray();

        var level = buffer.Append(bytes);
        var clip = buffer.ToClip();

        Assert.True(buffer.IsFull);
        Assert.Equal(16_000, clip.Pcm16Mono.Length);
        Assert.Equal(TimeSpan.FromSeconds(1), clip.Duration);
        Assert.InRange(level, 0, 1);
    }

    [Fact]
    public void Append_ReportsNormalizedPeakLevel()
    {
        using var buffer = new BoundedVoiceAudioBuffer(8_000, 1);

        var level = buffer.Append([0xFF, 0x7F]);

        Assert.InRange(level, 0.999, 1);
    }
}
