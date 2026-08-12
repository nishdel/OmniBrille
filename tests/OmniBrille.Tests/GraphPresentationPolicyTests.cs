using OmniBrille.Core;

namespace OmniBrille.Tests;

public sealed class GraphPresentationPolicyTests
{
    [Theory]
    [InlineData(1.0, 22)]
    [InlineData(1.25, 18)]
    [InlineData(1.5, 14)]
    [InlineData(2.0, 10)]
    public void RecommendedLabelBudget_ReducesDensityAsTextScales(double textScale, int expected)
    {
        Assert.Equal(expected, GraphPresentationPolicy.RecommendedLabelBudget(1, 48, textScale));
    }

    [Fact]
    public void Evaluate_PrioritizesFocusSelectionSearchAndHover()
    {
        var node = Node("node");
        var layout = new GraphLayoutNode(node.Id, 0, 0, 0.5, 1, 2);

        var ordinary = GraphPresentationPolicy.Evaluate(node, layout, Context());
        var hovered = GraphPresentationPolicy.Evaluate(node, layout, Context(hovered: node.Id));
        var highlighted = GraphPresentationPolicy.Evaluate(
            node,
            layout,
            Context(highlights: new HashSet<string> { node.Id }, searchActive: true));
        var selected = GraphPresentationPolicy.Evaluate(node, layout, Context(selected: node.Id));
        var focus = GraphPresentationPolicy.Evaluate(node, layout, Context(focus: node.Id));

        Assert.True(ordinary.LabelPriority < hovered.LabelPriority);
        Assert.True(hovered.LabelPriority < highlighted.LabelPriority);
        Assert.True(highlighted.LabelPriority < selected.LabelPriority);
        Assert.True(selected.LabelPriority < focus.LabelPriority);
    }

    [Fact]
    public void Evaluate_DimsUnrelatedNodesDuringSearchAndReducesGlow()
    {
        var node = Node("unrelated");
        var layout = new GraphLayoutNode(node.Id, 0, 0, 0.8, 1, 1);

        var normal = GraphPresentationPolicy.Evaluate(node, layout, Context());
        var searching = GraphPresentationPolicy.Evaluate(node, layout, Context(searchActive: true));
        var reduced = GraphPresentationPolicy.Evaluate(node, layout, Context(reducedEffects: true));

        Assert.True(searching.OpacityMultiplier < normal.OpacityMultiplier);
        Assert.True(reduced.GlowMultiplier < normal.GlowMultiplier);
    }

    [Fact]
    public void ResolveLabels_KeepsRequiredLabelsAndRejectsLowerPriorityCollisions()
    {
        LabelCandidate[] candidates =
        [
            new("focus", new LabelBox(0, 0, 100, 20), 1000, true),
            new("overlap", new LabelBox(10, 5, 100, 20), 500),
            new("clear", new LabelBox(150, 0, 100, 20), 400),
        ];

        var visible = GraphPresentationPolicy.ResolveLabels(candidates, 3);

        Assert.Contains("focus", visible);
        Assert.DoesNotContain("overlap", visible);
        Assert.Contains("clear", visible);
    }

    [Theory]
    [InlineData(0.55, 48, 10)]
    [InlineData(1.0, 48, 22)]
    [InlineData(1.5, 48, 34)]
    public void RecommendedLabelBudget_IsZoomAware(double zoom, int nodes, int expected) =>
        Assert.Equal(expected, GraphPresentationPolicy.RecommendedLabelBudget(zoom, nodes));

    private static ExplorerNode Node(string id) => new(id, id, id, ExplorerNodeKind.File, null, null, false);

    private static GraphPresentationContext Context(
        string focus = "focus",
        string? selected = null,
        string? hovered = null,
        IReadOnlySet<string>? highlights = null,
        bool searchActive = false,
        bool reducedEffects = false) => new(
            1,
            48,
            focus,
            selected,
            hovered,
            highlights ?? new HashSet<string>(),
            searchActive,
            reducedEffects);
}
