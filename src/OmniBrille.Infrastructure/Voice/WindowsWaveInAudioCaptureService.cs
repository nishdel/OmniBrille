using NAudio;
using NAudio.Wave;
using OmniBrille.Core;

namespace OmniBrille.Infrastructure.Voice;

public sealed class WindowsWaveInAudioCaptureService : IAudioCaptureService
{
    private const int CaptureSampleRate = 16_000;
    private readonly object _sync = new();
    private WaveInEvent? _recorder;
    private BoundedVoiceAudioBuffer? _buffer;
    private TaskCompletionSource<Exception?>? _stopped;
    private bool _disposed;

    public event Action<double>? LevelChanged;

    public VoiceCapability GetCapability()
    {
        if (!OperatingSystem.IsWindows())
        {
            return new VoiceCapability(
                VoiceCapabilityState.MicrophoneUnavailable,
                "Microphone capture is currently validated on Windows only.",
                "Windows WaveIn");
        }

        try
        {
            return WaveInEvent.DeviceCount > 0
                ? new VoiceCapability(
                    VoiceCapabilityState.Ready,
                    "Microphone capture is available.",
                    "Windows WaveIn")
                : new VoiceCapability(
                    VoiceCapabilityState.MicrophoneUnavailable,
                    "No microphone input device is available.",
                    "Windows WaveIn");
        }
        catch (Exception exception) when (exception is MmException or TypeInitializationException or DllNotFoundException)
        {
            return new VoiceCapability(
                VoiceCapabilityState.MicrophoneUnavailable,
                "Windows microphone capture is unavailable.",
                "Windows WaveIn");
        }
    }

    public Task StartAsync(VoiceRecognitionOptions options, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();
        var capability = GetCapability();
        if (!capability.IsReady)
        {
            throw new InvalidOperationException(capability.Message);
        }

        lock (_sync)
        {
            if (_recorder is not null)
            {
                throw new InvalidOperationException("Microphone capture is already active.");
            }

            _buffer = new BoundedVoiceAudioBuffer(CaptureSampleRate, options.Normalize().MaximumUtteranceSeconds);
            _stopped = new TaskCompletionSource<Exception?>(TaskCreationOptions.RunContinuationsAsynchronously);
            _recorder = new WaveInEvent
            {
                DeviceNumber = -1,
                WaveFormat = new WaveFormat(CaptureSampleRate, 16, 1),
                BufferMilliseconds = 100,
                NumberOfBuffers = 3,
            };
            _recorder.DataAvailable += OnDataAvailable;
            _recorder.RecordingStopped += OnRecordingStopped;
            try
            {
                _recorder.StartRecording();
            }
            catch
            {
                CleanupRecorder();
                throw;
            }
        }

        return Task.CompletedTask;
    }

    public async Task<VoiceAudioClip> StopAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        Task<Exception?>? stoppedTask;
        lock (_sync)
        {
            if (_recorder is null || _buffer is null || _stopped is null)
            {
                return new VoiceAudioClip([], CaptureSampleRate, TimeSpan.Zero);
            }

            stoppedTask = _stopped.Task;
            _recorder.StopRecording();
        }

        var failure = await stoppedTask.WaitAsync(cancellationToken).ConfigureAwait(false);
        lock (_sync)
        {
            var clip = _buffer?.ToClip() ?? new VoiceAudioClip([], CaptureSampleRate, TimeSpan.Zero);
            CleanupRecorder();
            if (failure is not null)
            {
                throw new InvalidOperationException("Microphone capture stopped unexpectedly.", failure);
            }

            return clip;
        }
    }

    public async Task CancelAsync()
    {
        Task<Exception?>? stoppedTask = null;
        lock (_sync)
        {
            if (_recorder is not null && _stopped is not null)
            {
                stoppedTask = _stopped.Task;
                try
                {
                    _recorder.StopRecording();
                }
                catch (MmException)
                {
                }
            }
        }

        if (stoppedTask is not null)
        {
            try
            {
                await stoppedTask.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is TimeoutException or InvalidOperationException)
            {
            }
        }

        lock (_sync)
        {
            CleanupRecorder();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _ = CancelAsync();
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs eventArgs)
    {
        double level;
        lock (_sync)
        {
            if (_buffer is null)
            {
                return;
            }

            level = _buffer.Append(eventArgs.Buffer.AsSpan(0, eventArgs.BytesRecorded));
        }

        LevelChanged?.Invoke(level);
    }

    private void OnRecordingStopped(object? sender, StoppedEventArgs eventArgs) =>
        _stopped?.TrySetResult(eventArgs.Exception);

    private void CleanupRecorder()
    {
        if (_recorder is not null)
        {
            _recorder.DataAvailable -= OnDataAvailable;
            _recorder.RecordingStopped -= OnRecordingStopped;
            _recorder.Dispose();
        }

        _recorder = null;
        _buffer?.Dispose();
        _buffer = null;
        _stopped = null;
        LevelChanged?.Invoke(0);
    }
}
