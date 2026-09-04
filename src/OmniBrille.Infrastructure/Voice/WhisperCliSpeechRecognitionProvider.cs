using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OmniBrille.Core;

namespace OmniBrille.Infrastructure.Voice;

public sealed class WhisperCliSpeechRecognitionProvider : ISpeechRecognitionProvider
{
    private const int MaximumTranscriptCharacters = 2_048;
    private const int MaximumJsonCharacters = 131_072;
    private const int MaximumSegments = 64;
    private const long MaximumStaleCaptureBytes = (45L * 16_000 * sizeof(short)) + 44;
    private const long MaximumStaleJsonBytes = MaximumJsonCharacters * 4L;
    public const string BundledRuntimeSha256 = "95E3C0B0E778AD9499EB0125F97C1DCF437DD9EB4EA77050B043574F93C2631D";
    public const string BundledModelSha256 = "4BAF70DD0D7C4247BA2B81FAFD9C01005AC77C2F9EF064E00DCF195D0E2FDD2F";
    private static readonly IReadOnlyDictionary<string, string> BundledRuntimeFiles =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ggml-base.dll"] = "1482359D921B4C1B183D49DB1D770F9B5E90D86A618B8B648D4845C2471AD6B0",
            ["ggml-cpu-alderlake.dll"] = "D1C5411561361F7CE71FF8455ECF01F666F581B0608FA91A1DFE7D3FD6A25BD1",
            ["ggml-cpu-cannonlake.dll"] = "2EF36F05FA252FF4FDCB8D42EBCE1CEBA4F3D3DE12B93BED15BDEE6237DCCD63",
            ["ggml-cpu-cascadelake.dll"] = "505899AAF3F99C5D714361640F561458EA97F8A09EB0614568A66BEAD2115CB0",
            ["ggml-cpu-haswell.dll"] = "F8CF2F35A06498D783D77FDE42004DD54D2F8236B0D42AC323B94BBA65A603C4",
            ["ggml-cpu-icelake.dll"] = "78AD143EE2E674D037B4840EF33B5748A0659762A26E0AE2B621C4F9451CBDE8",
            ["ggml-cpu-sandybridge.dll"] = "EE47DB7DC40FB30ECA73E62A05306059C2C3C42AECDDF2E8D6AD7E530069B815",
            ["ggml-cpu-skylakex.dll"] = "164E2793897944A43EE071CE6C0B09018088BDF4DD8B14AC0755C58849CF8C50",
            ["ggml-cpu-sse42.dll"] = "7318A9A3B95A85B2453C437B274412BBBAE89E5ECDF5BABB19B99EDC06DED063",
            ["ggml-cpu-x64.dll"] = "AF0F1C2F28FF9E3F472481DD969907BDA85FA39D4FDE17617D4BB0B389301B60",
            ["ggml.dll"] = "894C6237EE7849843213906A2B6A0B371AAA6234048D465F206D910AE846FAFB",
            ["whisper-cli.exe"] = BundledRuntimeSha256,
            ["whisper.dll"] = "792FC523C7AD16E6B9C348E30AD5E5F591165CBCF6A80CA8D0DB02A38CE3EEA2",
        };
    private readonly IVoiceProcessRunner _processRunner;
    private readonly string _temporaryRoot;
    private readonly string _conventionalVoiceRoot;
    private readonly bool _allowConfiguredPaths;
    private readonly bool _enforceBundleHashes;
    private readonly Task _startupCleanupTask;
    private bool _capabilityProbePassed;
    private bool _disposed;

    public WhisperCliSpeechRecognitionProvider(
        IVoiceProcessRunner? processRunner = null,
        string? temporaryRoot = null,
        string? conventionalVoiceRoot = null)
    {
        _processRunner = processRunner ?? new SystemVoiceProcessRunner();
        _temporaryRoot = Path.GetFullPath(temporaryRoot ?? Path.Combine(Path.GetTempPath(), "OmniBrille", "Voice"));
        _allowConfiguredPaths = conventionalVoiceRoot is not null;
        _enforceBundleHashes = conventionalVoiceRoot is null;
        _conventionalVoiceRoot = Path.GetFullPath(conventionalVoiceRoot ?? Path.Combine(
            AppContext.BaseDirectory,
            "Voice"));
        _startupCleanupTask = Task.Run(InitializeTemporaryAuthorityAsync);
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

        try
        {
            await _startupCleanupTask.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or InvalidOperationException or UnauthorizedAccessException)
        {
            return Capability(
                VoiceCapabilityState.Error,
                "A prior local voice workspace could not be cleaned safely. Restart OmniBrille after closing any process using it.");
        }

        var runtime = ResolveRuntime(options.RuntimePath);
        if (runtime is null)
        {
            return Capability(
                VoiceCapabilityState.RuntimeMissing,
                "The pinned local speech runtime is missing or failed integrity validation. Reinstall OmniBrille.");
        }

        var model = ResolveModel(options.ModelPath);
        if (model is null)
        {
            return Capability(
                VoiceCapabilityState.ModelMissing,
                "The pinned local speech model is missing or failed integrity validation. Reinstall OmniBrille.");
        }

        if (_capabilityProbePassed)
        {
            return Capability(
                VoiceCapabilityState.Ready,
                "Voice ready. Audio stays local and is not retained.",
                ModelIdentifier(model));
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

            _capabilityProbePassed = true;
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

        await _startupCleanupTask.WaitAsync(cancellationToken).ConfigureAwait(false);
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
                    "-t", Math.Clamp(Environment.ProcessorCount / 2, 1, 4).ToString(CultureInfo.InvariantCulture),
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
            await DeleteWorkspaceAsync(workspace, throwOnFailure: true).ConfigureAwait(false);
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
        var candidate = _allowConfiguredPaths && configuredPath is not null
            ? configuredPath
            : Path.Combine(_conventionalVoiceRoot, "Runtime", OperatingSystem.IsWindows() ? "whisper-cli.exe" : "whisper-cli");
        var runtime = InspectFile(
            candidate,
            minimumLength: 1,
            requireAbsolute: true,
            expectedSha256: _enforceBundleHashes ? BundledRuntimeSha256 : null);
        return runtime is not null && (!_enforceBundleHashes || ValidateBundledRuntimeDirectory(Path.GetDirectoryName(runtime)!))
            ? runtime
            : null;
    }

    private static bool ValidateBundledRuntimeDirectory(string runtimeDirectory)
    {
        foreach (var file in BundledRuntimeFiles)
        {
            if (InspectFile(
                    Path.Combine(runtimeDirectory, file.Key),
                    minimumLength: 1,
                    requireAbsolute: true,
                    expectedSha256: file.Value) is null)
            {
                return false;
            }
        }

        return true;
    }

    private string? ResolveModel(string? configuredPath)
    {
        if (_allowConfiguredPaths && configuredPath is not null)
        {
            return InspectFile(configuredPath, minimumLength: 1_048_576, requireAbsolute: true);
        }

        foreach (var name in new[] { "ggml-base.en-q5_1.bin", "ggml-base.en.bin", "ggml-tiny.en.bin", "ggml-base.bin", "ggml-tiny.bin" })
        {
            var candidate = InspectFile(
                Path.Combine(_conventionalVoiceRoot, "Models", name),
                1_048_576,
                requireAbsolute: true,
                expectedSha256: _enforceBundleHashes ? BundledModelSha256 : null);
            if (candidate is not null)
            {
                return candidate;
            }
        }

        return null;
    }

    private static string? InspectFile(
        string path,
        long minimumLength,
        bool requireAbsolute,
        string? expectedSha256 = null)
    {
        try
        {
            if (requireAbsolute && !Path.IsPathFullyQualified(path))
            {
                return null;
            }

            var fullPath = Path.GetFullPath(path);
            var info = new FileInfo(fullPath);
            if (!info.Exists || info.Length < minimumLength)
            {
                return null;
            }

            if (expectedSha256 is not null)
            {
                using var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                var actual = Convert.ToHexString(SHA256.HashData(stream));
                if (!string.Equals(actual, expectedSha256, StringComparison.Ordinal))
                {
                    return null;
                }
            }

            return fullPath;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return null;
        }
    }

    private string ModelIdentifier(string path) => _enforceBundleHashes
        ? "Bundled base.en q5_1"
        : $"Configured {Path.GetFileNameWithoutExtension(path)}";

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

    private void EnsureTemporaryAuthority()
    {
        if (OperatingSystem.IsWindows())
        {
            var volumeRoot = Path.GetPathRoot(_temporaryRoot);
            if (string.IsNullOrWhiteSpace(volumeRoot) || volumeRoot.StartsWith("\\\\", StringComparison.Ordinal) ||
                new DriveInfo(volumeRoot).DriveType == DriveType.Network)
            {
                throw new InvalidOperationException("Voice requires a local temporary workspace.");
            }
        }

        Directory.CreateDirectory(_temporaryRoot);
        for (var directory = new DirectoryInfo(_temporaryRoot); directory is not null; directory = directory.Parent)
        {
            if ((directory.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidOperationException("Voice cannot use a reparse-point temporary workspace.");
            }
        }
    }

    private async Task InitializeTemporaryAuthorityAsync()
    {
        EnsureTemporaryAuthority();
        await CleanupStaleWorkspacesAsync().ConfigureAwait(false);
    }

    private async Task CleanupStaleWorkspacesAsync()
    {
        var cutoff = DateTime.UtcNow - TimeSpan.FromHours(1);
        foreach (var directory in Directory.EnumerateDirectories(_temporaryRoot, "utterance-*", SearchOption.TopDirectoryOnly)
                     .Take(16))
        {
            var suffix = Path.GetFileName(directory)["utterance-".Length..];
            if (!Guid.TryParseExact(suffix, "N", out _) || Directory.GetLastWriteTimeUtc(directory) >= cutoff)
            {
                continue;
            }

            await DeleteWorkspaceAsync(directory, throwOnFailure: true).ConfigureAwait(false);
        }
    }

    private async Task DeleteWorkspaceAsync(string workspace, bool throwOnFailure)
    {
        var fullPath = Path.GetFullPath(workspace);
        var root = _temporaryRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(root, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
        {
            if (throwOnFailure)
            {
                throw new InvalidOperationException("Voice cleanup rejected an unexpected workspace path.");
            }

            return;
        }

        Exception? lastFailure = null;
        for (var attempt = 0; attempt < 4; attempt++)
        {
            try
            {
                if (!Directory.Exists(fullPath))
                {
                    return;
                }

                ZeroFileIfPresent(Path.Combine(fullPath, "capture.wav"), MaximumStaleCaptureBytes);
                ZeroFileIfPresent(Path.Combine(fullPath, "transcript.json"), MaximumStaleJsonBytes);
                Directory.Delete(fullPath, recursive: true);
                return;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                lastFailure = exception;
                if (attempt < 3)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(25 * (1 << attempt))).ConfigureAwait(false);
                }
            }
        }

        if (throwOnFailure)
        {
            throw new IOException("Voice could not remove its temporary audio workspace within the cleanup bound.", lastFailure);
        }
    }

    private static void ZeroFileIfPresent(string path, long maximumBytes)
    {
        if (!File.Exists(path))
        {
            return;
        }

        using var stream = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.None);
        if (stream.Length > maximumBytes)
        {
            throw new InvalidDataException("Voice cleanup rejected an oversized stale artifact.");
        }

        Span<byte> zeros = stackalloc byte[4096];
        var remaining = stream.Length;
        while (remaining > 0)
        {
            var write = (int)Math.Min(remaining, zeros.Length);
            stream.Write(zeros[..write]);
            remaining -= write;
        }

        stream.Flush(flushToDisk: true);
    }

    private static string NormalizeText(string? value) => string.IsNullOrWhiteSpace(value)
        ? string.Empty
        : string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

}
