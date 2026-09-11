using System.Diagnostics;
using System.Security.Cryptography;

namespace OmniBrille.Core;

public sealed class VoiceInteractionCoordinator : IDisposable
{
    private readonly IAudioCaptureService _capture;
    private readonly ISpeechRecognitionProvider _speech;
    private readonly IVoiceActionTarget _target;
    private readonly TimeProvider _timeProvider;
    private readonly object _listeningSync = new();
    private readonly SemaphoreSlim _gate = new(1, 1);
    private CancellationTokenSource? _operationCancellation;
    private CancellationTokenSource? _durationCancellation;
    private VoiceActionContext? _originContext;
    private ListeningOperation? _listeningOperation;
    private long _operationId;
    private bool _disposed;

    public VoiceInteractionCoordinator(
        IAudioCaptureService capture,
        ISpeechRecognitionProvider speech,
        IVoiceActionTarget target,
        TimeProvider? timeProvider = null)
    {
        _capture = capture ?? throw new ArgumentNullException(nameof(capture));
        _speech = speech ?? throw new ArgumentNullException(nameof(speech));
        _target = target ?? throw new ArgumentNullException(nameof(target));
        _timeProvider = timeProvider ?? TimeProvider.System;
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
        cancellationToken.ThrowIfCancellationRequested();
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
            _operationCancellation?.Dispose();
            _operationCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var operationCancellation = _operationCancellation;
            var operationId = ++_operationId;
            _originContext = _target.CaptureVoiceContext();
            try
            {
                await RefreshCapabilityAsync(options, operationCancellation.Token).ConfigureAwait(false);
                operationCancellation.Token.ThrowIfCancellationRequested();
            }
            catch (OperationCanceledException) when (operationCancellation.IsCancellationRequested)
            {
                await _capture.CancelAsync().ConfigureAwait(false);
                InputLevel = 0;
                SetState(VoiceCapabilityState.Cancelled, "Voice cancelled.");
                ClearOperation();
                return;
            }

            if (State != VoiceCapabilityState.Ready)
            {
                ClearOperation();
                return;
            }

            if (!_target.IsVoiceContextCurrent(_originContext!))
            {
                SetState(VoiceCapabilityState.Cancelled, "Voice cancelled because the provider session changed.");
                ClearOperation();
                return;
            }

            try
            {
                lock (_listeningSync)
                {
                    _listeningOperation = new ListeningOperation(operationId, options);
                }
                await _capture.StartAsync(options, operationCancellation.Token).ConfigureAwait(false);
                if (operationCancellation.IsCancellationRequested || !_target.IsVoiceContextCurrent(_originContext!))
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

            SetState(VoiceCapabilityState.Listening, $"Listening… stop or pause for 2 seconds after speaking. Maximum {options.MaximumUtteranceSeconds} seconds.");
            _durationCancellation?.Dispose();
            _durationCancellation = CancellationTokenSource.CreateLinkedTokenSource(_operationCancellation.Token);
            _ = EnforceDurationLimitAsync(
                TimeSpan.FromSeconds(options.MaximumUtteranceSeconds),
                options,
                operationId,
                _durationCancellation.Token);
        }
        finally
        {
            _gate.Release();
        }
    }

    public Task StopAsync(
        VoiceRecognitionOptions options,
        CancellationToken cancellationToken = default) =>
        StopCoreAsync(options, expectedOperationId: null, cancellationToken);

    private async Task StopCoreAsync(
        VoiceRecognitionOptions options,
        long? expectedOperationId,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        options = options.Normalize();
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        var ownsOperation = false;
        VoiceAudioClip? clip = null;
        try
        {
            if (State != VoiceCapabilityState.Listening || _operationCancellation is null || _originContext is null ||
                (expectedOperationId is not null && expectedOperationId != _operationId))
            {
                return;
            }

            ownsOperation = true;
            bool speechDetected;
            lock (_listeningSync)
            {
                speechDetected = _listeningOperation?.HasSpeech == true;
            }
            ClearListeningOperation();
            var operationToken = _operationCancellation.Token;
            var originContext = _originContext;
            _durationCancellation?.Cancel();
            _durationCancellation?.Dispose();
            _durationCancellation = null;
            SetState(VoiceCapabilityState.Transcribing, "Transcribing locally…");
            clip = await _capture.StopAsync(CancellationToken.None).ConfigureAwait(false);
            InputLevel = 0;
            Diagnostics = Diagnostics with { CaptureDuration = clip.Duration };
            operationToken.ThrowIfCancellationRequested();
            if (expectedOperationId is not null && !speechDetected)
            {
                SetFailure(VoiceCapabilityState.Error, "No speech was detected. Press the microphone to try again.");
                return;
            }
            if (clip.IsEmpty)
            {
                SetFailure(VoiceCapabilityState.Error, "No speech audio was captured. Try again or use typed Search.");
                return;
            }

            var transcriptionStopwatch = Stopwatch.StartNew();
            var recognition = await _speech.TranscribeAsync(clip, options, operationToken).ConfigureAwait(false);
            operationToken.ThrowIfCancellationRequested();
            transcriptionStopwatch.Stop();
            var transcript = recognition.Transcript.Trim();
            if (transcript.Length == 0)
            {
                SetFailure(VoiceCapabilityState.Error, "No speech was recognized. Try again or use typed Search.");
                return;
            }

            if (!_target.IsVoiceContextCurrent(originContext))
            {
                SetState(VoiceCapabilityState.Cancelled, "Voice result ignored because the provider session changed.");
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
            if (clip is not null)
            {
                CryptographicOperations.ZeroMemory(clip.Pcm16Mono);
            }
            if (ownsOperation)
            {
                ClearOperation();
            }
            _gate.Release();
        }
    }

    public async Task CancelAsync()
    {
        if (_disposed)
        {
            return;
        }

        var operationId = Volatile.Read(ref _operationId);
        var operationCancellation = _operationCancellation;
        var durationCancellation = _durationCancellation;
        ClearListeningOperation(operationId);
        // Bind device cleanup before token callbacks can complete transcription and
        // release the operation gate to a new capture.
        var captureCancellation = _capture.CancelAsync();
        CancelIfAlive(operationCancellation);
        CancelIfAlive(durationCancellation);
        await captureCancellation.ConfigureAwait(false);
        try
        {
            // A starting/stopping operation observes its cancelled token and owns its
            // own final state. Do not wait on a recognizer that ignores cancellation.
            if (!_gate.Wait(0))
            {
                return;
            }

            try
            {
                if (_disposed || operationId != _operationId)
                {
                    return;
                }

                InputLevel = 0;
                SetState(VoiceCapabilityState.Cancelled, "Voice cancelled.");
            }
            finally
            {
                _gate.Release();
            }
        }
        catch (ObjectDisposedException) when (_disposed)
        {
        }
    }

    public void Disable()
    {
        ClearListeningOperation();
        _ = _capture.CancelAsync();
        CancelIfAlive(_operationCancellation);
        CancelIfAlive(_durationCancellation);
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
        ClearListeningOperation();
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
        long operationId,
        CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(duration, _timeProvider, cancellationToken).ConfigureAwait(false);
            await StopCoreAsync(options, operationId, CancellationToken.None).ConfigureAwait(false);
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
        level = double.IsFinite(level) ? Math.Clamp(level, 0, 1) : 0;
        ListeningOperation? completedUtterance = null;
        lock (_listeningSync)
        {
            var operation = _listeningOperation;
            InputLevel = operation is null ? 0 : level;
            if (operation is not null && !operation.StopRequested)
            {
                // Capture reports normalized PCM peaks every 100 ms. This is a bounded
                // level gate, not a speech model: initial quiet never submits a command.
                if (level >= 0.025)
                {
                    operation.HasSpeech = true;
                    operation.LastSpeechTimestamp = _timeProvider.GetTimestamp();
                }
                else if (operation.HasSpeech && State == VoiceCapabilityState.Listening &&
                         _timeProvider.GetElapsedTime(operation.LastSpeechTimestamp) >= TimeSpan.FromSeconds(2))
                {
                    operation.StopRequested = true;
                    completedUtterance = operation;
                }
            }
        }
        StateChanged?.Invoke(this, EventArgs.Empty);
        if (completedUtterance is not null)
        {
            _ = StopAfterSilenceAsync(completedUtterance);
        }
    }

    private async Task StopAfterSilenceAsync(ListeningOperation operation)
    {
        try
        {
            await StopCoreAsync(operation.Options, operation.Id, CancellationToken.None).ConfigureAwait(false);
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private void ClearListeningOperation(long? expectedOperationId = null)
    {
        lock (_listeningSync)
        {
            if (expectedOperationId is null || _listeningOperation?.Id == expectedOperationId)
            {
                _listeningOperation = null;
            }
        }
    }

    private static void CancelIfAlive(CancellationTokenSource? cancellation)
    {
        try
        {
            cancellation?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // The owning operation completed while this cancellation was being issued.
        }
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
        ClearListeningOperation();
        _originContext = null;
        _operationCancellation?.Dispose();
        _operationCancellation = null;
        _durationCancellation?.Dispose();
        _durationCancellation = null;
    }

    private sealed class ListeningOperation(long id, VoiceRecognitionOptions options)
    {
        public long Id { get; } = id;

        public VoiceRecognitionOptions Options { get; } = options;

        public bool HasSpeech { get; set; }

        public long LastSpeechTimestamp { get; set; }

        public bool StopRequested { get; set; }
    }
}
