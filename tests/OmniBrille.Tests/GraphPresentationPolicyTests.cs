using OmniBrille.Core;

namespace OmniBrille.Tests;

public sealed class GraphPresentationPolicyTests
{
    [Theory]
    [InlineData(1.0, 48)]
    [InlineData(1.25, 48)]
    [InlineData(1.5, 48)]
    [InlineData(2.0, 48)]
    public void RecommendedLabelBudget_KeepsEveryNameAsTextScales(double textScale, int expected)
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
        var searchHovered = GraphPresentationPolicy.Evaluate(
            node,
            layout,
            Context(hovered: node.Id, searchActive: true));
        var reduced = GraphPresentationPolicy.Evaluate(node, layout, Context(reducedEffects: true));

        Assert.True(searching.OpacityMultiplier < normal.OpacityMultiplier);
        Assert.True(searchHovered.OpacityMultiplier > searching.OpacityMultiplier);
        Assert.True(searchHovered.EdgeMultiplier > searching.EdgeMultiplier);
        Assert.True(reduced.GlowMultiplier < normal.GlowMultiplier);
    }

    [Fact]
    public void Evaluate_PreservesEmphasizedNodesAndRecedesOuterDensityBands()
    {
        var node = Node("node");
        var immediate = GraphPresentationPolicy.Evaluate(
            node,
            new GraphLayoutNode(node.Id, 0, 0, 0.9, 0.98, 1, 1),
            Context());
        var secondary = GraphPresentationPolicy.Evaluate(
            node,
            new GraphLayoutNode(node.Id, 0, 0, 0.65, 0.68, 1, 2),
            Context());
        var ambient = GraphPresentationPolicy.Evaluate(
            node,
            new GraphLayoutNode(node.Id, 0, 0, 0.45, 0.4, 1, 3),
            Context());
        var selectedAmbient = GraphPresentationPolicy.Evaluate(
            node,
            new GraphLayoutNode(node.Id, 0, 0, 0.45, 0.4, 1, 3),
            Context(selected: node.Id));
        var hoveredAmbient = GraphPresentationPolicy.Evaluate(
            node,
            new GraphLayoutNode(node.Id, 0, 0, 0.45, 0.4, 1, 3),
            Context(hovered: node.Id));
        var highlightedAmbient = GraphPresentationPolicy.Evaluate(
            node,
            new GraphLayoutNode(node.Id, 0, 0, 0.32, 0.26, 1, 3),
            Context(highlights: new HashSet<string> { node.Id }, searchActive: true));

        Assert.True(immediate.OpacityMultiplier > secondary.OpacityMultiplier);
        Assert.True(secondary.OpacityMultiplier > ambient.OpacityMultiplier);
        Assert.True(immediate.GlowMultiplier > secondary.GlowMultiplier);
        Assert.True(immediate.EdgeMultiplier > secondary.EdgeMultiplier);
        Assert.True(secondary.EdgeMultiplier > ambient.EdgeMultiplier);
        Assert.Equal(GraphLevelOfDetail.Focused, selectedAmbient.LevelOfDetail);
        Assert.True(selectedAmbient.LabelIsRequired);
        Assert.True(selectedAmbient.OpacityMultiplier > ambient.OpacityMultiplier);
        Assert.True(selectedAmbient.EdgeMultiplier > ambient.EdgeMultiplier);
        Assert.Equal(GraphLevelOfDetail.Focused, hoveredAmbient.LevelOfDetail);
        Assert.True(hoveredAmbient.LabelIsRequired);
        Assert.True(hoveredAmbient.OpacityMultiplier > ambient.OpacityMultiplier);
        Assert.True(hoveredAmbient.EdgeMultiplier > ambient.EdgeMultiplier);
        Assert.Equal(GraphLevelOfDetail.Focused, highlightedAmbient.LevelOfDetail);
        Assert.True(highlightedAmbient.LabelIsRequired);
        Assert.True(highlightedAmbient.EdgeMultiplier > ambient.EdgeMultiplier);
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
    [InlineData(0.55, 48, 48)]
    [InlineData(1.0, 48, 48)]
    [InlineData(1.5, 48, 48)]
    public void RecommendedLabelBudget_DoesNotHideNamesWhenZoomChanges(double zoom, int nodes, int expected) =>
        Assert.Equal(expected, GraphPresentationPolicy.RecommendedLabelBudget(zoom, nodes));

    [Theory]
    [InlineData(0.5, 0.3)]
    [InlineData(1.0, 0.56)]
    [InlineData(2.0, 0.7)]
    public void EveryAdmittedFileHasALabelWithoutHover(double zoom, double scale)
    {
        var node = Node("far-away");
        var result = GraphPresentationPolicy.Evaluate(node, new(node.Id, 0.6, 0.5, scale, 0.7, 1, 3), Context() with { Zoom = zoom });
        Assert.True(result.LevelOfDetail >= GraphLevelOfDetail.Labeled);
    }

    [Fact]
    public void LabelPlacementMovesCollisionsWithoutDroppingNamesOrCoveringGlyphs()
    {
        var candidates = Enumerable.Range(0, 48).Select(index => new LabelCandidate($"n{index:D2}", new LabelBox(360, 250, 75, 16), 0)).ToArray();
        LabelBox[] glyphs = [new(340, 240, 80, 55)];
        var placed = GraphPresentationPolicy.PlaceLabels(candidates, new(6, 6, 808, 508), glyphs);
        Assert.Equal(48, placed.Count);
        foreach (var (id, box) in placed)
        {
            Assert.InRange(box.X, 6, 814 - box.Width);
            Assert.InRange(box.Y, 6, 514 - box.Height);
            Assert.DoesNotContain(glyphs, glyph => box.Intersects(glyph, 3));
            Assert.DoesNotContain(placed, other => other.Key != id && box.Intersects(other.Value, 3));
        }
        Assert.Equal(placed, GraphPresentationPolicy.PlaceLabels(candidates.Reverse(), new(6, 6, 808, 508), glyphs));
    }

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
