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
        Assert.True(layout[context.Id].Depth > layout[focus.Id].Depth);
    }

    [Fact]
    public void Layout_PreservesSurvivingNodeCoordinatesAcrossRefresh()
    {
        var focus = Node("root", ExplorerNodeKind.Folder);
        var survivors = Enumerable.Range(0, 8).Select(index => Node($"n{index}", ExplorerNodeKind.File)).ToArray();
        var firstNeighborhood = Neighborhood(focus, survivors);
        var engine = new RadialGraphLayout();
        var first = engine.Layout(firstNeighborhood);
        var refreshed = Neighborhood(focus, [Node("new", ExplorerNodeKind.Folder), .. survivors.Reverse()]);

        var second = engine.Layout(refreshed, first);

        Assert.All(survivors, node =>
        {
            var xDelta = first[node.Id].X - second[node.Id].X;
            var yDelta = first[node.Id].Y - second[node.Id].Y;
            Assert.True((xDelta * xDelta) + (yDelta * yDelta) < 0.12);
        });
    }

    [Fact]
    public void Layout_UsesThreeDepthRingsForDenseScene()
    {
        var focus = Node("root", ExplorerNodeKind.Folder);
        var children = Enumerable.Range(0, 40).Select(index => Node($"n{index:D2}", ExplorerNodeKind.File)).ToArray();

        var layout = new RadialGraphLayout().Layout(Neighborhood(focus, children));

        Assert.Equal([0, 1, 2, 3], layout.Values.Select(node => node.Depth).Distinct().Order().ToArray());
        Assert.True(layout.Values.Where(node => node.Depth == 3).All(node => node.Opacity < 0.5));
    }

    [Fact]
    public void Layout_ContinuityNeverMovesStructurallyPreferredFoldersBehindFiles()
    {
        var focus = Node("root", ExplorerNodeKind.Folder);
        var folders = Enumerable.Range(0, 15).Select(index => Node($"folder-{index:D2}", ExplorerNodeKind.Folder)).ToArray();
        var files = Enumerable.Range(0, 25).Select(index => Node($"file-{index:D2}", ExplorerNodeKind.File)).ToArray();
        var neighborhood = Neighborhood(focus, [.. folders, .. files]);
        var misleadingPreviousLayout = files.ToDictionary(
            node => node.Id,
            node => new GraphLayoutNode(node.Id, 0, -0.28, 0.84, 0.98, 1));

        var layout = new RadialGraphLayout().Layout(neighborhood, misleadingPreviousLayout);

        Assert.All(folders, folder => Assert.InRange(layout[folder.Id].Depth, 1, 2));
        Assert.True(files.Count(file => layout[file.Id].Depth == 3) > folders.Count(folder => layout[folder.Id].Depth == 3));
    }

    private static ExplorerNeighborhood Neighborhood(ExplorerNode focus, IEnumerable<ExplorerNode> children)
    {
        var childArray = children.ToArray();
        return new ExplorerNeighborhood(
            focus.Id,
            [focus, .. childArray],
            childArray.Select(node => new ExplorerEdge(focus.Id, node.Id)).ToArray(),
            childArray.Length,
            0);
    }

    private static ExplorerNode Node(string id, ExplorerNodeKind kind) =>
        new(id, id, id, kind, null, null, true);
}
