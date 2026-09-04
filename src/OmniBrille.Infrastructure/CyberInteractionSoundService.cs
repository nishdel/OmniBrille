using NAudio;
using NAudio.Wave;
using OmniBrille.Core;

namespace OmniBrille.Infrastructure;

/// <summary>
/// A lazy, bounded interaction-cue player. Cues are generated locally, hover requests are
/// coalesced, and the output device is released shortly after the last sound.
/// </summary>
public sealed class CyberInteractionSoundService : IInteractionSoundService
{
    private const int SampleRate = 22_050;
    private static readonly WaveFormat Format = new(SampleRate, 16, 1);
    private static readonly Dictionary<InteractionSoundCue, byte[]> Clips =
        Enum.GetValues<InteractionSoundCue>().ToDictionary(cue => cue, BuildClip);
    private readonly object _gate = new();
    private WaveOutEvent? _output;
    private BufferedWaveProvider? _buffer;
    private Timer? _releaseTimer;
    private DateTime _lastHoverUtc;
    private bool _enabled = true;
    private bool _disposed;

    public bool Enabled
    {
        get => _enabled;
        set
        {
            lock (_gate)
            {
                _enabled = value;
                if (!value)
                {
                    ReleaseOutput();
                }
            }
        }
    }

    public void Play(InteractionSoundCue cue)
    {
        lock (_gate)
        {
            if (_disposed || !_enabled || !OperatingSystem.IsWindows())
            {
                return;
            }

            if (cue == InteractionSoundCue.Hover)
            {
                var now = DateTime.UtcNow;
                if (now - _lastHoverUtc < TimeSpan.FromMilliseconds(90))
                {
                    return;
                }

                _lastHoverUtc = now;
            }

            try
            {
                EnsureOutput();
                var clip = Clips[cue];
                if (_buffer!.BufferedBytes + clip.Length <= _buffer.BufferLength)
                {
                    _buffer.AddSamples(clip, 0, clip.Length);
                }

                _releaseTimer?.Change(TimeSpan.FromSeconds(1), Timeout.InfiniteTimeSpan);
            }
            catch (Exception exception) when (exception is InvalidOperationException or MmException or
                                              System.Runtime.InteropServices.COMException)
            {
                ReleaseOutput();
            }
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;
            ReleaseOutput();
        }
    }

    private void EnsureOutput()
    {
        if (_output is not null)
        {
            return;
        }

        _buffer = new BufferedWaveProvider(Format)
        {
            BufferDuration = TimeSpan.FromMilliseconds(650),
            DiscardOnBufferOverflow = true,
            ReadFully = true,
        };
        _output = new WaveOutEvent { DesiredLatency = 80, NumberOfBuffers = 2 };
        _output.Init(_buffer);
        _output.Play();
        _releaseTimer = new Timer(_ =>
        {
            lock (_gate)
            {
                ReleaseOutput();
            }
        });
    }

    private void ReleaseOutput()
    {
        _releaseTimer?.Dispose();
        _releaseTimer = null;
        _output?.Stop();
        _output?.Dispose();
        _output = null;
        _buffer = null;
    }

    private static byte[] BuildClip(InteractionSoundCue cue)
    {
        var (duration, low, high, volume) = cue switch
        {
            InteractionSoundCue.Hover => (0.045, 920d, 1_160d, 0.07),
            InteractionSoundCue.Select => (0.075, 520d, 880d, 0.1),
            InteractionSoundCue.Navigate => (0.14, 300d, 690d, 0.12),
            InteractionSoundCue.FolderEnter => (0.18, 240d, 780d, 0.13),
            InteractionSoundCue.FileOpen => (0.11, 680d, 1_120d, 0.11),
            _ => (0.06, 500d, 700d, 0.08),
        };
        var sampleCount = Math.Max(1, (int)(SampleRate * duration));
        var bytes = new byte[sampleCount * 2];
        for (var index = 0; index < sampleCount; index++)
        {
            var progress = index / (double)Math.Max(1, sampleCount - 1);
            var frequency = low + ((high - low) * progress);
            var envelope = Math.Sin(Math.PI * progress) * (1 - (progress * 0.35));
            var carrier = Math.Sin(Math.PI * 2 * frequency * index / SampleRate);
            var shimmer = Math.Sin(Math.PI * 2 * (frequency * 1.51) * index / SampleRate) * 0.18;
            var sample = (short)Math.Clamp((carrier + shimmer) * envelope * volume * short.MaxValue, short.MinValue, short.MaxValue);
            bytes[index * 2] = (byte)(sample & 0xff);
            bytes[(index * 2) + 1] = (byte)((sample >> 8) & 0xff);
        }

        return bytes;
    }
}
