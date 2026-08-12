using OmniBrille.Core;

namespace OmniBrille.Tests;

public sealed class ContrastMathTests
{
    [Theory]
    [InlineData("EAF7FF", "030B1A", 15)]
    [InlineData("82A6C8", "030B1A", 7)]
    [InlineData("082B4C", "EAF5FF", 10)]
    [InlineData("426A8E", "EAF5FF", 4.5)]
    [InlineData("006D86", "EAF5FF", 4.5)]
    public void ThemeTextPairs_MeetDocumentedContrastFloor(
        string foreground,
        string background,
        double minimum)
    {
        var ratio = ContrastMath.Ratio(RgbColor.Parse(foreground), RgbColor.Parse(background));

        Assert.True(ratio >= minimum, $"Expected {foreground}/{background} to be >= {minimum}:1, but it was {ratio:0.00}:1.");
    }
}
