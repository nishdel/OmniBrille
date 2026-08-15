namespace OmniBrille.Desktop.Support;

public sealed record SanitizedDiagnosticsSnapshot(
    string Version,
    string OperatingSystem,
    string Framework,
    string RuntimeIdentifier,
    string Provider,
    string ConnectionState,
    string ViewMode,
    string ProtocolVersion,
    string Transport,
    string Capabilities,
    bool ContextAvailable,
    bool ReducedMotion,
    bool ReducedEffects,
    string VoiceState,
    string VoiceProvider,
    string VoiceModelState,
    TimeSpan VoiceInitializationDuration,
    TimeSpan VoiceCaptureDuration,
    TimeSpan VoiceTranscriptionDuration,
    TimeSpan VoiceExecutionDuration,
    int VoiceTranscriptLength,
    string VoiceClassification,
    string? VoiceErrorCategory,
    int Nodes,
    int NodeBudget,
    int Edges,
    int Labels,
    double Zoom,
    TimeSpan LayoutDuration,
    TimeSpan PreparationDuration,
    TimeSpan RenderDuration,
    TimeSpan LoadDuration,
    TimeSpan RequestDuration,
    int TimeoutCount,
    int ReconnectCount,
    int StaleResponseCount,
    string? FailureCategory,
    long RenderAllocatedBytes,
    int TextCacheEntries,
    int ResourceCacheEntries,
    int DataRainTokens,
    TimeSpan DataRainDuration);

public static class SanitizedDiagnosticsReport
{
    private static readonly HashSet<string> SafeFailureCategories = new(StringComparer.Ordinal)
    {
        "EndOfStreamException",
        "ExplorerProtocolException",
        "ExplorerProtocolMalformedResponseException",
        "ExplorerProtocolTimeoutException",
        "IOException",
        "InvalidDataException",
        "No current session grant",
        "Session expired",
        "Timeout",
        "TimeoutException",
        "UnauthorizedAccessException",
    };

    public static string Create(SanitizedDiagnosticsSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var transport = string.Equals(snapshot.Transport, "named-pipe", StringComparison.OrdinalIgnoreCase)
            ? "named-pipe"
            : string.IsNullOrWhiteSpace(snapshot.Transport) ? "none" : "other-local";
        var failure = snapshot.FailureCategory is not null && SafeFailureCategories.Contains(snapshot.FailureCategory)
            ? snapshot.FailureCategory
            : snapshot.FailureCategory is null ? "None" : "Other";

        return string.Join(
            Environment.NewLine,
            [
                "OmniBrille safe diagnostics",
                $"Version: {snapshot.Version}",
                $"OS: {snapshot.OperatingSystem}",
                $"Runtime: {snapshot.Framework} ({snapshot.RuntimeIdentifier})",
                $"Provider: {snapshot.Provider}",
                $"Connection: {snapshot.ConnectionState}",
                $"View: {snapshot.ViewMode}",
                $"Protocol: {snapshot.ProtocolVersion}",
                $"Transport: {transport}",
                $"Capabilities: {snapshot.Capabilities}",
                $"Context available: {snapshot.ContextAvailable}",
                $"Reduced motion/effects: {snapshot.ReducedMotion}/{snapshot.ReducedEffects}",
                $"Voice: {snapshot.VoiceState}, provider {SafeVoiceProvider(snapshot.VoiceProvider)}, model {SafeVoiceModelState(snapshot.VoiceModelState)}",
                FormattableString.Invariant($"Voice timings ms: initialize {snapshot.VoiceInitializationDuration.TotalMilliseconds:0.0}, capture {snapshot.VoiceCaptureDuration.TotalMilliseconds:0.0}, transcribe {snapshot.VoiceTranscriptionDuration.TotalMilliseconds:0.0}, execute {snapshot.VoiceExecutionDuration.TotalMilliseconds:0.0}"),
                $"Voice result: {snapshot.VoiceClassification}, {snapshot.VoiceTranscriptLength} characters, error {SafeVoiceCategory(snapshot.VoiceErrorCategory)}",
                FormattableString.Invariant($"Scene: {snapshot.Nodes}/{snapshot.NodeBudget} nodes, {snapshot.Edges} edges, {snapshot.Labels} labels, zoom {snapshot.Zoom:0.00}"),
                FormattableString.Invariant($"Timings ms: layout {snapshot.LayoutDuration.TotalMilliseconds:0.00}, prepare {snapshot.PreparationDuration.TotalMilliseconds:0.00}, render {snapshot.RenderDuration.TotalMilliseconds:0.00}, load {snapshot.LoadDuration.TotalMilliseconds:0.0}, IPC {snapshot.RequestDuration.TotalMilliseconds:0.0}"),
                $"Counters: timeouts {snapshot.TimeoutCount}, reconnects {snapshot.ReconnectCount}, stale responses {snapshot.StaleResponseCount}",
                $"Last failure category: {failure}",
                FormattableString.Invariant($"Renderer: {snapshot.RenderAllocatedBytes / 1024d:0.0} KiB allocated, text cache {snapshot.TextCacheEntries}/256, resource cache {snapshot.ResourceCacheEntries}/576"),
                FormattableString.Invariant($"Data rain: {snapshot.DataRainTokens} tokens, {snapshot.DataRainDuration.TotalMilliseconds:0.00} ms"),
                "Privacy: excludes paths, filenames, queries, content, endpoints, grants, tokens, and session/node identifiers.",
            ]);
    }

    private static string SafeVoiceCategory(string? category)
    {
        if (category is null)
        {
            return "None";
        }

        return Enum.TryParse<OmniBrille.Core.VoiceCapabilityState>(category, out _) ||
            string.Equals(category, "ActionRejected", StringComparison.Ordinal)
                ? category
                : "Other";
    }

    private static string SafeVoiceProvider(string provider) => provider switch
    {
        "None" => "None",
        "Windows microphone" => "Windows microphone",
        "whisper.cpp-cli" => "whisper.cpp-cli",
        _ => "Other",
    };

    private static string SafeVoiceModelState(string modelState) =>
        modelState.StartsWith("Configured ", StringComparison.Ordinal) ? "Configured" : "Not configured";
}
