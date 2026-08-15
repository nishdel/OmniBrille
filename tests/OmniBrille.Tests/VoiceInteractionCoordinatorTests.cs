using OmniBrille.Core;

namespace OmniBrille.Tests;

public sealed class VoiceInteractionCoordinatorTests
{
    private static readonly VoiceRecognitionOptions EnabledOptions = new(true);

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

        public Exception? StartException { get; init; }

        public VoiceCapability GetCapability() => new(CapabilityState, CapabilityState.ToString(), "Fake microphone");

        public Task StartAsync(VoiceRecognitionOptions options, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            StartCount++;
            if (StartException is not null)
            {
                throw StartException;
            }

            LevelChanged?.Invoke(0.5);
            return Task.CompletedTask;
        }

        public Task<VoiceAudioClip> StopAsync(CancellationToken cancellationToken) => Task.FromResult(
            new VoiceAudioClip(new byte[32_000], 16_000, TimeSpan.FromSeconds(1)));

        public Task CancelAsync()
        {
            CancelCount++;
            return Task.CompletedTask;
        }

        public void Dispose()
        {
        }
    }

    private sealed class FakeSpeech(string transcript) : ISpeechRecognitionProvider
    {
        public VoiceCapabilityState CapabilityState { get; init; } = VoiceCapabilityState.Ready;

        public bool DelayUntilCancellation { get; init; }

        public Exception? Failure { get; init; }

        public TaskCompletionSource TranscriptionStarted { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<VoiceCapability> GetCapabilityAsync(
            VoiceRecognitionOptions options,
            CancellationToken cancellationToken) => Task.FromResult(
                new VoiceCapability(CapabilityState, CapabilityState.ToString(), "Fake speech", "Configured"));

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
}
