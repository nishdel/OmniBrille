namespace OmniBrille.Core;

public sealed record VisualPreferences(
    string Theme = "Dark",
    bool ReducedMotion = false,
    bool ReducedEffects = false,
    bool DiagnosticsVisible = false)
{
    public VisualPreferences Normalize() => this with
    {
        Theme = string.Equals(Theme, "Light", StringComparison.OrdinalIgnoreCase) ? "Light" : "Dark",
    };
}

public interface IVisualPreferencesStore
{
    public VisualPreferences Load();

    public void Save(VisualPreferences preferences);
}
