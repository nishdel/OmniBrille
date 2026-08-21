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
    public void ReleasePipeline_HasFailClosedSigningAndNonPublishingCandidateWorkflow()
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
        Assert.Contains("Test-EngineeringDocs.ps1", releaseScript, StringComparison.Ordinal);
        Assert.Contains("workflow_dispatch", workflow, StringComparison.Ordinal);
        Assert.Contains("Signed release candidate requested, but signing secrets are unavailable", workflow, StringComparison.Ordinal);
        Assert.Contains("Release candidate artifact", workflow, StringComparison.Ordinal);
        Assert.Contains("normalCloseAndRelaunch", workflow, StringComparison.Ordinal);
        Assert.Contains("Installer filename or byte length does not match the manifest", workflow, StringComparison.Ordinal);
        Assert.Contains("Required installed distribution notice", workflow, StringComparison.Ordinal);
        Assert.Contains("Manifest signing state does not match", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("gh release", workflow, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("action-gh-release", workflow, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("create tag", workflow, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Packaged distribution notice", releaseScript, StringComparison.Ordinal);
        Assert.Contains("tracked DNG-free SkiaSharp package", releaseScript, StringComparison.Ordinal);
        Assert.Contains("Published libSkiaSharp.dll is not the reviewed project-built DNG-free unsigned binary", releaseScript, StringComparison.Ordinal);
        Assert.DoesNotContain("owner explicitly accepts the separately applicable Adobe DNG SDK agreement", releaseScript, StringComparison.Ordinal);
    }

    [Fact]
    public void DngFreeSkiaBuild_IsPinnedAndFailsClosed()
    {
        var root = FindRepositoryRoot();
        var buildScript = File.ReadAllText(Path.Combine(root, "build", "Build-DngFreeSkia.ps1"));
        var packageScript = File.ReadAllText(Path.Combine(root, "build", "New-DngFreeSkiaPackage.ps1"));
        var packageProject = File.ReadAllText(Path.Combine(root, "build", "native-skia", "OmniBrille.SkiaSharp.NativeAssets.Win32.NoDng.csproj"));
        var workflow = File.ReadAllText(Path.Combine(root, ".github", "workflows", "private-preview.yml"));
        var provenanceGuide = File.ReadAllText(Path.Combine(root, "docs", "native-skia.md"));

        Assert.Contains("f568ac94dd768ef9a2f593537cfde2dd0d348ef5", buildScript, StringComparison.Ordinal);
        Assert.Contains("7dbfc07dd33181f84e0958afb7ee805c6c769f0b", buildScript, StringComparison.Ordinal);
        Assert.Contains("8fecc592a290769242d5098666cee8d29b7f0523", buildScript, StringComparison.Ordinal);
        Assert.Contains("64F27E6A38F1E9C222B6B40D103C60597EF112D08F1F5E6E1A535DA845EF53DD", buildScript, StringComparison.Ordinal);
        Assert.Contains("rollForward = 'disable'", buildScript, StringComparison.Ordinal);
        Assert.Contains("Get-NormalizedTextSha256 $globalJsonPath", buildScript, StringComparison.Ordinal);
        Assert.Contains("--gnArgs=$gnArguments", buildScript, StringComparison.Ordinal);
        Assert.Contains("$gnArguments = 'skia_use_dng_sdk=false'", buildScript, StringComparison.Ordinal);
        Assert.Contains("windows-reproducibility.patch", buildScript, StringComparison.Ordinal);
        Assert.Contains("windows-reproducibility.patch", packageScript, StringComparison.Ordinal);
        Assert.Contains("windows-reproducibility.patch", packageProject, StringComparison.Ordinal);
        Assert.Contains("GN did not append /Brepro to extra_cflags", buildScript, StringComparison.Ordinal);
        Assert.Contains("GN did not append /Brepro to extra_ldflags", buildScript, StringComparison.Ordinal);
        Assert.Contains("/PDBALTPATH:libSkiaSharp.pdb", buildScript, StringComparison.Ordinal);
        Assert.Contains("absolute Windows PDB path", buildScript, StringComparison.Ordinal);
        Assert.Contains("absolutePdbPathInBinary", packageScript, StringComparison.Ordinal);
        Assert.Contains("Invoke-CapturedLine", buildScript, StringComparison.Ordinal);
        Assert.DoesNotContain(")[0].Trim()", buildScript, StringComparison.Ordinal);
        Assert.Contains("DNG or its RAW-only PIEX dependency was fetched", buildScript, StringComparison.Ordinal);
        Assert.Contains("native C export set differs", buildScript, StringComparison.Ordinal);
        Assert.Contains("Expected the project-built native DLL to be NotSigned", buildScript, StringComparison.Ordinal);
        Assert.Contains("A5A4C1EECE528A5BED7C98889435BD8214BBA610F963FE80E35256A91508B5DD", buildScript, StringComparison.Ordinal);
        Assert.Contains("Build-DngFreeSkia.ps1", workflow, StringComparison.Ordinal);
        Assert.Contains("if: always()", workflow, StringComparison.Ordinal);
        Assert.Contains("needs: build-dng-free-skia", workflow, StringComparison.Ordinal);
        Assert.Contains("Installed Skia native binary does not match", workflow, StringComparison.Ordinal);
        Assert.Contains("$manifest.schemaVersion -ne 4", workflow, StringComparison.Ordinal);
        Assert.Contains("OmniBrille-dng-free-skia-3.119.4", workflow, StringComparison.Ordinal);
        Assert.Contains("no upstream source branch or permanent fork", provenanceGuide, StringComparison.Ordinal);
        Assert.Contains("proof-bundle.sha256", provenanceGuide, StringComparison.Ordinal);
    }

    [Fact]
    public void ReleaseMetadata_HasHashManifestAndSanitizedDependencyInventory()
    {
        var root = FindRepositoryRoot();
        var packageScript = File.ReadAllText(Path.Combine(root, "build", "Package-Windows.ps1"));
        var metadataScript = File.ReadAllText(Path.Combine(root, "build", "New-ReleaseArtifacts.ps1"));

        Assert.Contains("New-ReleaseArtifacts.ps1", packageScript, StringComparison.Ordinal);
        Assert.Contains("Get-FileHash", metadataScript, StringComparison.Ordinal);
        Assert.Contains("safe.directory", metadataScript, StringComparison.Ordinal);
        Assert.Contains("explorerProtocol", metadataScript, StringComparison.Ordinal);
        Assert.Contains("schemaVersion = 4", metadataScript, StringComparison.Ordinal);
        Assert.Contains("projectLicenseExpression = 'MIT'", metadataScript, StringComparison.Ordinal);
        Assert.Contains("rather than 'NotSigned'", metadataScript, StringComparison.Ordinal);
        Assert.Contains("distributionNotices", metadataScript, StringComparison.Ordinal);
        Assert.Contains("skiaSharpNotice", metadataScript, StringComparison.Ordinal);
        Assert.Contains("nativeComponents", metadataScript, StringComparison.Ordinal);
        Assert.Contains("dngSdkIncluded = $false", metadataScript, StringComparison.Ordinal);
        Assert.Contains("sourceUrl", metadataScript, StringComparison.Ordinal);
        Assert.Contains("commitSha", metadataScript, StringComparison.Ordinal);
        Assert.Contains("release-notes.md", metadataScript, StringComparison.Ordinal);
        Assert.Contains("docs\\release-notes.md", metadataScript, StringComparison.Ordinal);
        Assert.Contains("not an exact packaged-file inventory", metadataScript, StringComparison.Ordinal);
        Assert.Contains("assetOverrides", metadataScript, StringComparison.Ordinal);
        Assert.Contains("contributesPackagedFiles = $false", metadataScript, StringComparison.Ordinal);
        Assert.Contains("Suppress the transitive official DNG-bearing native runtime", metadataScript, StringComparison.Ordinal);
        Assert.Contains("Runtime dependency manifest does not describe the fail-closed official-native exclusion", File.ReadAllText(
            Path.Combine(root, "build", "verify-release.ps1")), StringComparison.Ordinal);
        Assert.Contains("release-gate status is not recorded", metadataScript, StringComparison.Ordinal);
        Assert.Contains("MIT License", File.ReadAllText(Path.Combine(root, "docs", "release-notes.md")), StringComparison.Ordinal);
        Assert.DoesNotContain("AuthorizationToken", metadataScript, StringComparison.Ordinal);
        Assert.DoesNotContain("UserProfile", metadataScript, StringComparison.Ordinal);
        Assert.Contains("Public release verification requires a maintainer-approved", File.ReadAllText(
            Path.Combine(root, "build", "verify-release.ps1")), StringComparison.Ordinal);
        Assert.Contains("Unsigned candidate must report NotSigned for manifest, installer, and installed application", File.ReadAllText(
            Path.Combine(root, ".github", "workflows", "private-preview.yml")), StringComparison.Ordinal);
    }

    [Fact]
    public void ContinuousIntegration_UsesNode24OrNewerActionsAndAuditsPackages()
    {
        var workflow = File.ReadAllText(Path.Combine(FindRepositoryRoot(), ".github", "workflows", "ci.yml"));

        Assert.Contains("actions/checkout@v7", workflow, StringComparison.Ordinal);
        Assert.Contains("actions/setup-dotnet@v6", workflow, StringComparison.Ordinal);
        Assert.Contains("actions/upload-artifact@v7", workflow, StringComparison.Ordinal);
        Assert.Contains("Test-NuGetVulnerabilities.ps1", workflow, StringComparison.Ordinal);
        Assert.Contains("Test-EngineeringDocs.ps1", workflow, StringComparison.Ordinal);
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
    public void ReleaseDocumentation_DefinesCompatibilitySecurityAndManualGates()
    {
        var root = FindRepositoryRoot();

        Assert.True(File.Exists(Path.Combine(root, "COMPATIBILITY.md")));
        Assert.True(File.Exists(Path.Combine(root, "CHANGELOG.md")));
        Assert.True(File.Exists(Path.Combine(root, "RELEASE_CHECKLIST.md")));
        Assert.True(File.Exists(Path.Combine(root, "docs", "SECURITY-PRIVACY.md")));
        Assert.True(File.Exists(Path.Combine(root, "docs", "release-notes.md")));
        Assert.True(File.Exists(Path.Combine(root, "docs", "PRIVATE_PREVIEW_FEEDBACK.md")));
        Assert.True(File.Exists(Path.Combine(root, "docs", "PRIVATE_PREVIEW_ROLLOUT.md")));
        Assert.True(File.Exists(Path.Combine(root, "docs", "voice.md")));
        Assert.Contains("1.0.0", File.ReadAllText(Path.Combine(root, "COMPATIBILITY.md")), StringComparison.Ordinal);
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
