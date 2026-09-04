using OmniBrille.Core;
using OmniBrille.Infrastructure;

namespace OmniBrille.Tests;

public sealed class ShellFileActivationServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"OmniBrilleFileActivation-{Guid.NewGuid():N}");

    [Fact]
    public async Task OpenAsync_RejectsTargetsOutsideTheGrantedRoot()
    {
        Directory.CreateDirectory(_root);
        var outside = Path.Combine(Path.GetTempPath(), $"outside-{Guid.NewGuid():N}.txt");
        await File.WriteAllTextAsync(outside, "outside");
        try
        {
            var result = await new ShellFileActivationService().OpenAsync(_root, outside);

            Assert.Equal(FileActivationResult.OutsideAccessRoot, result);
        }
        finally
        {
            File.Delete(outside);
        }
    }

    [Theory]
    [InlineData("command.cmd")]
    [InlineData("installer.exe")]
    [InlineData("shortcut.lnk")]
    [InlineData("website.url")]
    [InlineData("control-panel.cpl")]
    [InlineData("management-console.msc")]
    [InlineData("installer-patch.msp")]
    [InlineData("clickonce.application")]
    [InlineData("app-reference.appref-ms")]
    [InlineData("shell-command.scf")]
    public async Task OpenAsync_BlocksHighRiskTypesBeforeInvokingTheShell(string fileName)
    {
        Directory.CreateDirectory(_root);
        var path = Path.Combine(_root, fileName);
        await File.WriteAllTextAsync(path, "not executable");

        var result = await new ShellFileActivationService().OpenAsync(_root, path);

        Assert.Equal(FileActivationResult.HighRiskTypeBlocked, result);
    }

    [Fact]
    public async Task OpenAsync_ReportsMissingOrdinaryFilesWithoutLaunchingAnything()
    {
        Directory.CreateDirectory(_root);

        var result = await new ShellFileActivationService().OpenAsync(_root, Path.Combine(_root, "missing.txt"));

        Assert.Equal(FileActivationResult.NotFound, result);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
