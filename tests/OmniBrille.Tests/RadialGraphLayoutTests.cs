using OmniBrille.Core;

namespace OmniBrille.Tests;

public sealed class RadialGraphLayoutTests
{
    [Fact]
    public void Layout_AnchorsFocusAtCenterAndIsDeterministic()
    {
        var focus = Node("root", ExplorerNodeKind.Folder);
        var children = Enumerable.Range(0, 20).Select(index => Node($"n{index}", ExplorerNodeKind.File)).ToArray();
        var nodes = new[] { focus }.Concat(children).ToArray();
        var edges = children.Select(node => new ExplorerEdge(focus.Id, node.Id)).ToArray();
        var neighborhood = new ExplorerNeighborhood(focus.Id, nodes, edges, children.Length, 0);
        var engine = new RadialGraphLayout();

        var first = engine.Layout(neighborhood);
        var second = engine.Layout(neighborhood);

        Assert.Equal((0d, 0d), (first[focus.Id].X, first[focus.Id].Y));
        Assert.Equal(first, second);
        Assert.Equal(nodes.Length, first.Count);
        Assert.All(children, node => Assert.NotEqual((0d, 0d), (first[node.Id].X, first[node.Id].Y)));
    }

    [Fact]
    public void Layout_RecedesContextNode()
    {
        var focus = Node("root", ExplorerNodeKind.Folder);
        var context = Node("parent", ExplorerNodeKind.Context);
        var neighborhood = new ExplorerNeighborhood(
            focus.Id,
            [focus, context],
            [new ExplorerEdge(context.Id, focus.Id)],
            0,
            0);

        var layout = new RadialGraphLayout().Layout(neighborhood);

        Assert.True(layout[context.Id].Opacity < layout[focus.Id].Opacity);
        Assert.True(layout[context.Id].Scale < layout[focus.Id].Scale);
    }

    private static ExplorerNode Node(string id, ExplorerNodeKind kind) =>
        new(id, id, id, kind, null, null, true);
}
