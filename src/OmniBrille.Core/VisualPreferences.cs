namespace OmniBrille.Core;

public sealed record VisualPreferences(
    string Theme = "Dark",
    bool ReducedMotion = false,
    bool ReducedEffects = false,
    bool DiagnosticsVisible = false,
    bool VoiceEnabled = false,
    string? VoiceRuntimePath = null,
    string? VoiceModelPath = null,
    string VoiceLanguage = "en")
{
    public VisualPreferences Normalize() => this with
    {
        Theme = string.Equals(Theme, "Light", StringComparison.OrdinalIgnoreCase) ? "Light" : "Dark",
        VoiceRuntimePath = string.IsNullOrWhiteSpace(VoiceRuntimePath) ? null : VoiceRuntimePath.Trim(),
        VoiceModelPath = string.IsNullOrWhiteSpace(VoiceModelPath) ? null : VoiceModelPath.Trim(),
        VoiceLanguage = string.Equals(VoiceLanguage, "auto", StringComparison.OrdinalIgnoreCase) ? "auto" : "en",
    };

    public VoiceRecognitionOptions ToVoiceOptions() => new(
        VoiceEnabled,
        VoiceRuntimePath,
        VoiceModelPath,
        VoiceLanguage);
}

public interface IVisualPreferencesStore
{
    public VisualPreferences Load();

    public void Save(VisualPreferences preferences);
}
