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
            VoiceRuntimePath: Path.Combine(directory.Path, "whisper-cli.exe"),
            VoiceModelPath: Path.Combine(directory.Path, "ggml-base.en.bin"),
            VoiceLanguage: "auto");

        store.Save(expected);
        var actual = store.Load();

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Load_PreVoicePreferenceFileUsesSafeDisabledDefaults()
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
        Assert.False(preferences.VoiceEnabled);
        Assert.Null(preferences.VoiceRuntimePath);
        Assert.Null(preferences.VoiceModelPath);
        Assert.Equal("en", preferences.VoiceLanguage);
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
