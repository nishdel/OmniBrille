using System.Text.Json;
using OmniBrille.Core;

namespace OmniBrille.Infrastructure;

public sealed class JsonVisualPreferencesStore : IVisualPreferencesStore
{
    private const string SettingsFileName = "visual-preferences.json";
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    private readonly string _settingsPath;

    public JsonVisualPreferencesStore(string? settingsRoot = null)
    {
        var root = settingsRoot ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "OmniBrille");
        _settingsPath = Path.Combine(root, SettingsFileName);
    }

    public VisualPreferences Load()
    {
        try
        {
            if (!File.Exists(_settingsPath))
            {
                return new VisualPreferences();
            }

            var json = File.ReadAllText(_settingsPath);
            return (JsonSerializer.Deserialize<VisualPreferences>(json) ?? new VisualPreferences()).Normalize();
        }
        catch (Exception exception) when (IsRecoverable(exception))
        {
            return new VisualPreferences();
        }
    }

    public void Save(VisualPreferences preferences)
    {
        ArgumentNullException.ThrowIfNull(preferences);

        try
        {
            var directory = Path.GetDirectoryName(_settingsPath)!;
            Directory.CreateDirectory(directory);
            var temporaryPath = _settingsPath + ".tmp";
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(preferences.Normalize(), SerializerOptions));
            File.Move(temporaryPath, _settingsPath, overwrite: true);
        }
        catch (Exception exception) when (IsRecoverable(exception))
        {
            // Preferences are a convenience. A read-only profile must not stop navigation.
        }
    }

    private static bool IsRecoverable(Exception exception) => exception is
        IOException or
        UnauthorizedAccessException or
        System.Security.SecurityException or
        JsonException or
        NotSupportedException;
}
