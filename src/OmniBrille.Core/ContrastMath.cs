namespace OmniBrille.Core;

public readonly record struct RgbColor(byte Red, byte Green, byte Blue)
{
    public static RgbColor Parse(string hexadecimal)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hexadecimal);
        var value = hexadecimal.TrimStart('#');
        if (value.Length != 6)
        {
            throw new FormatException("RGB colors must use six hexadecimal digits.");
        }

        return new RgbColor(
            Convert.ToByte(value[..2], 16),
            Convert.ToByte(value[2..4], 16),
            Convert.ToByte(value[4..6], 16));
    }
}

public static class ContrastMath
{
    public static double Ratio(RgbColor first, RgbColor second)
    {
        var lighter = Math.Max(Luminance(first), Luminance(second));
        var darker = Math.Min(Luminance(first), Luminance(second));
        return (lighter + 0.05) / (darker + 0.05);
    }

    private static double Luminance(RgbColor color) =>
        (0.2126 * Linearize(color.Red)) +
        (0.7152 * Linearize(color.Green)) +
        (0.0722 * Linearize(color.Blue));

    private static double Linearize(byte channel)
    {
        var value = channel / 255d;
        return value <= 0.04045
            ? value / 12.92
            : Math.Pow((value + 0.055) / 1.055, 2.4);
    }
}
