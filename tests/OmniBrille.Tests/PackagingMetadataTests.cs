using System.Xml.Linq;

namespace OmniBrille.Tests;

public sealed class PackagingMetadataTests
{
    [Fact]
    public void WindowsNativeSkia_HasExpectedManagedAbiVersion()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        Assert.Equal(new Version(119, 0), SkiaSharp.SkiaSharpVersion.Native);
        Assert.True(SkiaSharp.SkiaSharpVersion.CheckNativeLibraryCompatible(false));
    }

    [Fact]
    public void CentralVersion_IsStableAndConsistentWithFirstPublicRelease()
    {
        var root = FindRepositoryRoot();
        var document = XDocument.Load(Path.Combine(root, "Directory.Build.props"));

        Assert.Equal("1.0.0", document.Descendants("VersionPrefix").Single().Value);
        Assert.Equal(string.Empty, document.Descendants("VersionSuffix").Single().Value);
        Assert.Equal("1.0.0.0", document.Descendants("FileVersion").Single().Value);
        Assert.Equal("1.0.0.0", document.Descendants("AssemblyVersion").Single().Value);
        Assert.Equal("OmniBrille", document.Descendants("Product").Single().Value);
        Assert.Equal("MIT", document.Descendants("PackageLicenseExpression").Single().Value);
        var license = File.ReadAllText(Path.Combine(root, "LICENSE"));
        Assert.Contains("MIT License", license, StringComparison.Ordinal);
        Assert.Contains("Copyright (c) 2026 OmniBrille Contributors", license, StringComparison.Ordinal);
        Assert.Contains("Permission is hereby granted, free of charge", license, StringComparison.Ordinal);
        Assert.Contains("THE SOFTWARE IS PROVIDED \"AS IS\"", license, StringComparison.Ordinal);
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
        Assert.Contains("Local-first spatial file explorer", script, StringComparison.Ordinal);
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
        Assert.Contains("Windows packaging supports only", packageScript, StringComparison.Ordinal);
        Assert.Contains("6.7.3", bootstrapScript, StringComparison.Ordinal);
        Assert.Contains("Get-FileHash", bootstrapScript, StringComparison.Ordinal);
    }

    [Fact]
    public void RedistributedDependencies_HavePackagedNoticesWithoutBundledVoiceModel()
    {
        var root = FindRepositoryRoot();
        var infrastructure = XDocument.Load(Path.Combine(
            root,
            "src",
            "OmniBrille.Infrastructure",
            "OmniBrille.Infrastructure.csproj"));
        var desktop = File.ReadAllText(Path.Combine(
            root,
            "src",
            "OmniBrille.Desktop",
            "OmniBrille.Desktop.csproj"));
        var notice = File.ReadAllText(Path.Combine(root, "THIRD-PARTY-NOTICES.txt"));

        Assert.Equal("2.3.0", infrastructure.Descendants("PackageReference")
            .Single(reference => (string?)reference.Attribute("Include") == "NAudio.WinMM")
            .Attribute("Version")!.Value);
        Assert.Contains("THIRD-PARTY-NOTICES.txt", desktop, StringComparison.Ordinal);
        Assert.Contains("LICENSE", desktop, StringComparison.Ordinal);
        Assert.Contains("THIRD-PARTY-LICENSES", desktop, StringComparison.Ordinal);
        Assert.Contains("SkiaSharp.NativeAssets.Win32\" Version=\"3.119.4\" ExcludeAssets=\"all", desktop, StringComparison.Ordinal);
        Assert.Contains("OmniBrille.SkiaSharp.NativeAssets.Win32.NoDng\" Version=\"3.119.4.3", desktop, StringComparison.Ordinal);
        Assert.Contains("Copyright 2020 Mark Heath", notice, StringComparison.Ordinal);
        Assert.Contains("Avalonia 12.1.1", notice, StringComparison.Ordinal);
        Assert.Contains("Inter-OFL-1.1.txt", notice, StringComparison.Ordinal);
        Assert.Contains("SkiaSharp-HarfBuzz-THIRD-PARTY-NOTICES.txt", notice, StringComparison.Ordinal);
        Assert.Contains("unused DNG/RAW codec excluded", notice, StringComparison.Ordinal);
        Assert.DoesNotContain("DNG SDK License Agreement", notice, StringComparison.Ordinal);
        Assert.Contains("DotNet-Runtime-THIRD-PARTY-NOTICES.txt", notice, StringComparison.Ordinal);
        Assert.Contains("Tmds.DBus.Protocol 0.94.1", notice, StringComparison.Ordinal);
        Assert.Contains("licensed under the MIT License", notice, StringComparison.Ordinal);
        var skiaNotices = File.ReadAllText(Path.Combine(
            root,
            "THIRD-PARTY-LICENSES",
            "SkiaSharp-HarfBuzz-THIRD-PARTY-NOTICES.txt"));
        Assert.Contains("OMNIBRILLE DNG-FREE SKIASHARP 3.119.4 NOTICE", skiaNotices, StringComparison.Ordinal);
        Assert.Contains("All other upstream notice sections are retained conservatively", skiaNotices, StringComparison.Ordinal);
        Assert.DoesNotContain("DNG SDK License Agreement", skiaNotices, StringComparison.Ordinal);
        Assert.DoesNotContain("# piex", skiaNotices, StringComparison.OrdinalIgnoreCase);
        var nativePackage = Path.Combine(
            root,
            "packages",
            "OmniBrille.SkiaSharp.NativeAssets.Win32.NoDng.3.119.4.3.nupkg");
        Assert.True(File.Exists(nativePackage));
        Assert.Equal(
            "8B3FED3C96DA7A94F24849490D0A7B9B8DCC2BB520C09067CA0F0F1635264D92",
            Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(nativePackage))));
        Assert.True(File.Exists(Path.Combine(root, "THIRD-PARTY-LICENSES", "ANGLE-LICENSE.txt")));
        Assert.True(File.Exists(Path.Combine(root, "THIRD-PARTY-LICENSES", "Inter-OFL-1.1.txt")));
        Assert.True(File.Exists(Path.Combine(root, "THIRD-PARTY-LICENSES", "Tmds.DBus-LICENSE.txt")));
        Assert.Contains("whisper.cpp and GGML speech models are not included", notice, StringComparison.Ordinal);
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
