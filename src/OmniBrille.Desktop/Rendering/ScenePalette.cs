using Avalonia.Media;

namespace OmniBrille.Desktop.Rendering;

internal sealed record ScenePalette(
    Color Background,
    Color BackgroundPoint,
    Color Edge,
    Color EdgeGlow,
    Color Node,
    Color Context,
    Color Focus,
    Color Search,
    Color Text,
    Color MutedText,
    Color Selection)
{
    public static ScenePalette Dark { get; } = new(
        Color.Parse("#030B1A"),
        Color.Parse("#214E78"),
        Color.Parse("#268FD2"),
        Color.Parse("#30C8FF"),
        Color.Parse("#75CFFF"),
        Color.Parse("#3B769F"),
        Color.Parse("#EAFBFF"),
        Color.Parse("#7CFAFF"),
        Color.Parse("#EAF7FF"),
        Color.Parse("#7EA4C5"),
        Color.Parse("#32C8FF"));

    public static ScenePalette Light { get; } = new(
        Color.Parse("#EAF5FF"),
        Color.Parse("#84B5D6"),
        Color.Parse("#4D8FC5"),
        Color.Parse("#008DE3"),
        Color.Parse("#0B62A5"),
        Color.Parse("#7CA6C5"),
        Color.Parse("#004B91"),
        Color.Parse("#00A8CB"),
        Color.Parse("#082B4C"),
        Color.Parse("#527897"),
        Color.Parse("#0079D8"));
}
