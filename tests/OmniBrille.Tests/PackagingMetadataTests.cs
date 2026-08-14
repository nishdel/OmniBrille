using System.Xml.Linq;

namespace OmniBrille.Tests;

public sealed class PackagingMetadataTests
{
    [Fact]
    public void CentralVersion_IsPreReleaseAndConsistentWithStageSevenPackage()
    {
        var root = FindRepositoryRoot();
        var document = XDocument.Load(Path.Combine(root, "Directory.Build.props"));

        Assert.Equal("0.6.0", document.Descendants("VersionPrefix").Single().Value);
        Assert.Equal("preview.2", document.Descendants("VersionSuffix").Single().Value);
        Assert.Equal("0.6.0.2", document.Descendants("FileVersion").Single().Value);
        Assert.Equal("0.6.0.0", document.Descendants("AssemblyVersion").Single().Value);
        Assert.Equal("OmniBrille", document.Descendants("Product").Single().Value);
    }

    [Fact]
    public void DesktopExecutable_UsesLocatorCompatibleIdentityAndBrandedAssets()
    {
        var root = FindRepositoryRoot();
        var project = XDocument.Load(Path.Combine(root, "src", "OmniBrille.Desktop", "OmniBrille.Desktop.csproj"));

        Assert.Equal("OmniBrille", project.Descendants("AssemblyName").Single().Value);
        Assert.Equal("Assets\\OmniBrille.ico", project.Descendants("ApplicationIcon").Single().Value);
        Assert.True(File.Exists(Path.Combine(root, "src", "OmniBrille.Desktop", "Assets", "OmniBrille.ico")));
        Assert.True(File.Exists(Path.Combine(root, "src", "OmniBrille.Desktop", "Assets", "OmniBrille.png")));
    }

    [Fact]
    public void Installer_IsCurrentUserScopedAtExistingOmniSorSeLocatorPath()
    {
        var script = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "installer", "OmniBrille.iss"));

        Assert.Contains("DefaultDirName={localappdata}\\Programs\\OmniBrille", script, StringComparison.Ordinal);
        Assert.Contains("PrivilegesRequired=lowest", script, StringComparison.Ordinal);
        Assert.Contains("OmniBrille.exe", script, StringComparison.Ordinal);
        Assert.Contains("{autoprograms}\\OmniBrille", script, StringComparison.Ordinal);
        Assert.Contains("Excludes: \"*.pdb\"", script, StringComparison.Ordinal);
        Assert.Contains("Type: files; Name: \"{app}\\*.pdb\"", script, StringComparison.Ordinal);
        Assert.DoesNotContain("OMNISORSE_OMNIBRILLE_PATH", script, StringComparison.Ordinal);
        Assert.DoesNotContain(string.Concat("Omni", "Explorer"), script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(string.Concat("Omni", "Nav"), script, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PackageScript_UsesReliableAvaloniaDeploymentAndPinnedInstallerTool()
    {
        var root = FindRepositoryRoot();
        var packageScript = File.ReadAllText(Path.Combine(root, "build", "Package-Windows.ps1"));
        var bootstrapScript = File.ReadAllText(Path.Combine(root, "build", "Get-InnoSetup.ps1"));

        Assert.Contains("--self-contained', 'true", packageScript, StringComparison.Ordinal);
        Assert.Contains("PublishSingleFile=false", packageScript, StringComparison.Ordinal);
        Assert.Contains("PublishTrimmed=false", packageScript, StringComparison.Ordinal);
        Assert.Contains("DebugSymbols=false", packageScript, StringComparison.Ordinal);
        Assert.Contains("6.7.3", bootstrapScript, StringComparison.Ordinal);
        Assert.Contains("Get-FileHash", bootstrapScript, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Directory.Build.props")) &&
                Directory.Exists(Path.Combine(directory.FullName, "installer")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the OmniBrille repository root.");
    }
}
