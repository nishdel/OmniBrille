using OmniBrille.Core;

namespace OmniBrille.Tests;

public sealed class ContextGraphLayoutTests
{
    [Fact]
    public void Layout_AnchorsFocusAndIsDeterministicWithoutPhysics()
    {
        var scene = Scene(18);
        var layout = new ContextGraphLayout();

        var first = layout.Layout(scene);
        var second = layout.Layout(scene);

        Assert.Equal(new GraphLayoutNode("focus", 0, 0, 1.38, 1, 0), first["focus"]);
        Assert.Equal(first, second);
        Assert.Equal(19, first.Count);
    }

    [Fact]
    public void Layout_PlacesStrongestRelationshipsInInnerRing()
    {
        var scene = Scene(14);
        var layout = new ContextGraphLayout().Layout(scene);

        Assert.Equal(1, layout["node-00"].Depth);
        Assert.Equal(1, layout["node-09"].Depth);
        Assert.Equal(2, layout["node-10"].Depth);
    }

    [Fact]
    public void Layout_UsesStrengthForSubtleDepthWithinTheSameRing()
    {
        var layout = new ContextGraphLayout().Layout(Scene(3));
        var strong = layout["node-00"];
        var weak = layout["node-02"];

        Assert.True(Distance(strong) < Distance(weak));
        Assert.True(strong.Scale > weak.Scale);
        Assert.True(strong.Opacity > weak.Opacity);
        Assert.Equal(strong.Depth, weak.Depth);
    }

    private static ExplorerNeighborhood Scene(int count)
    {
        var focus = Node("focus", "Focus");
        var nodes = Enumerable.Range(0, count).Select(index => Node($"node-{index:D2}", $"Node {index:D2}")).ToArray();
        var edges = nodes.Select((node, index) => new ExplorerEdge(
            focus.Id,
            node.Id,
            ExplorerGraphEdgeKind.Contextual,
            new ExplorerRelationship(
                $"r-{index:D2}",
                focus.Id,
                node.Id,
                ExplorerRelationshipKind.Related,
                100 - index,
                "Authoritative reason",
                ExplorerRelationshipEvidenceClass.Deterministic,
                "OmniSorSe"))).ToArray();
        return new ExplorerNeighborhood(
            focus.Id,
            [focus, .. nodes],
            edges,
            count,
            0,
            ViewMode: ExplorerViewMode.Context);
    }

    private static ExplorerNode Node(string id, string name) => new(
        id,
        name,
        name,
        ExplorerNodeKind.File,
        null,
        null,
        true,
        NavigationTarget: id);

    private static double Distance(GraphLayoutNode node) => Math.Sqrt((node.X * node.X) + (node.Y * node.Y));
}
