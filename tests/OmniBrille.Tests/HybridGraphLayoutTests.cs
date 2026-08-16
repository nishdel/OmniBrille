using OmniBrille.Core;

namespace OmniBrille.Tests;

public sealed class HybridGraphLayoutTests
{
    [Fact]
    public void Layout_IsDeterministicAndAnchorsFocus()
    {
        var scene = Scene();
        var engine = new HybridGraphLayout();

        var first = engine.Layout(scene);
        var second = engine.Layout(scene);

        Assert.Equal(new GraphLayoutNode("focus", 0, 0, 1.4, 1, 0), first["focus"]);
        Assert.Equal(first, second);
        Assert.Equal(scene.Nodes.Count, first.Count);
    }

    [Fact]
    public void Layout_SeparatesStructuralAndContextualPlanesWithoutDuplicatingBothRoleNode()
    {
        var layout = new HybridGraphLayout().Layout(Scene());

        Assert.True(layout["structural"].X < 0);
        Assert.True(layout["contextual"].X > 0);
        Assert.True(Math.Abs(layout["both"].X) < 0.35);
        Assert.Equal(5, layout.Count);
    }

    [Fact]
    public void Layout_PlacesStructuralParentAboveFocus()
    {
        var layout = new HybridGraphLayout().Layout(Scene());

        Assert.True(layout["parent"].Y < 0);
        Assert.Equal(2, layout["parent"].Depth);
    }

    private static ExplorerNeighborhood Scene()
    {
        var focus = Node("focus", ExplorerNodeRole.Structural | ExplorerNodeRole.Contextual);
        var parent = Node("parent", ExplorerNodeRole.Structural, ExplorerNodeKind.Folder);
        var structural = Node("structural", ExplorerNodeRole.Structural);
        var contextual = Node("contextual", ExplorerNodeRole.Contextual);
        var both = Node("both", ExplorerNodeRole.Structural | ExplorerNodeRole.Contextual);
        ExplorerEdge[] edges =
        [
            new(parent.Id, focus.Id),
            new(focus.Id, structural.Id),
            new(focus.Id, both.Id),
            ContextEdge(focus.Id, contextual.Id, "r1", 90),
            ContextEdge(focus.Id, both.Id, "r2", 80),
        ];
        return new ExplorerNeighborhood(
            focus.Id,
            [focus, parent, structural, contextual, both],
            edges,
            4,
            0,
            ViewMode: ExplorerViewMode.Hybrid);
    }

    private static ExplorerNode Node(
        string id,
        ExplorerNodeRole roles,
        ExplorerNodeKind kind = ExplorerNodeKind.File) => new(
        id,
        id,
        id,
        kind,
        null,
        null,
        true,
        Roles: roles);

    private static ExplorerEdge ContextEdge(string source, string target, string id, int strength) => new(
        source,
        target,
        ExplorerGraphEdgeKind.Contextual,
        new ExplorerRelationship(
            id,
            source,
            target,
            ExplorerRelationshipKind.Related,
            strength,
            "Reason",
            ExplorerRelationshipEvidenceClass.Deterministic,
            "OmniSorSe"));
}
