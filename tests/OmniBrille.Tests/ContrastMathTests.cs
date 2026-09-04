using System.Text.RegularExpressions;
using System.Xml.Linq;
using OmniBrille.Core;
using OmniBrille.Desktop.Rendering;

namespace OmniBrille.Tests;

public sealed class ContrastMathTests
{
    [Theory]
    [InlineData("Dark")]
    [InlineData("Light")]
    public void ApplicationThemeResources_MeetEffectiveTextAndFocusContrastFloors(string theme)
    {
        var resources = ReadThemeResources(theme);
        var background = resources["AppBackgroundBrush"];
        var surface = Composite(resources["SurfaceStrongBrush"], background);
        var loadingOverlay = Composite(resources["LoadingOverlayBrush"], background);

        AssertContrast(resources["PrimaryTextBrush"], background, 4.5, theme, "primary text/background");
        AssertContrast(resources["PrimaryTextBrush"], surface, 4.5, theme, "primary text/surface");
        AssertContrast(resources["SecondaryTextBrush"], background, 4.5, theme, "secondary text/background");
        AssertContrast(resources["SecondaryTextBrush"], surface, 4.5, theme, "secondary text/surface");
        AssertContrast(resources["DangerBrush"], background, 4.5, theme, "danger text/background");
        AssertContrast(resources["AccentBrush"], background, 3, theme, "focus cue/background");
        AssertContrast(resources["AccentBrush"], surface, 3, theme, "focus cue/surface");
        AssertContrast(resources["LoadingTextBrush"], loadingOverlay, 4.5, theme, "loading text/overlay");
    }

    [Theory]
    [InlineData("Dark")]
    [InlineData("Light")]
    public void GraphPalette_MeetsEffectiveInteractiveAndLabelContrastFloors(string theme)
    {
        var palette = ReadScenePalette(theme);
        var background = palette["Background"];
        var glyphAlpha = GraphSceneControl.MinimumInteractiveOpacity * (250d / 255d);
        var pointAlpha = GraphSceneControl.MinimumInteractiveOpacity * (220d / 255d);

        foreach (var key in new[] { "Node", "ContextEdge", "Context", "Focus", "Search", "Selection" })
        {
            AssertContrast(
                Composite(palette[key], background, glyphAlpha),
                background,
                3,
                theme,
                $"graph {key} glyph/background");
        }

        AssertContrast(
            Composite(palette["ContextEdge"], background, pointAlpha),
            background,
            3,
            theme,
            "graph Context point/background");
        AssertContrast(palette["Text"], background, 4.5, theme, "graph label/background");
    }

    private static Dictionary<string, ParsedColor> ReadThemeResources(string theme)
    {
        var root = FindRepositoryRoot();
        var document = XDocument.Load(Path.Combine(root, "src", "OmniBrille.Desktop", "App.axaml"));
        var x = XNamespace.Get("http://schemas.microsoft.com/winfx/2006/xaml");
        var dictionary = document
            .Descendants()
            .Single(element => element.Name.LocalName == "ResourceDictionary" &&
                               (string?)element.Attribute(x + "Key") == theme);
        return dictionary.Elements()
            .Where(element => element.Name.LocalName == "SolidColorBrush")
            .ToDictionary(
                element => (string)element.Attribute(x + "Key")!,
                element => ParsedColor.Parse((string)element.Attribute("Color")!),
                StringComparer.Ordinal);
    }

    private static Dictionary<string, ParsedColor> ReadScenePalette(string theme)
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(root, "src", "OmniBrille.Desktop", "Rendering", "ScenePalette.cs"));
        var block = Regex.Match(
            source,
            $@"public static ScenePalette {Regex.Escape(theme)} \{{ get; \}} = new\((?<values>.*?)\);",
            RegexOptions.Singleline | RegexOptions.CultureInvariant);
        Assert.True(block.Success, $"Could not parse the {theme} ScenePalette initializer.");
        var values = Regex.Matches(block.Groups["values"].Value, "Color\\.Parse\\(\"(?<color>#[0-9A-Fa-f]+)\"\\)")
            .Select(match => ParsedColor.Parse(match.Groups["color"].Value))
            .ToArray();
        var names = new[]
        {
            "Background", "BackgroundPoint", "Edge", "EdgeGlow", "ContextEdge", "ContextEdgeGlow",
            "Node", "Context", "Focus", "Search", "Text", "MutedText", "Selection",
        };
        Assert.Equal(names.Length, values.Length);
        return names.Zip(values).ToDictionary(pair => pair.First, pair => pair.Second, StringComparer.Ordinal);
    }

    private static void AssertContrast(
        ParsedColor foreground,
        ParsedColor background,
        double minimum,
        string theme,
        string state)
    {
        var ratio = ContrastMath.Ratio(foreground.Rgb, background.Rgb);
        Assert.True(ratio >= minimum, $"{theme} {state} was {ratio:0.00}:1; expected at least {minimum:0.0}:1.");
    }

    private static ParsedColor Composite(ParsedColor foreground, ParsedColor background)
    {
        var alpha = foreground.Alpha / 255d;
        byte Blend(byte front, byte back) => (byte)Math.Round((front * alpha) + (back * (1 - alpha)));
        return new ParsedColor(
            255,
            new RgbColor(
                Blend(foreground.Rgb.Red, background.Rgb.Red),
                Blend(foreground.Rgb.Green, background.Rgb.Green),
                Blend(foreground.Rgb.Blue, background.Rgb.Blue)));
    }

    private static ParsedColor Composite(ParsedColor foreground, ParsedColor background, double opacity) =>
        Composite(foreground with { Alpha = (byte)Math.Round(Math.Clamp(opacity, 0, 1) * 255) }, background);

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Directory.Build.props")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the OmniBrille repository root.");
    }

    private readonly record struct ParsedColor(byte Alpha, RgbColor Rgb)
    {
        public static ParsedColor Parse(string hexadecimal)
        {
            var value = hexadecimal.TrimStart('#');
            return value.Length switch
            {
                6 => new ParsedColor(255, RgbColor.Parse(value)),
                8 => new ParsedColor(Convert.ToByte(value[..2], 16), RgbColor.Parse(value[2..])),
                _ => throw new FormatException($"Unsupported color '{hexadecimal}'."),
            };
        }
    }
}
