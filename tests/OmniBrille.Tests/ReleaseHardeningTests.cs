using System.Buffers.Binary;
using System.Text;

namespace OmniBrille.Tests;

public sealed class ReleaseHardeningTests
{
    [Fact]
    public void WindowsIcon_ContainsCompleteSmallAndLargeSizeSet()
    {
        var bytes = File.ReadAllBytes(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "OmniBrille.Desktop",
            "Assets",
            "OmniBrille.ico"));

        Assert.True(bytes.Length >= 6);
        Assert.Equal((ushort)0, BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(0, 2)));
        Assert.Equal((ushort)1, BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(2, 2)));
        var count = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(4, 2));
        Assert.True(bytes.Length >= 6 + (count * 16));

        var sizes = new HashSet<int>();
        for (var index = 0; index < count; index++)
        {
            var offset = 6 + (index * 16);
            var width = bytes[offset] == 0 ? 256 : bytes[offset];
            var height = bytes[offset + 1] == 0 ? 256 : bytes[offset + 1];
            Assert.Equal(width, height);
            sizes.Add(width);
        }

        foreach (var requiredSize in new[] { 16, 20, 24, 32, 40, 48, 64, 128, 256 })
        {
            Assert.Contains(requiredSize, sizes);
        }
    }

    [Fact]
    public void ReleasePipeline_HasFailClosedSigningAndNonPublishingManualWorkflow()
    {
        var root = FindRepositoryRoot();
        var packageScript = File.ReadAllText(Path.Combine(root, "build", "Package-Windows.ps1"));
        var releaseScript = File.ReadAllText(Path.Combine(root, "build", "verify-release.ps1"));
        var workflow = File.ReadAllText(Path.Combine(root, ".github", "workflows", "private-preview.yml"));

        Assert.Contains("RequireSigning", packageScript, StringComparison.Ordinal);
        Assert.Contains("Set-AuthenticodeSignature", packageScript, StringComparison.Ordinal);
        Assert.Contains("SignatureStatus]::Valid", packageScript, StringComparison.Ordinal);
        Assert.Contains("OMNIBRILLE_SIGNING_CERTIFICATE_THUMBPRINT", packageScript, StringComparison.Ordinal);
        Assert.DoesNotContain("PFX_PASSWORD", packageScript, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("requires a clean checkout", releaseScript, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("workflow_dispatch", workflow, StringComparison.Ordinal);
        Assert.Contains("Signed preview requested, but signing secrets are unavailable", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("gh release", workflow, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("action-gh-release", workflow, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("create tag", workflow, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReleaseMetadata_HasHashManifestAndSanitizedDependencyInventory()
    {
        var root = FindRepositoryRoot();
        var packageScript = File.ReadAllText(Path.Combine(root, "build", "Package-Windows.ps1"));
        var metadataScript = File.ReadAllText(Path.Combine(root, "build", "New-ReleaseArtifacts.ps1"));

        Assert.Contains("New-ReleaseArtifacts.ps1", packageScript, StringComparison.Ordinal);
        Assert.Contains("Get-FileHash", metadataScript, StringComparison.Ordinal);
        Assert.Contains("explorerProtocol", metadataScript, StringComparison.Ordinal);
        Assert.Contains("commitSha", metadataScript, StringComparison.Ordinal);
        Assert.Contains("private-preview-notes.md", metadataScript, StringComparison.Ordinal);
        Assert.Contains("docs\\private-preview.md", metadataScript, StringComparison.Ordinal);
        Assert.Contains("dependency manifest; not a formal SPDX or CycloneDX SBOM", metadataScript, StringComparison.Ordinal);
        Assert.DoesNotContain("AuthorizationToken", metadataScript, StringComparison.Ordinal);
        Assert.DoesNotContain("UserProfile", metadataScript, StringComparison.Ordinal);
    }

    [Fact]
    public void ContinuousIntegration_UsesNode24OrNewerActionsAndAuditsPackages()
    {
        var workflow = File.ReadAllText(Path.Combine(FindRepositoryRoot(), ".github", "workflows", "ci.yml"));

        Assert.Contains("actions/checkout@v7", workflow, StringComparison.Ordinal);
        Assert.Contains("actions/setup-dotnet@v6", workflow, StringComparison.Ordinal);
        Assert.Contains("actions/upload-artifact@v7", workflow, StringComparison.Ordinal);
        Assert.Contains("Test-NuGetVulnerabilities.ps1", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("actions/checkout@v4", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("actions/setup-dotnet@v4", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("actions/upload-artifact@v4", workflow, StringComparison.Ordinal);

        var previewWorkflow = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            ".github",
            "workflows",
            "private-preview.yml"));
        Assert.Contains("actions/download-artifact@v8", previewWorkflow, StringComparison.Ordinal);
        Assert.Contains("Fresh Windows installer lifecycle", previewWorkflow, StringComparison.Ordinal);
        Assert.Contains("retention-days: 90", previewWorkflow, StringComparison.Ordinal);
    }

    [Fact]
    public void TrackedReleaseText_HasNoStaleProductBrand()
    {
        var root = FindRepositoryRoot();
        var staleNames = new[] { string.Concat("Omni", "Explorer"), string.Concat("Omni", "Nav") };
        var extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".cs", ".axaml", ".csproj", ".props", ".sln", ".md", ".ps1", ".yml", ".yaml", ".iss", ".json",
        };
        var matches = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Where(path => extensions.Contains(Path.GetExtension(path)))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}.git{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Select(path => new { Path = path, Text = File.ReadAllText(path, Encoding.UTF8) })
            .SelectMany(file => staleNames
                .Where(name => file.Text.Contains(name, StringComparison.OrdinalIgnoreCase))
                .Select(name => $"{file.Path}: {name}"))
            .ToArray();

        Assert.Empty(matches);
    }

    [Fact]
    public void PrivatePreviewDocumentation_DefinesCompatibilitySecurityAndManualGates()
    {
        var root = FindRepositoryRoot();

        Assert.True(File.Exists(Path.Combine(root, "COMPATIBILITY.md")));
        Assert.True(File.Exists(Path.Combine(root, "CHANGELOG.md")));
        Assert.True(File.Exists(Path.Combine(root, "RELEASE_CHECKLIST.md")));
        Assert.True(File.Exists(Path.Combine(root, "docs", "SECURITY-PRIVACY.md")));
        Assert.True(File.Exists(Path.Combine(root, "docs", "private-preview.md")));
        Assert.True(File.Exists(Path.Combine(root, "docs", "PRIVATE_PREVIEW_FEEDBACK.md")));
        Assert.True(File.Exists(Path.Combine(root, "docs", "PRIVATE_PREVIEW_ROLLOUT.md")));
        Assert.True(File.Exists(Path.Combine(root, "docs", "voice.md")));
        Assert.Contains("0.7.0-preview.1", File.ReadAllText(Path.Combine(root, "COMPATIBILITY.md")), StringComparison.Ordinal);
        Assert.Contains("always-listening mode", File.ReadAllText(Path.Combine(root, "docs", "voice.md")), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("whisper-cli", File.ReadAllText(Path.Combine(root, "docs", "SECURITY-PRIVACY.md")), StringComparison.Ordinal);
        Assert.Contains("- [ ]", File.ReadAllText(Path.Combine(root, "RELEASE_CHECKLIST.md")), StringComparison.Ordinal);
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
