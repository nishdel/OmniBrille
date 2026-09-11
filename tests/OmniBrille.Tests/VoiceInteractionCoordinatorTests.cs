using OmniBrille.Core;

namespace OmniBrille.Tests;

public sealed class VoiceInteractionCoordinatorTests
{
    private static readonly VoiceRecognitionOptions EnabledOptions = new(true);

    [Fact]
    public async Task MaximumDuration_StopsInitialSilenceWithoutTranscriptionAndClearsCapture()
    {
        using var capture = new FakeCapture { InitialLevel = 0 };
        using var speech = new FakeSpeech("must never be used");
        var target = new FakeTarget();
        var clock = new ManualVoiceClock();
        using var coordinator = new VoiceInteractionCoordinator(capture, speech, target, clock);
        var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        coordinator.StateChanged += (_, _) =>
        {
            if (coordinator.State == VoiceCapabilityState.Error)
            {
                finished.TrySetResult();
            }
        };
        await coordinator.StartAsync(EnabledOptions);

        clock.Advance(TimeSpan.FromSeconds(45));
        await finished.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await coordinator.StopAsync(EnabledOptions);

        Assert.Equal(1, capture.StopCount);
        Assert.False(speech.TranscriptionStarted.Task.IsCompleted);
        Assert.Empty(target.Executed);
        Assert.Null(coordinator.TranscriptPreview);
        Assert.DoesNotContain(capture.LastClip!.Pcm16Mono, sample => sample != 0);
    }

    [Fact]
    public async Task Silence_AutomaticallySubmitsOnceAfterTwoSecondsFollowingSpeech()
    {
        using var capture = new FakeCapture();
        using var speech = new FakeSpeech("go back");
        var target = new FakeTarget();
        var clock = new ManualVoiceClock();
        using var coordinator = new VoiceInteractionCoordinator(capture, speech, target, clock);
        await coordinator.StartAsync(EnabledOptions);

        clock.Advance(TimeSpan.FromMilliseconds(1_999));
        capture.ReportLevel(0.001);
        Assert.Equal(VoiceCapabilityState.Listening, coordinator.State);
        clock.Advance(TimeSpan.FromMilliseconds(1));
        capture.ReportLevel(0.001);
        capture.ReportLevel(0);

        Assert.Equal(VoiceCapabilityState.Ready, coordinator.State);
        Assert.Equal(1, capture.StopCount);
        Assert.Equal(VoiceIntentKind.GoBack, Assert.Single(target.Executed).Kind);
    }

    [Fact]
    public async Task Silence_InitialQuietDoesNotSubmitAndSpeechResetsQuietDeadline()
    {
        using var capture = new FakeCapture { InitialLevel = 0 };
        using var speech = new FakeSpeech("go back");
        var target = new FakeTarget();
        var clock = new ManualVoiceClock();
        using var coordinator = new VoiceInteractionCoordinator(capture, speech, target, clock);
        await coordinator.StartAsync(EnabledOptions);
        clock.Advance(TimeSpan.FromSeconds(10));
        capture.ReportLevel(0.001);
        Assert.Equal(VoiceCapabilityState.Listening, coordinator.State);
        Assert.Empty(target.Executed);

        capture.ReportLevel(0.4);
        clock.Advance(TimeSpan.FromMilliseconds(1_500));
        capture.ReportLevel(0.001);
        capture.ReportLevel(0.4);
        clock.Advance(TimeSpan.FromMilliseconds(1_999));
        capture.ReportLevel(0.001);
        Assert.Equal(VoiceCapabilityState.Listening, coordinator.State);
        clock.Advance(TimeSpan.FromMilliseconds(1));
        capture.ReportLevel(0);

        Assert.Equal(1, capture.StopCount);
        Assert.Single(target.Executed);
    }

    [Fact]
    public async Task Silence_ObsoleteStopCannotStopOrClearRestartedCapture()
    {
        using var capture = new FakeCapture();
        using var speech = new FakeSpeech("go back");
        var target = new FakeTarget();
        var clock = new ManualVoiceClock();
        using var coordinator = new VoiceInteractionCoordinator(capture, speech, target, clock);
        await coordinator.StartAsync(EnabledOptions);
        var restart = true;
        coordinator.StateChanged += (_, _) =>
        {
            if (!restart)
            {
                return;
            }

            restart = false;
            // Reenter between the quiet sample and its queued stop, as a UI action can.
            Assert.True(coordinator.CancelAsync().IsCompletedSuccessfully);
            Assert.True(coordinator.StartAsync(EnabledOptions).IsCompletedSuccessfully);
        };

        clock.Advance(TimeSpan.FromSeconds(2));
        capture.ReportLevel(0);

        Assert.Equal(VoiceCapabilityState.Listening, coordinator.State);
        Assert.Equal(2, capture.StartCount);
        Assert.Equal(0, capture.StopCount);
        await coordinator.StopAsync(EnabledOptions);
        Assert.Single(target.Executed);
    }

    [Fact]
    public async Task Silence_CancelAndProviderReplacementCannotExecuteLateCommand()
    {
        using var capture = new FakeCapture();
        using var speech = new FakeSpeech("go back");
        var target = new FakeTarget();
        var clock = new ManualVoiceClock();
        using var coordinator = new VoiceInteractionCoordinator(capture, speech, target, clock);
        await coordinator.StartAsync(EnabledOptions);
        await coordinator.CancelAsync();
        clock.Advance(TimeSpan.FromSeconds(2));
        capture.ReportLevel(0);
        Assert.Equal(0, capture.StopCount);

        await coordinator.StartAsync(EnabledOptions);
        target.Generation++;
        clock.Advance(TimeSpan.FromSeconds(2));
        capture.ReportLevel(0);

        Assert.Equal(VoiceCapabilityState.Cancelled, coordinator.State);
        Assert.Null(coordinator.TranscriptPreview);
        Assert.Empty(target.Executed);
    }

    [Fact]
    public async Task Cancel_RejectsLateTranscriptEvenWhenRecognizerIgnoresCancellation()
    {
        using var capture = new FakeCapture();
        using var speech = new FakeSpeech("go back") { HoldTranscript = true };
        var target = new FakeTarget();
        using var coordinator = new VoiceInteractionCoordinator(capture, speech, target);
        await coordinator.StartAsync(EnabledOptions);
        var stopping = coordinator.StopAsync(EnabledOptions);
        await speech.TranscriptionStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));

        await coordinator.CancelAsync();
        speech.ReleaseTranscript.TrySetResult();
        await stopping;

        Assert.Equal(VoiceCapabilityState.Cancelled, coordinator.State);
        Assert.Null(coordinator.TranscriptPreview);
        Assert.Empty(target.Executed);
    }

    [Fact]
    public async Task Cancel_CompletionCannotCancelRestartedListeningAfterOldTranscriptionEnds()
    {
        using var capture = new FakeCapture { HoldCancellation = true };
        using var speech = new FakeSpeech("go back") { HoldTranscript = true };
        var target = new FakeTarget();
        using var coordinator = new VoiceInteractionCoordinator(capture, speech, target);
        await coordinator.StartAsync(EnabledOptions);
        var stopping = coordinator.StopAsync(EnabledOptions);
        await speech.TranscriptionStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));

        var cancelling = coordinator.CancelAsync();
        await capture.CancellationStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        speech.ReleaseTranscript.TrySetResult();
        await stopping;
        Assert.Equal(VoiceCapabilityState.Cancelled, coordinator.State);

        await coordinator.StartAsync(EnabledOptions);
        Assert.Equal(VoiceCapabilityState.Listening, coordinator.State);
        capture.ReleaseCancellation.TrySetResult();
        await cancelling;

        Assert.Equal(VoiceCapabilityState.Listening, coordinator.State);
        Assert.Equal(0.5, coordinator.InputLevel);
        Assert.Equal(2, capture.StartCount);
        Assert.Empty(target.Executed);
        Assert.Null(coordinator.TranscriptPreview);
        await coordinator.StopAsync(EnabledOptions);
        Assert.Equal(VoiceIntentKind.GoBack, Assert.Single(target.Executed).Kind);
    }

    [Fact]
    public async Task StartAndStop_TranscribesAndExecutesOneShotIntent()
    {
        using var capture = new FakeCapture();
        using var speech = new FakeSpeech("Use dark mode");
        var target = new FakeTarget();
        using var coordinator = new VoiceInteractionCoordinator(capture, speech, target);

        await coordinator.StartAsync(EnabledOptions);
        Assert.Equal(VoiceCapabilityState.Listening, coordinator.State);

        await coordinator.StopAsync(EnabledOptions);

        var intent = Assert.Single(target.Executed);
        Assert.Equal(VoiceIntentKind.UseDarkTheme, intent.Kind);
        Assert.Equal(VoiceCapabilityState.Ready, coordinator.State);
        Assert.Equal("Use dark mode", coordinator.TranscriptPreview);
        Assert.Equal(13, coordinator.Diagnostics.TranscriptLength);
        Assert.Equal(nameof(VoiceIntentKind.UseDarkTheme), coordinator.Diagnostics.Classification);
    }

    [Fact]
    public async Task CancelDuringCapabilityCheck_NeverStartsMicrophoneAfterCancellation()
    {
        using var capture = new FakeCapture();
        using var speech = new FakeSpeech("ignored") { DelayCapability = true };
        using var coordinator = new VoiceInteractionCoordinator(capture, speech, new FakeTarget());

        var starting = coordinator.StartAsync(EnabledOptions);
        await speech.CapabilityStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await coordinator.CancelAsync();
        speech.ReleaseCapability.TrySetResult();
        await starting;

        Assert.Equal(VoiceCapabilityState.Cancelled, coordinator.State);
        Assert.Equal(0, capture.StartCount);
    }

    [Fact]
    public async Task ProviderChangeDuringCapabilityCheck_NeverStartsMicrophoneForNewAuthority()
    {
        using var capture = new FakeCapture();
        using var speech = new FakeSpeech("ignored") { DelayCapability = true };
        var target = new FakeTarget();
        using var coordinator = new VoiceInteractionCoordinator(capture, speech, target);

        var starting = coordinator.StartAsync(EnabledOptions);
        await speech.CapabilityStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        target.Generation++;
        speech.ReleaseCapability.TrySetResult();
        await starting;

        Assert.Equal(VoiceCapabilityState.Cancelled, coordinator.State);
        Assert.Contains("provider session changed", coordinator.Status, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, capture.StartCount);
    }

    [Fact]
    public async Task Stop_RejectsTranscriptWhenProviderGenerationChanged()
    {
        using var capture = new FakeCapture();
        using var speech = new FakeSpeech("find invoices");
        var target = new FakeTarget();
        using var coordinator = new VoiceInteractionCoordinator(capture, speech, target);

        await coordinator.StartAsync(EnabledOptions);
        target.Generation++;
        await coordinator.StopAsync(EnabledOptions);

        Assert.Empty(target.Executed);
        Assert.Equal(VoiceCapabilityState.Cancelled, coordinator.State);
        Assert.Contains("session changed", coordinator.Status, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(VoiceCapabilityState.RuntimeMissing)]
    [InlineData(VoiceCapabilityState.ModelMissing)]
    [InlineData(VoiceCapabilityState.Error)]
    public async Task Start_DoesNotCaptureWhenSpeechCapabilityUnavailable(VoiceCapabilityState state)
    {
        using var capture = new FakeCapture();
        using var speech = new FakeSpeech("ignored") { CapabilityState = state };
        var target = new FakeTarget();
        using var coordinator = new VoiceInteractionCoordinator(capture, speech, target);

        await coordinator.StartAsync(EnabledOptions);

        Assert.Equal(state, coordinator.State);
        Assert.Equal(0, capture.StartCount);
    }

    [Fact]
    public async Task Start_ReportsMicrophoneUnavailableWithoutTranscription()
    {
        using var capture = new FakeCapture { CapabilityState = VoiceCapabilityState.MicrophoneUnavailable };
        using var speech = new FakeSpeech("ignored");
        using var coordinator = new VoiceInteractionCoordinator(capture, speech, new FakeTarget());

        await coordinator.StartAsync(EnabledOptions);

        Assert.Equal(VoiceCapabilityState.MicrophoneUnavailable, coordinator.State);
        Assert.Equal(0, capture.StartCount);
    }

    [Fact]
    public async Task Start_ReportsPermissionDeniedAndCanRetryWithoutLeakingOperation()
    {
        using var capture = new FakeCapture { StartException = new UnauthorizedAccessException() };
        using var speech = new FakeSpeech("ignored");
        using var coordinator = new VoiceInteractionCoordinator(capture, speech, new FakeTarget());

        await coordinator.StartAsync(EnabledOptions);
        await coordinator.StartAsync(EnabledOptions);

        Assert.Equal(VoiceCapabilityState.PermissionDenied, coordinator.State);
        Assert.Equal(2, capture.StartCount);
    }

    [Fact]
    public async Task Cancel_StopsCaptureAndNeverExecutesLateIntent()
    {
        using var capture = new FakeCapture();
        using var speech = new FakeSpeech("go back");
        var target = new FakeTarget();
        using var coordinator = new VoiceInteractionCoordinator(capture, speech, target);

        await coordinator.StartAsync(EnabledOptions);
        await coordinator.CancelAsync();

        Assert.Equal(VoiceCapabilityState.Cancelled, coordinator.State);
        Assert.Equal(1, capture.CancelCount);
        Assert.Empty(target.Executed);
    }

    [Fact]
    public async Task EmptyTranscript_FailsSafelyWithoutAction()
    {
        using var capture = new FakeCapture();
        using var speech = new FakeSpeech("   ");
        var target = new FakeTarget();
        using var coordinator = new VoiceInteractionCoordinator(capture, speech, target);

        await coordinator.StartAsync(EnabledOptions);
        await coordinator.StopAsync(EnabledOptions);

        Assert.Equal(VoiceCapabilityState.Error, coordinator.State);
        Assert.Empty(target.Executed);
    }

    [Fact]
    public async Task Cancel_InterruptsDelayedTranscription()
    {
        using var capture = new FakeCapture();
        using var speech = new FakeSpeech("ignored") { DelayUntilCancellation = true };
        var target = new FakeTarget();
        using var coordinator = new VoiceInteractionCoordinator(capture, speech, target);
        await coordinator.StartAsync(EnabledOptions);

        var stopping = coordinator.StopAsync(EnabledOptions);
        await speech.TranscriptionStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await coordinator.CancelAsync();
        await stopping;

        Assert.Equal(VoiceCapabilityState.Cancelled, coordinator.State);
        Assert.Empty(target.Executed);
    }

    [Fact]
    public async Task ProviderFailure_IsSanitizedAndDoesNotExecuteAction()
    {
        using var capture = new FakeCapture();
        using var speech = new FakeSpeech("ignored") { Failure = new InvalidDataException("private transcript") };
        var target = new FakeTarget();
        using var coordinator = new VoiceInteractionCoordinator(capture, speech, target);
        await coordinator.StartAsync(EnabledOptions);

        await coordinator.StopAsync(EnabledOptions);

        Assert.Equal(VoiceCapabilityState.Error, coordinator.State);
        Assert.Equal(nameof(VoiceCapabilityState.Error), coordinator.Diagnostics.LastErrorCategory);
        Assert.DoesNotContain("private transcript", coordinator.Status, StringComparison.Ordinal);
        Assert.Empty(target.Executed);
    }

    private sealed class FakeCapture : IAudioCaptureService
    {
        public event Action<double>? LevelChanged;

        public VoiceCapabilityState CapabilityState { get; init; } = VoiceCapabilityState.Ready;

        public int StartCount { get; private set; }

        public int CancelCount { get; private set; }

        public int StopCount { get; private set; }

        public double InitialLevel { get; init; } = 0.5;

        public VoiceAudioClip? LastClip { get; private set; }

        public Exception? StartException { get; init; }

        public bool HoldCancellation { get; init; }

        public TaskCompletionSource CancellationStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource ReleaseCancellation { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public VoiceCapability GetCapability() => new(CapabilityState, CapabilityState.ToString(), "Fake microphone");

        public Task StartAsync(VoiceRecognitionOptions options, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            StartCount++;
            if (StartException is not null)
            {
                throw StartException;
            }

            LevelChanged?.Invoke(InitialLevel);
            return Task.CompletedTask;
        }

        public Task<VoiceAudioClip> StopAsync(CancellationToken cancellationToken)
        {
            StopCount++;
            LastClip = new VoiceAudioClip(Enumerable.Repeat((byte)42, 32_000).ToArray(), 16_000, TimeSpan.FromSeconds(1));
            return Task.FromResult(LastClip);
        }

        public void ReportLevel(double level) => LevelChanged?.Invoke(level);

        public async Task CancelAsync()
        {
            CancelCount++;
            CancellationStarted.TrySetResult();
            if (HoldCancellation)
            {
                await ReleaseCancellation.Task;
            }
        }

        public void Dispose()
        {
        }
    }

    private sealed class FakeSpeech(string transcript) : ISpeechRecognitionProvider
    {
        public VoiceCapabilityState CapabilityState { get; init; } = VoiceCapabilityState.Ready;

        public bool DelayUntilCancellation { get; init; }

        public bool DelayCapability { get; init; }

        public bool HoldTranscript { get; init; }

        public TaskCompletionSource ReleaseTranscript { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public Exception? Failure { get; init; }

        public TaskCompletionSource CapabilityStarted { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource ReleaseCapability { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource TranscriptionStarted { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<VoiceCapability> GetCapabilityAsync(
            VoiceRecognitionOptions options,
            CancellationToken cancellationToken)
        {
            CapabilityStarted.TrySetResult();
            if (DelayCapability)
            {
                await ReleaseCapability.Task.WaitAsync(cancellationToken);
            }

            return new VoiceCapability(CapabilityState, CapabilityState.ToString(), "Fake speech", "Configured");
        }

        public async Task<SpeechRecognitionResult> TranscribeAsync(
            VoiceAudioClip clip,
            VoiceRecognitionOptions options,
            CancellationToken cancellationToken)
        {
            TranscriptionStarted.TrySetResult();
            if (Failure is not null)
            {
                throw Failure;
            }

            if (DelayUntilCancellation)
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }

            if (HoldTranscript)
            {
                await ReleaseTranscript.Task;
            }

            return new SpeechRecognitionResult(transcript, null, TimeSpan.FromMilliseconds(2), "Fake speech");
        }

        public void Dispose()
        {
        }
    }

    private sealed class FakeTarget : IVoiceActionTarget
    {
        public long Generation { get; set; } = 1;

        public List<VoiceIntent> Executed { get; } = [];

        public VoiceActionContext CaptureVoiceContext() => new(Generation);

        public bool IsVoiceContextCurrent(VoiceActionContext context) => context.ProviderGeneration == Generation;

        public Task<VoiceActionResult> ExecuteVoiceIntentAsync(
            VoiceIntent intent,
            CancellationToken cancellationToken)
        {
            Executed.Add(intent);
            return Task.FromResult(VoiceActionResult.Completed("Done"));
        }
    }

    private sealed class ManualVoiceClock : TimeProvider
    {
        private long _timestamp;
        private readonly List<ManualTimer> _timers = [];

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp() => _timestamp;

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = new ManualTimer(this, callback, state);
            timer.Change(dueTime, period);
            _timers.Add(timer);
            return timer;
        }

        public void Advance(TimeSpan duration)
        {
            _timestamp += duration.Ticks;
            foreach (var timer in _timers.ToArray())
            {
                timer.FireIfDue();
            }
        }

        private sealed class ManualTimer(ManualVoiceClock clock, TimerCallback callback, object? state) : ITimer
        {
            private long? _deadline;

            public bool Change(TimeSpan dueTime, TimeSpan period)
            {
                _deadline = dueTime == Timeout.InfiniteTimeSpan ? null : clock._timestamp + dueTime.Ticks;
                return true;
            }

            public void FireIfDue()
            {
                if (_deadline is not null && clock._timestamp >= _deadline)
                {
                    _deadline = null;
                    callback(state);
                }
            }

            public void Dispose() => _deadline = null;

            public ValueTask DisposeAsync()
            {
                Dispose();
                return ValueTask.CompletedTask;
            }
        }
    }
}
