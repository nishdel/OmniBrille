using System.Diagnostics;

namespace OmniBrille.Core;

public sealed class VoiceInteractionCoordinator : IDisposable
{
    private readonly IAudioCaptureService _capture;
    private readonly ISpeechRecognitionProvider _speech;
    private readonly IVoiceActionTarget _target;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private CancellationTokenSource? _operationCancellation;
    private CancellationTokenSource? _durationCancellation;
    private VoiceActionContext? _originContext;
    private bool _disposed;

    public VoiceInteractionCoordinator(
        IAudioCaptureService capture,
        ISpeechRecognitionProvider speech,
        IVoiceActionTarget target)
    {
        _capture = capture ?? throw new ArgumentNullException(nameof(capture));
        _speech = speech ?? throw new ArgumentNullException(nameof(speech));
        _target = target ?? throw new ArgumentNullException(nameof(target));
        _capture.LevelChanged += OnLevelChanged;
    }

    public event EventHandler? StateChanged;

    public VoiceCapabilityState State { get; private set; } = VoiceCapabilityState.Disabled;

    public string Status { get; private set; } = "Voice is off.";

    public string? TranscriptPreview { get; private set; }

    public double InputLevel { get; private set; }

    public VoiceDiagnostics Diagnostics { get; private set; } = VoiceDiagnostics.Empty;

    public bool IsActive => State is VoiceCapabilityState.Loading or VoiceCapabilityState.Listening or
        VoiceCapabilityState.Transcribing or VoiceCapabilityState.Executing;

    public async Task RefreshCapabilityAsync(
        VoiceRecognitionOptions options,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        options = options.Normalize();
        if (!options.Enabled)
        {
            SetState(VoiceCapabilityState.Disabled, "Voice is off.");
            return;
        }

        var stopwatch = Stopwatch.StartNew();
        SetState(VoiceCapabilityState.Loading, "Checking local voice setup…");
        var capability = await _speech.GetCapabilityAsync(options, cancellationToken).ConfigureAwait(false);
        if (capability.IsReady)
        {
            var captureCapability = _capture.GetCapability();
            capability = captureCapability.IsReady ? capability : captureCapability;
        }

        stopwatch.Stop();
        Diagnostics = Diagnostics with
        {
            State = capability.State,
            Provider = capability.Provider,
            ModelState = capability.ModelIdentifier,
            InitializationDuration = stopwatch.Elapsed,
            LastErrorCategory = capability.IsReady ? null : capability.State.ToString(),
        };
        SetState(capability.State, capability.Message);
    }

    public async Task StartAsync(
        VoiceRecognitionOptions options,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        options = options.Normalize();
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (IsActive)
            {
                return;
            }

            TranscriptPreview = null;
            InputLevel = 0;
            await RefreshCapabilityAsync(options, cancellationToken).ConfigureAwait(false);
            if (State != VoiceCapabilityState.Ready)
            {
                return;
            }

            _operationCancellation?.Dispose();
            _operationCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _originContext = _target.CaptureVoiceContext();
            try
            {
                await _capture.StartAsync(options, _operationCancellation.Token).ConfigureAwait(false);
                if (_operationCancellation.IsCancellationRequested)
                {
                    await _capture.CancelAsync().ConfigureAwait(false);
                    SetState(VoiceCapabilityState.Cancelled, "Voice cancelled.");
                    ClearOperation();
                    return;
                }
            }
            catch (UnauthorizedAccessException)
            {
                SetFailure(VoiceCapabilityState.PermissionDenied, "Microphone access was denied. Typed Search remains available.");
                ClearOperation();
                return;
            }
            catch (Exception exception) when (exception is InvalidOperationException or IOException or NotSupportedException)
            {
                SetFailure(VoiceCapabilityState.MicrophoneUnavailable, "No usable microphone is available. Typed Search remains available.");
                ClearOperation();
                return;
            }

            SetState(VoiceCapabilityState.Listening, $"Listening… press again to stop. Maximum {options.MaximumUtteranceSeconds} seconds.");
            _durationCancellation?.Dispose();
            _durationCancellation = CancellationTokenSource.CreateLinkedTokenSource(_operationCancellation.Token);
            _ = EnforceDurationLimitAsync(
                TimeSpan.FromSeconds(options.MaximumUtteranceSeconds),
                options,
                _durationCancellation.Token);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task StopAsync(
        VoiceRecognitionOptions options,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        options = options.Normalize();
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (State != VoiceCapabilityState.Listening || _operationCancellation is null || _originContext is null)
            {
                return;
            }

            var operationToken = _operationCancellation.Token;
            var originContext = _originContext;
            _durationCancellation?.Cancel();
            _durationCancellation?.Dispose();
            _durationCancellation = null;
            SetState(VoiceCapabilityState.Transcribing, "Transcribing locally…");
            var clip = await _capture.StopAsync(CancellationToken.None).ConfigureAwait(false);
            InputLevel = 0;
            Diagnostics = Diagnostics with { CaptureDuration = clip.Duration };
            operationToken.ThrowIfCancellationRequested();
            if (clip.IsEmpty)
            {
                SetFailure(VoiceCapabilityState.Error, "No speech audio was captured. Try again or use typed Search.");
                return;
            }

            var transcriptionStopwatch = Stopwatch.StartNew();
            var recognition = await _speech.TranscribeAsync(clip, options, operationToken).ConfigureAwait(false);
            transcriptionStopwatch.Stop();
            var transcript = recognition.Transcript.Trim();
            if (transcript.Length == 0)
            {
                SetFailure(VoiceCapabilityState.Error, "No speech was recognized. Try again or use typed Search.");
                return;
            }

            TranscriptPreview = transcript.Length <= 160 ? transcript : transcript[..157] + "…";
            var intent = VoiceCommandParser.Parse(transcript);
            Diagnostics = Diagnostics with
            {
                State = VoiceCapabilityState.Transcribing,
                Provider = recognition.Provider,
                TranscriptionDuration = transcriptionStopwatch.Elapsed,
                TranscriptLength = transcript.Length,
                Classification = intent.Kind.ToString(),
                LastErrorCategory = null,
            };

            if (!_target.IsVoiceContextCurrent(originContext))
            {
                SetState(VoiceCapabilityState.Cancelled, "Voice result ignored because the provider session changed.");
                return;
            }

            SetState(VoiceCapabilityState.Executing, intent.Kind == VoiceIntentKind.Search ? "Starting Search…" : "Applying voice command…");
            var executionStopwatch = Stopwatch.StartNew();
            var result = await _target.ExecuteVoiceIntentAsync(intent, operationToken).ConfigureAwait(false);
            executionStopwatch.Stop();
            Diagnostics = Diagnostics with
            {
                State = intent.Kind == VoiceIntentKind.Cancel ? VoiceCapabilityState.Cancelled : VoiceCapabilityState.Ready,
                ExecutionDuration = executionStopwatch.Elapsed,
                LastErrorCategory = result.Succeeded ? null : "ActionRejected",
            };
            SetState(
                intent.Kind == VoiceIntentKind.Cancel ? VoiceCapabilityState.Cancelled : VoiceCapabilityState.Ready,
                result.Message);
        }
        catch (OperationCanceledException)
        {
            SetState(VoiceCapabilityState.Cancelled, "Voice cancelled.");
        }
        catch (TimeoutException)
        {
            SetFailure(VoiceCapabilityState.Error, "Local transcription timed out. Try a shorter phrase.");
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or InvalidOperationException or UnauthorizedAccessException)
        {
            SetFailure(VoiceCapabilityState.Error, "Local voice processing failed safely. Typed Search remains available.");
        }
        finally
        {
            ClearOperation();
            _gate.Release();
        }
    }

    public async Task CancelAsync()
    {
        if (_disposed)
        {
            return;
        }

        _operationCancellation?.Cancel();
        _durationCancellation?.Cancel();
        await _capture.CancelAsync().ConfigureAwait(false);
        InputLevel = 0;
        SetState(VoiceCapabilityState.Cancelled, "Voice cancelled.");
    }

    public void Disable()
    {
        _operationCancellation?.Cancel();
        _durationCancellation?.Cancel();
        _ = _capture.CancelAsync();
        TranscriptPreview = null;
        InputLevel = 0;
        SetState(VoiceCapabilityState.Disabled, "Voice is off.");
    }

    public void DismissTranscript()
    {
        TranscriptPreview = null;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _operationCancellation?.Cancel();
        _durationCancellation?.Cancel();
        _capture.LevelChanged -= OnLevelChanged;
        _capture.Dispose();
        _speech.Dispose();
        _operationCancellation?.Dispose();
        _durationCancellation?.Dispose();
        _gate.Dispose();
    }

    private async Task EnforceDurationLimitAsync(
        TimeSpan duration,
        VoiceRecognitionOptions options,
        CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(duration, cancellationToken).ConfigureAwait(false);
            await StopAsync(options, CancellationToken.None).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private void OnLevelChanged(double level)
    {
        InputLevel = Math.Clamp(level, 0, 1);
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void SetFailure(VoiceCapabilityState state, string message)
    {
        Diagnostics = Diagnostics with { State = state, LastErrorCategory = state.ToString() };
        SetState(state, message);
    }

    private void SetState(VoiceCapabilityState state, string message)
    {
        State = state;
        Status = message;
        Diagnostics = Diagnostics with { State = state };
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ClearOperation()
    {
        _originContext = null;
        _operationCancellation?.Dispose();
        _operationCancellation = null;
        _durationCancellation?.Dispose();
        _durationCancellation = null;
    }
}
