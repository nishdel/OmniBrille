using OmniBrille.Desktop.Support;

namespace OmniBrille.Tests;

public sealed class SanitizedDiagnosticsReportTests
{
    [Fact]
    public void Create_ReducesUnexpectedTransportAndFailureToSafeCategories()
    {
        var report = SanitizedDiagnosticsReport.Create(new SanitizedDiagnosticsSnapshot(
            Version: "0.6.0-preview.3",
            OperatingSystem: "Windows",
            Framework: ".NET 8",
            RuntimeIdentifier: "win-x64",
            Provider: "Connected",
            ConnectionState: "Error",
            ViewMode: "Context",
            ProtocolVersion: "1.0",
            Transport: "private-endpoint-value",
            Capabilities: "Structure, Context",
            ContextAvailable: true,
            ReducedMotion: false,
            ReducedEffects: false,
            VoiceState: "Ready",
            VoiceProvider: "whisper.cpp-cli",
            VoiceModelState: "Configured ggml-base.en",
            VoiceInitializationDuration: TimeSpan.FromMilliseconds(10),
            VoiceCaptureDuration: TimeSpan.FromSeconds(2),
            VoiceTranscriptionDuration: TimeSpan.FromMilliseconds(300),
            VoiceExecutionDuration: TimeSpan.FromMilliseconds(20),
            VoiceTranscriptLength: 24,
            VoiceClassification: "Search",
            VoiceErrorCategory: "C:\\private\\voice-model.bin",
            Nodes: 3,
            NodeBudget: 48,
            Edges: 2,
            Labels: 3,
            Zoom: 1,
            LayoutDuration: TimeSpan.FromMilliseconds(1),
            PreparationDuration: TimeSpan.FromMilliseconds(2),
            RenderDuration: TimeSpan.FromMilliseconds(3),
            LoadDuration: TimeSpan.FromMilliseconds(4),
            RequestDuration: TimeSpan.FromMilliseconds(5),
            TimeoutCount: 0,
            ReconnectCount: 0,
            StaleResponseCount: 0,
            FailureCategory: "C:\\private\\path",
            RenderAllocatedBytes: 1024,
            TextCacheEntries: 1,
            ResourceCacheEntries: 2,
            DataRainTokens: 0,
            DataRainDuration: TimeSpan.Zero));

        Assert.Contains("Transport: other-local", report, StringComparison.Ordinal);
        Assert.Contains("Last failure category: Other", report, StringComparison.Ordinal);
        Assert.DoesNotContain("private-endpoint-value", report, StringComparison.Ordinal);
        Assert.DoesNotContain("C:\\private\\path", report, StringComparison.Ordinal);
        Assert.DoesNotContain("C:\\private\\voice-model.bin", report, StringComparison.Ordinal);
        Assert.Contains("Voice result: Search, 24 characters, error Other", report, StringComparison.Ordinal);
    }
}
