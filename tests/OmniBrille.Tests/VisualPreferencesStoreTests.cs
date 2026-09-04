using OmniBrille.Core;
using OmniBrille.Infrastructure;

namespace OmniBrille.Tests;

public sealed class VisualPreferencesStoreTests
{
    [Fact]
    public void SaveAndLoad_RoundTripsReducedSettingsAndTheme()
    {
        using var directory = new TemporaryDirectory();
        var store = new JsonVisualPreferencesStore(directory.Path);
        var expected = new VisualPreferences(
            "Light",
            ReducedMotion: true,
            ReducedEffects: true,
            DiagnosticsVisible: true,
            VoiceEnabled: true,
            VoiceLanguage: "auto",
            SoundEnabled: false);

        store.Save(expected);
        var actual = store.Load();

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Load_PreVoicePreferenceFileUsesInstallerOwnedVoiceAndSoundDefaults()
    {
        using var directory = new TemporaryDirectory();
        File.WriteAllText(
            Path.Combine(directory.Path, "visual-preferences.json"),
            """
            {
              "Theme": "Light",
              "ReducedMotion": true,
              "ReducedEffects": false,
              "DiagnosticsVisible": false
            }
            """);

        var preferences = new JsonVisualPreferencesStore(directory.Path).Load();

        Assert.Equal("Light", preferences.Theme);
        Assert.True(preferences.ReducedMotion);
        Assert.True(preferences.VoiceEnabled);
        Assert.Equal("en", preferences.VoiceLanguage);
        Assert.True(preferences.SoundEnabled);
    }

    [Fact]
    public void Load_LegacyVoiceOverridesAreRemovedWithoutRetainingTheirValues()
    {
        using var directory = new TemporaryDirectory();
        var settingsPath = Path.Combine(directory.Path, "visual-preferences.json");
        File.WriteAllText(
            settingsPath,
            """
            {
              "Theme": "Light",
              "VoiceEnabled": true,
              "VoiceRuntimePath": "C:\\private\\whisper-cli.exe",
              "VoiceModelPath": "C:\\private\\model.bin",
              "VoiceLanguage": "auto"
            }
            """);

        var preferences = new JsonVisualPreferencesStore(directory.Path).Load();
        var migratedJson = File.ReadAllText(settingsPath);

        Assert.Equal("Light", preferences.Theme);
        Assert.Equal("auto", preferences.VoiceLanguage);
        Assert.DoesNotContain("VoiceRuntimePath", migratedJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("VoiceModelPath", migratedJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("private", migratedJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Load_MalformedFileFallsBackToSafeDefaults()
    {
        using var directory = new TemporaryDirectory();
        File.WriteAllText(Path.Combine(directory.Path, "visual-preferences.json"), "not-json");

        var preferences = new JsonVisualPreferencesStore(directory.Path).Load();

        Assert.Equal(new VisualPreferences(), preferences);
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"OmniBrillePreferenceTests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
