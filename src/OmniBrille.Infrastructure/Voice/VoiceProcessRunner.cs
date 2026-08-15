using System.Diagnostics;
using System.Text;

namespace OmniBrille.Infrastructure.Voice;

public sealed record VoiceProcessResult(
    int ExitCode,
    string StandardOutput,
    string StandardError,
    bool OutputTruncated);

public interface IVoiceProcessRunner
{
    public Task<VoiceProcessResult> ExecuteAsync(
        string executablePath,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        int maximumOutputCharacters,
        TimeSpan timeout,
        CancellationToken cancellationToken);
}

public sealed class SystemVoiceProcessRunner : IVoiceProcessRunner
{
    public async Task<VoiceProcessResult> ExecuteAsync(
        string executablePath,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        int maximumOutputCharacters,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumOutputCharacters);

        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };
        try
        {
            if (!process.Start())
            {
                throw new InvalidOperationException("The local speech process could not be started.");
            }

            var outputTask = ReadBoundedAsync(process.StandardOutput, maximumOutputCharacters);
            var errorTask = ReadBoundedAsync(process.StandardError, maximumOutputCharacters);
            using var timeoutCancellation = new CancellationTokenSource(timeout);
            using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                timeoutCancellation.Token);
            try
            {
                await process.WaitForExitAsync(linkedCancellation.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                Kill(process);
                throw new TimeoutException("The local speech process exceeded its bounded timeout.");
            }
            catch (OperationCanceledException)
            {
                Kill(process);
                throw;
            }

            var output = await outputTask.ConfigureAwait(false);
            var error = await errorTask.ConfigureAwait(false);
            return new VoiceProcessResult(
                process.ExitCode,
                output.Text,
                error.Text,
                output.Truncated || error.Truncated);
        }
        catch (System.ComponentModel.Win32Exception exception)
        {
            throw new InvalidOperationException("The configured local speech runtime could not be started.", exception);
        }
    }

    private static async Task<(string Text, bool Truncated)> ReadBoundedAsync(
        StreamReader reader,
        int maximumCharacters)
    {
        var builder = new StringBuilder(Math.Min(maximumCharacters, 4096));
        var buffer = new char[4096];
        var truncated = false;
        while (true)
        {
            var read = await reader.ReadAsync(buffer).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            var remaining = maximumCharacters - builder.Length;
            if (remaining > 0)
            {
                builder.Append(buffer, 0, Math.Min(remaining, read));
            }

            truncated |= read > remaining;
        }

        return (builder.ToString(), truncated);
    }

    private static void Kill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
        }
    }
}
