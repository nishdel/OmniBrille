using System.Diagnostics;
using System.Text;
using System.Text.Json;
using OmniBrille.Core;

namespace OmniBrille.Infrastructure.Voice;

public sealed class WhisperCliSpeechRecognitionProvider : ISpeechRecognitionProvider
{
    private const int MaximumTranscriptCharacters = 2_048;
    private const int MaximumJsonCharacters = 131_072;
    private const int MaximumSegments = 64;
    private readonly IVoiceProcessRunner _processRunner;
    private readonly string _temporaryRoot;
    private readonly string _conventionalVoiceRoot;
    private bool _disposed;

    public WhisperCliSpeechRecognitionProvider(
        IVoiceProcessRunner? processRunner = null,
        string? temporaryRoot = null,
        string? conventionalVoiceRoot = null)
    {
        _processRunner = processRunner ?? new SystemVoiceProcessRunner();
        _temporaryRoot = Path.GetFullPath(temporaryRoot ?? Path.Combine(Path.GetTempPath(), "OmniBrille", "Voice"));
        _conventionalVoiceRoot = Path.GetFullPath(conventionalVoiceRoot ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "OmniBrille",
            "Voice"));
    }

    public async Task<VoiceCapability> GetCapabilityAsync(
        VoiceRecognitionOptions options,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        options = options.Normalize();
        if (!options.Enabled)
        {
            return Capability(VoiceCapabilityState.Disabled, "Voice is off.");
        }

        var runtime = ResolveRuntime(options.RuntimePath);
        if (runtime is null)
        {
            return Capability(
                VoiceCapabilityState.RuntimeMissing,
                "A local whisper.cpp runtime is required. Configure whisper-cli in Voice settings.");
        }

        var model = ResolveModel(options.ModelPath);
        if (model is null)
        {
            return Capability(
                VoiceCapabilityState.ModelMissing,
                "A local GGML speech model is required. No model is downloaded automatically.");
        }

        try
        {
            var result = await _processRunner.ExecuteAsync(
                runtime,
                ["--help"],
                Path.GetDirectoryName(runtime)!,
                16_384,
                TimeSpan.FromSeconds(5),
                cancellationToken).ConfigureAwait(false);
            if (result.ExitCode != 0 || result.OutputTruncated)
            {
                return Capability(
                    VoiceCapabilityState.Error,
                    "whisper-cli was found but its bounded capability check failed.",
                    ModelIdentifier(model));
            }

            return Capability(
                VoiceCapabilityState.Ready,
                "Voice ready. Audio stays local and is not retained.",
                ModelIdentifier(model));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException or TimeoutException or UnauthorizedAccessException)
        {
            return Capability(
                VoiceCapabilityState.Error,
                "The configured whisper.cpp runtime could not be validated safely.",
                ModelIdentifier(model));
        }
    }

    public async Task<SpeechRecognitionResult> TranscribeAsync(
        VoiceAudioClip clip,
        VoiceRecognitionOptions options,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(clip);
        options = options.Normalize();
        ValidateClip(clip, options);
        var runtime = ResolveRuntime(options.RuntimePath) ?? throw new InvalidOperationException("The whisper.cpp runtime is unavailable.");
        var model = ResolveModel(options.ModelPath) ?? throw new InvalidOperationException("The GGML speech model is unavailable.");

        Directory.CreateDirectory(_temporaryRoot);
        var workspace = Path.Combine(_temporaryRoot, $"utterance-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workspace);
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var inputPath = Path.Combine(workspace, "capture.wav");
            await WriteWaveAsync(inputPath, clip, cancellationToken).ConfigureAwait(false);
            var outputBase = Path.Combine(workspace, "transcript");
            var result = await _processRunner.ExecuteAsync(
                runtime,
                [
                    "-m", model,
                    "-f", inputPath,
                    "-oj",
                    "-of", outputBase,
                    "-np",
                    "-l", options.Language,
                ],
                workspace,
                32_768,
                TimeSpan.FromSeconds(30),
                cancellationToken).ConfigureAwait(false);
            var jsonPath = outputBase + ".json";
            if (result.ExitCode != 0 || result.OutputTruncated || !File.Exists(jsonPath))
            {
                throw new InvalidDataException("whisper-cli did not produce a bounded transcript response.");
            }

            var json = await ReadBoundedAsync(jsonPath, cancellationToken).ConfigureAwait(false);
            var transcript = ParseTranscript(json);
            stopwatch.Stop();
            return new SpeechRecognitionResult(transcript, null, stopwatch.Elapsed, "whisper.cpp-cli");
        }
        finally
        {
            DeleteWorkspace(workspace);
        }
    }

    public void Dispose() => _disposed = true;

    internal static string ParseTranscript(string json)
    {
        using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 32 });
        if (!document.RootElement.TryGetProperty("transcription", out var transcription) ||
            transcription.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException("The whisper.cpp response did not contain transcription segments.");
        }

        var builder = new StringBuilder();
        foreach (var item in transcription.EnumerateArray().Take(MaximumSegments))
        {
            if (!item.TryGetProperty("text", out var textElement) || textElement.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            var value = NormalizeText(textElement.GetString());
            if (value.Length == 0)
            {
                continue;
            }

            var remaining = MaximumTranscriptCharacters - builder.Length - (builder.Length > 0 ? 1 : 0);
            if (remaining <= 0)
            {
                break;
            }

            if (builder.Length > 0)
            {
                builder.Append(' ');
            }

            builder.Append(value.AsSpan(0, Math.Min(value.Length, remaining)));
        }

        return builder.Length > 0
            ? builder.ToString()
            : throw new InvalidDataException("The whisper.cpp response contained no transcript text.");
    }

    private static VoiceCapability Capability(VoiceCapabilityState state, string message, string model = "Not configured") =>
        new(state, message, "whisper.cpp-cli", model);

    private string? ResolveRuntime(string? configuredPath)
    {
        var candidate = configuredPath ?? Path.Combine(_conventionalVoiceRoot, "Runtime", OperatingSystem.IsWindows() ? "whisper-cli.exe" : "whisper-cli");
        return InspectFile(candidate, minimumLength: 1, requireAbsolute: true);
    }

    private string? ResolveModel(string? configuredPath)
    {
        if (configuredPath is not null)
        {
            return InspectFile(configuredPath, minimumLength: 1_048_576, requireAbsolute: true);
        }

        foreach (var name in new[] { "ggml-base.en.bin", "ggml-tiny.en.bin", "ggml-base.bin", "ggml-tiny.bin" })
        {
            var candidate = InspectFile(Path.Combine(_conventionalVoiceRoot, "Models", name), 1_048_576, requireAbsolute: true);
            if (candidate is not null)
            {
                return candidate;
            }
        }

        return null;
    }

    private static string? InspectFile(string path, long minimumLength, bool requireAbsolute)
    {
        try
        {
            if (requireAbsolute && !Path.IsPathFullyQualified(path))
            {
                return null;
            }

            var fullPath = Path.GetFullPath(path);
            return File.Exists(fullPath) && new FileInfo(fullPath).Length >= minimumLength ? fullPath : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return null;
        }
    }

    private static string ModelIdentifier(string path) => $"Configured {Path.GetFileNameWithoutExtension(path)}";

    private static void ValidateClip(VoiceAudioClip clip, VoiceRecognitionOptions options)
    {
        if (clip.SampleRate is < 8_000 or > 48_000 || clip.Pcm16Mono.Length % VoiceAudioClip.BytesPerSample != 0)
        {
            throw new InvalidDataException("Voice audio format is invalid.");
        }

        var maximumBytes = options.MaximumUtteranceSeconds * clip.SampleRate * VoiceAudioClip.BytesPerSample;
        if (clip.IsEmpty || clip.Pcm16Mono.Length > maximumBytes)
        {
            throw new InvalidDataException("Voice audio exceeded its bounded duration or was empty.");
        }
    }

    private static async Task WriteWaveAsync(
        string path,
        VoiceAudioClip clip,
        CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            path,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            16_384,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        using var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true);
        writer.Write(Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(36 + clip.Pcm16Mono.Length);
        writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)1);
        writer.Write(clip.SampleRate);
        writer.Write(clip.SampleRate * VoiceAudioClip.BytesPerSample);
        writer.Write((short)VoiceAudioClip.BytesPerSample);
        writer.Write((short)16);
        writer.Write(Encoding.ASCII.GetBytes("data"));
        writer.Write(clip.Pcm16Mono.Length);
        writer.Flush();
        await stream.WriteAsync(clip.Pcm16Mono, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<string> ReadBoundedAsync(string path, CancellationToken cancellationToken)
    {
        var info = new FileInfo(path);
        if (info.Length > MaximumJsonCharacters * 4L)
        {
            throw new InvalidDataException("The whisper.cpp response exceeded its bounded size.");
        }

        var json = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
        return json.Length <= MaximumJsonCharacters
            ? json
            : throw new InvalidDataException("The whisper.cpp response exceeded its bounded size.");
    }

    private void DeleteWorkspace(string workspace)
    {
        try
        {
            var fullPath = Path.GetFullPath(workspace);
            var root = _temporaryRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (fullPath.StartsWith(root, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal) &&
                Directory.Exists(fullPath))
            {
                Directory.Delete(fullPath, recursive: true);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
        }
    }

    private static string NormalizeText(string? value) => string.IsNullOrWhiteSpace(value)
        ? string.Empty
        : string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
