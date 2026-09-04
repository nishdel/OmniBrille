using OmniBrille.Infrastructure.Voice;

namespace OmniBrille.Tests;

public sealed class VoiceProcessRunnerTests
{
    [Fact]
    public async Task WindowsProcessRunner_BoundsOutputAndTerminatesTimedOutProcess()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var ping = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "ping.exe");
        Assert.True(File.Exists(ping));
        var runner = new SystemVoiceProcessRunner();
        var workingDirectory = Path.GetDirectoryName(ping)!;

        var bounded = await runner.ExecuteAsync(
            ping,
            ["-n", "1", "127.0.0.1"],
            workingDirectory,
            8,
            TimeSpan.FromSeconds(5),
            CancellationToken.None);

        Assert.Equal(0, bounded.ExitCode);
        Assert.True(bounded.OutputTruncated);
        Assert.True(bounded.StandardOutput.Length <= 8);
        Assert.True(bounded.StandardError.Length <= 8);

        await Assert.ThrowsAsync<TimeoutException>(() => runner.ExecuteAsync(
            ping,
            ["-n", "30", "127.0.0.1"],
            workingDirectory,
            512,
            TimeSpan.FromMilliseconds(50),
            CancellationToken.None));
    }
}
