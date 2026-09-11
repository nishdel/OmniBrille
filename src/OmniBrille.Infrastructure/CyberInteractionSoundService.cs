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
    private static readonly WaveFormat Format = new(InteractionSoundSynthesis.SampleRate, 16, 1);
    private static readonly Dictionary<InteractionSoundCue, byte[]> Clips =
        Enum.GetValues<InteractionSoundCue>().ToDictionary(cue => cue, InteractionSoundSynthesis.CreatePcm16Mono);
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
}
