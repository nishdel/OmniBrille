using OmniBrille.Core;

namespace OmniBrille.Infrastructure.Voice;

public sealed class BoundedVoiceAudioBuffer : IDisposable
{
    private readonly MemoryStream _stream;
    private readonly int _maximumBytes;

    public BoundedVoiceAudioBuffer(int sampleRate, int maximumSeconds)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(sampleRate, 8_000);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(sampleRate, 48_000);
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumSeconds, 1);
        SampleRate = sampleRate;
        _maximumBytes = checked(sampleRate * VoiceAudioClip.BytesPerSample * maximumSeconds);
        _stream = new MemoryStream(Math.Min(_maximumBytes, sampleRate * VoiceAudioClip.BytesPerSample * 5));
    }

    public int SampleRate { get; }

    public int Length => checked((int)_stream.Length);

    public bool IsFull => Length >= _maximumBytes;

    public double Append(ReadOnlySpan<byte> pcm16Mono)
    {
        var remaining = _maximumBytes - Length;
        var length = Math.Min(remaining, pcm16Mono.Length) & ~1;
        if (length <= 0)
        {
            return 0;
        }

        var slice = pcm16Mono[..length];
        _stream.Write(slice);
        var peak = 0;
        for (var index = 0; index < slice.Length; index += VoiceAudioClip.BytesPerSample)
        {
            var sample = Math.Abs((int)(short)(slice[index] | (slice[index + 1] << 8)));
            peak = Math.Max(peak, sample);
        }

        return Math.Clamp(peak / 32768d, 0, 1);
    }

    public VoiceAudioClip ToClip()
    {
        var bytes = _stream.ToArray();
        var duration = TimeSpan.FromSeconds(bytes.Length / (double)(SampleRate * VoiceAudioClip.BytesPerSample));
        return new VoiceAudioClip(bytes, SampleRate, duration);
    }

    public void Dispose() => _stream.Dispose();
}
