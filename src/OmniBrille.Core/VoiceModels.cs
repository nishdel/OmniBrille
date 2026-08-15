namespace OmniBrille.Core;

public enum VoiceCapabilityState
{
    Disabled,
    RuntimeMissing,
    ModelMissing,
    Loading,
    Ready,
    Listening,
    Transcribing,
    Executing,
    Cancelled,
    PermissionDenied,
    MicrophoneUnavailable,
    Error,
}

public enum VoiceIntentKind
{
    Search,
    GoBack,
    OpenVisibleNode,
    FocusVisibleNode,
    ZoomIn,
    ZoomOut,
    ResetView,
    SwitchToStructure,
    SwitchToContext,
    ShowRelatedToFocus,
    UseDarkTheme,
    UseLightTheme,
    OpenDetails,
    CloseDetails,
    ShowAccessibleList,
    HideAccessibleList,
    ClearSearch,
    Cancel,
}

public sealed record VoiceRecognitionOptions(
    bool Enabled = false,
    string? RuntimePath = null,
    string? ModelPath = null,
    string Language = "en",
    int MaximumUtteranceSeconds = 45)
{
    public VoiceRecognitionOptions Normalize() => this with
    {
        RuntimePath = NormalizeOptionalPath(RuntimePath),
        ModelPath = NormalizeOptionalPath(ModelPath),
        Language = string.Equals(Language, "auto", StringComparison.OrdinalIgnoreCase) ? "auto" : "en",
        MaximumUtteranceSeconds = Math.Clamp(MaximumUtteranceSeconds, 5, 60),
    };

    private static string? NormalizeOptionalPath(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed record VoiceCapability(
    VoiceCapabilityState State,
    string Message,
    string Provider,
    string ModelIdentifier = "Not configured")
{
    public bool IsReady => State == VoiceCapabilityState.Ready;
}

public sealed record VoiceAudioClip(
    byte[] Pcm16Mono,
    int SampleRate,
    TimeSpan Duration)
{
    public const int BytesPerSample = 2;

    public bool IsEmpty => Pcm16Mono.Length == 0 || Duration <= TimeSpan.Zero;
}

public sealed record SpeechRecognitionResult(
    string Transcript,
    double? Confidence,
    TimeSpan Duration,
    string Provider);

public sealed record VoiceIntent(VoiceIntentKind Kind, string? Argument = null);

public sealed record VoiceActionContext(long ProviderGeneration);

public sealed record VoiceActionResult(bool Succeeded, string Message)
{
    public static VoiceActionResult Completed(string message) => new(true, message);

    public static VoiceActionResult Rejected(string message) => new(false, message);
}

public sealed record VoiceDiagnostics(
    VoiceCapabilityState State,
    string Provider,
    string ModelState,
    TimeSpan InitializationDuration,
    TimeSpan CaptureDuration,
    TimeSpan TranscriptionDuration,
    TimeSpan ExecutionDuration,
    int TranscriptLength,
    string Classification,
    string? LastErrorCategory)
{
    public static VoiceDiagnostics Empty { get; } = new(
        VoiceCapabilityState.Disabled,
        "None",
        "Not configured",
        TimeSpan.Zero,
        TimeSpan.Zero,
        TimeSpan.Zero,
        TimeSpan.Zero,
        0,
        "None",
        null);
}

public interface IAudioCaptureService : IDisposable
{
    public event Action<double>? LevelChanged;

    public VoiceCapability GetCapability();

    public Task StartAsync(VoiceRecognitionOptions options, CancellationToken cancellationToken);

    public Task<VoiceAudioClip> StopAsync(CancellationToken cancellationToken);

    public Task CancelAsync();
}

public interface ISpeechRecognitionProvider : IDisposable
{
    public Task<VoiceCapability> GetCapabilityAsync(
        VoiceRecognitionOptions options,
        CancellationToken cancellationToken);

    public Task<SpeechRecognitionResult> TranscribeAsync(
        VoiceAudioClip clip,
        VoiceRecognitionOptions options,
        CancellationToken cancellationToken);
}

public interface IVoiceActionTarget
{
    public VoiceActionContext CaptureVoiceContext();

    public bool IsVoiceContextCurrent(VoiceActionContext context);

    public Task<VoiceActionResult> ExecuteVoiceIntentAsync(
        VoiceIntent intent,
        CancellationToken cancellationToken);
}
