using System.Text.Json;
using OmniBrille.Core;
using OmniBrille.Infrastructure.Voice;

namespace OmniBrille.Tests;

public sealed class WhisperCliSpeechRecognitionProviderTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"OmniBrille-voice-tests-{Guid.NewGuid():N}");

    [Fact]
    public async Task Capability_ReturnsMissingStatesWithoutStartingProcess()
    {
        var runner = new RecordingRunner();
        using var provider = new WhisperCliSpeechRecognitionProvider(
            runner,
            Path.Combine(_root, "temp"),
            Path.Combine(_root, "conventional"));

        var runtimeMissing = await provider.GetCapabilityAsync(new VoiceRecognitionOptions(true), default);
        Directory.CreateDirectory(_root);
        var runtime = Path.Combine(_root, "whisper-cli.exe");
        await File.WriteAllTextAsync(runtime, "runtime");
        var modelMissing = await provider.GetCapabilityAsync(
            new VoiceRecognitionOptions(true, runtime, Path.Combine(_root, "missing.bin")),
            default);

        Assert.Equal(VoiceCapabilityState.RuntimeMissing, runtimeMissing.State);
        Assert.Equal(VoiceCapabilityState.ModelMissing, modelMissing.State);
        Assert.Empty(runner.Calls);
    }

    [Fact]
    public async Task Transcribe_UsesStructuredArgumentsAndDeletesTemporaryAudio()
    {
        Directory.CreateDirectory(_root);
        var runtime = Path.Combine(_root, "runtime with spaces.exe");
        var model = Path.Combine(_root, "model with spaces.bin");
        await File.WriteAllTextAsync(runtime, "runtime");
        await using (var stream = File.Create(model))
        {
            stream.SetLength(1_048_576);
        }

        var temporaryRoot = Path.Combine(_root, "voice-temp");
        var runner = new RecordingRunner { CreateTranscript = true };
        using var provider = new WhisperCliSpeechRecognitionProvider(runner, temporaryRoot, Path.Combine(_root, "none"));
        var options = new VoiceRecognitionOptions(true, runtime, model, "en", 30);

        var capability = await provider.GetCapabilityAsync(options, default);
        var result = await provider.TranscribeAsync(
            new VoiceAudioClip(new byte[64_000], 16_000, TimeSpan.FromSeconds(2)),
            options,
            default);

        Assert.Equal(VoiceCapabilityState.Ready, capability.State);
        Assert.Equal("show me monitoring files", result.Transcript);
        var transcriptionCall = Assert.Single(runner.Calls, call => call.Arguments.Contains("-f"));
        Assert.Equal(runtime, transcriptionCall.ExecutablePath);
        Assert.Contains(model, transcriptionCall.Arguments);
        Assert.Contains("-oj", transcriptionCall.Arguments);
        Assert.Contains("en", transcriptionCall.Arguments);
        Assert.True(transcriptionCall.InputWaveWasValid);
        Assert.True(!Directory.Exists(temporaryRoot) || !Directory.EnumerateFileSystemEntries(temporaryRoot).Any());
    }

    [Fact]
    public async Task Transcribe_DeletesTemporaryAudioAfterMalformedResponse()
    {
        Directory.CreateDirectory(_root);
        var runtime = Path.Combine(_root, "whisper-cli.exe");
        var model = Path.Combine(_root, "model.bin");
        await File.WriteAllTextAsync(runtime, "runtime");
        await using (var stream = File.Create(model))
        {
            stream.SetLength(1_048_576);
        }

        var temporaryRoot = Path.Combine(_root, "voice-temp");
        var runner = new RecordingRunner { CreateTranscript = true, MalformedTranscript = true };
        using var provider = new WhisperCliSpeechRecognitionProvider(runner, temporaryRoot, Path.Combine(_root, "none"));

        await Assert.ThrowsAsync<InvalidDataException>(() => provider.TranscribeAsync(
            new VoiceAudioClip(new byte[32_000], 16_000, TimeSpan.FromSeconds(1)),
            new VoiceRecognitionOptions(true, runtime, model),
            default));

        Assert.True(!Directory.Exists(temporaryRoot) || !Directory.EnumerateFileSystemEntries(temporaryRoot).Any());
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private sealed class RecordingRunner : IVoiceProcessRunner
    {
        public bool CreateTranscript { get; init; }

        public bool MalformedTranscript { get; init; }

        public List<ProcessCall> Calls { get; } = [];

        public async Task<VoiceProcessResult> ExecuteAsync(
            string executablePath,
            IReadOnlyList<string> arguments,
            string workingDirectory,
            int maximumOutputCharacters,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            var inputIndex = arguments.IndexOf("-f");
            var inputPath = inputIndex >= 0 ? arguments[inputIndex + 1] : null;
            var inputWaveWasValid = inputPath is null ||
                File.Exists(inputPath) && (await File.ReadAllBytesAsync(inputPath, cancellationToken))[..4]
                    .SequenceEqual("RIFF"u8.ToArray());
            Calls.Add(new ProcessCall(executablePath, arguments.ToArray(), inputWaveWasValid));
            if (CreateTranscript && arguments.Contains("-of"))
            {
                var outputIndex = arguments.IndexOf("-of");
                var outputPath = arguments[outputIndex + 1] + ".json";
                var payload = MalformedTranscript
                    ? "{\"unexpected\":true}"
                    : JsonSerializer.Serialize(new
                    {
                        transcription = new[]
                        {
                            new { text = " show me monitoring" },
                            new { text = "files " },
                        },
                    });
                await File.WriteAllTextAsync(outputPath, payload, cancellationToken);
            }

            return new VoiceProcessResult(0, string.Empty, string.Empty, false);
        }
    }

    private sealed record ProcessCall(
        string ExecutablePath,
        IReadOnlyList<string> Arguments,
        bool InputWaveWasValid);
}

internal static class VoiceTestListExtensions
{
    public static int IndexOf(this IReadOnlyList<string> values, string value)
    {
        for (var index = 0; index < values.Count; index++)
        {
            if (string.Equals(values[index], value, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }
}
