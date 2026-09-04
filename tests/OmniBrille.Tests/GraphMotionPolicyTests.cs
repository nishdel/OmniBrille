using OmniBrille.Core;

namespace OmniBrille.Tests;

public sealed class GraphMotionPolicyTests
{
    [Fact]
    public void Evaluate_IsDeterministicBoundedAndDoesNotChangeSemanticLayoutFields()
    {
        var source = new GraphLayoutNode("node", 0.42, -0.25, 0.7, 0.8, 1, 2);

        var first = GraphMotionPolicy.Evaluate(source, 12.5, 1, reducedMotion: false);
        var second = GraphMotionPolicy.Evaluate(source, 12.5, 1, reducedMotion: false);

        Assert.Equal(first, second);
        Assert.InRange(Math.Abs(first.X - source.X), 0, GraphMotionPolicy.MaximumNormalizedOffset + 0.008);
        Assert.InRange(Math.Abs(first.Y - source.Y), 0, GraphMotionPolicy.MaximumNormalizedOffset + 0.008);
        Assert.InRange(first.Scale, source.Scale, source.Scale * GraphMotionPolicy.MaximumLensScale);
        Assert.Equal(source.Depth, first.Depth);
        Assert.Equal(source.PresentationBand, first.PresentationBand);
    }

    [Fact]
    public void Evaluate_ReducedMotionReturnsTheImmutableBaseLayout()
    {
        var source = new GraphLayoutNode("node", 0.42, -0.25, 0.7, 0.8, 1, 2);

        var result = GraphMotionPolicy.Evaluate(source, 12.5, 1, reducedMotion: true);

        Assert.Equal(source, result);
    }

    [Fact]
    public void Evaluate_DoesNotFloatTheCurrentFocus()
    {
        var source = new GraphLayoutNode("focus", 0, 0, 0, 0, 1, 1);

        var result = GraphMotionPolicy.Evaluate(source, 31, 0, reducedMotion: false, isCurrentFocus: true);

        Assert.Equal(source, result);
    }
}
