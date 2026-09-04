namespace OmniBrille.Core;

public sealed record VisualPreferences(
    string Theme = "Dark",
    bool ReducedMotion = false,
    bool ReducedEffects = false,
    bool DiagnosticsVisible = false,
    bool VoiceEnabled = true,
    string VoiceLanguage = "en",
    bool SoundEnabled = true)
{
    public VisualPreferences Normalize() => this with
    {
        Theme = string.Equals(Theme, "Light", StringComparison.OrdinalIgnoreCase) ? "Light" : "Dark",
        VoiceLanguage = string.Equals(VoiceLanguage, "auto", StringComparison.OrdinalIgnoreCase) ? "auto" : "en",
    };

    public VoiceRecognitionOptions ToVoiceOptions() => new(
        VoiceEnabled,
        RuntimePath: null,
        ModelPath: null,
        Language: VoiceLanguage);
}

public interface IVisualPreferencesStore
{
    public VisualPreferences Load();

    public void Save(VisualPreferences preferences);
}
