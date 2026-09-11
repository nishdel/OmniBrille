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
    public void Layout_PreservesSurvivingNodeCoordinatesWhenPresentationBandIsUnchanged()
    {
        var focus = Node("root", ExplorerNodeKind.Folder);
        var survivors = Enumerable.Range(0, 7).Select(index => Node($"n{index}", ExplorerNodeKind.File)).ToArray();
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
    public void Layout_KeepsAllDenseStructuralChildrenOnOneTruthfulPlaneWithDensityBands()
    {
        var focus = Node("root", ExplorerNodeKind.Folder);
        var children = Enumerable.Range(0, 40).Select(index => Node($"n{index:D2}", ExplorerNodeKind.File)).ToArray();

        var layout = new RadialGraphLayout().Layout(Neighborhood(focus, children));

        Assert.Equal([0, 1], layout.Values.Select(node => node.Depth).Distinct().Order().ToArray());
        Assert.Equal(12, layout.Values.Count(node => node.PresentationBand == 1));
        Assert.Equal(16, layout.Values.Count(node => node.PresentationBand == 2));
        Assert.Equal(12, layout.Values.Count(node => node.PresentationBand == 3));
        Assert.All(children, child => Assert.Equal(1, layout[child.Id].Depth));
        Assert.All(layout.Values.Where(node => node.PresentationBand == 1), node =>
        {
            Assert.True(node.Scale >= 0.8);
            Assert.True(node.Opacity >= 0.95);
        });
        Assert.All(layout.Values.Where(node => node.PresentationBand == 2), node =>
        {
            Assert.InRange(node.Scale, 0.65, 0.75);
            Assert.InRange(node.Opacity, 0.8, 0.95);
        });
        Assert.All(layout.Values.Where(node => node.PresentationBand == 3), node =>
        {
            Assert.True(node.Scale >= 0.5);
            Assert.True(node.Opacity >= 0.65);
        });
    }

    [Fact]
    public void Layout_KeepsEverySmallNeighborhoodChildInTheCrispFocusPlane()
    {
        var focus = Node("root", ExplorerNodeKind.Folder);
        var children = Enumerable.Range(0, 8).Select(index => Node($"n{index:D2}", ExplorerNodeKind.File)).ToArray();

        var layout = new RadialGraphLayout().Layout(Neighborhood(focus, children));

        Assert.All(children, child =>
        {
            Assert.Equal(1, layout[child.Id].Depth);
            Assert.True(layout[child.Id].Opacity >= 0.95);
        });
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

        Assert.All(folders, folder => Assert.InRange(layout[folder.Id].PresentationBand, 1, 2));
        Assert.True(files.Count(file => layout[file.Id].PresentationBand == 3) > folders.Count(folder => layout[folder.Id].PresentationBand == 3));
    }

    [Fact]
    public void Layout_VariesSpokeLengthAndAngleWithoutChangingTheBoundedScene()
    {
        var focus = Node("root", ExplorerNodeKind.Folder);
        var children = Enumerable.Range(0, 47).Select(index => Node($"folder-{index:D2}", ExplorerNodeKind.Folder)).ToArray();
        var neighborhood = Neighborhood(focus, children);

        var layout = new RadialGraphLayout().Layout(neighborhood);

        Assert.Equal(48, layout.Count);
        Assert.Equal(neighborhood.Nodes.Select(node => node.Id).Order(), layout.Keys.Order());
        Assert.Equal(47, neighborhood.Edges.Count);
        Assert.All(layout.Values, position =>
        {
            Assert.InRange(position.X, -0.74, 0.74);
            Assert.InRange(position.Y, -0.64, 0.64);
        });
        var inner = layout.Values.Where(position => position.PresentationBand == 1).ToArray();
        var radii = inner.Select(position => Math.Sqrt(Math.Pow(position.X / 0.29, 2) + Math.Pow(position.Y / 0.27, 2))).ToArray();
        Assert.True(radii.Max() - radii.Min() > 0.05, "The spokes should have visibly different lengths within a density band.");
        var angles = inner.Select(position => Math.Atan2(position.Y / 0.27, position.X / 0.29)).Order().ToArray();
        var gaps = angles.Select((angle, index) => (angles[(index + 1) % angles.Length] - angle + Math.PI * 2) % (Math.PI * 2)).ToArray();
        Assert.True(gaps.Max() - gaps.Min() > 0.02, "The spokes should not form an evenly spaced polygon.");
    }

    [Fact]
    public void Layout_GroupsExactFileExtensionsDespiteInterleavedNamesAndOpaqueTargets()
    {
        var focus = Node("root", ExplorerNodeKind.Folder);
        var documents = new[] { File("a.pdf"), File("d.PDF"), File("g.pdf"), File("j.pdf") };
        var images = new[] { File("b.png"), File("e.PNG"), File("h.png"), File("k.png") };
        var text = new[] { File("c.txt"), File("f.TXT"), File("i.txt"), File("l.txt") };
        var children = documents.Concat(images).Concat(text).ToArray();
        var neighborhood = Neighborhood(focus, children);
        var engine = new RadialGraphLayout();

        var layout = engine.Layout(neighborhood);

        AssertCloserWithinGroup(layout, documents, images.Concat(text).ToArray());
        AssertCloserWithinGroup(layout, images, documents.Concat(text).ToArray());
        AssertCloserWithinGroup(layout, text, documents.Concat(images).ToArray());
        Assert.Equal(layout, engine.Layout(Neighborhood(focus, children.Reverse())));
        Assert.Equal(layout, engine.Layout(neighborhood, layout));
        Assert.All(children, node => Assert.Equal(1, layout[node.Id].Depth));
    }

    [Fact]
    public void Layout_KeepsUnknownExtensionsAndNamesWithoutExtensionsTogether()
    {
        var focus = Node("root", ExplorerNodeKind.Folder);
        var unknown = new[] { File("a.unrecognized"), File("d.unfamiliar"), File("g"), File(".hidden") };
        var known = new[] { File("b.pdf"), File("e.pdf"), File("h.pdf"), File("k.pdf"), File("c.png"), File("f.png"), File("i.png"), File("l.png") };

        var layout = new RadialGraphLayout().Layout(Neighborhood(focus, unknown.Concat(known)));

        AssertCloserWithinGroup(layout, unknown, known);
    }

    [Fact]
    public void Layout_AlignsAFileGroupThatStraddlesPresentationBands()
    {
        var focus = Node("root", ExplorerNodeKind.Folder);
        var folders = Enumerable.Range(0, 9).Select(index => Node($"folder-{index:D2}", ExplorerNodeKind.Folder));
        var documents = Enumerable.Range(0, 6).Select(index => File($"document-{index:D2}.pdf")).ToArray();
        var text = Enumerable.Range(0, 6).Select(index => File($"text-{index:D2}.txt"));

        var layout = new RadialGraphLayout().Layout(Neighborhood(focus, folders.Concat(documents).Concat(text)));

        var inner = documents.Select(node => layout[node.Id]).Where(position => position.PresentationBand == 1).ToArray();
        var middle = documents.Select(node => layout[node.Id]).Where(position => position.PresentationBand == 2).ToArray();
        Assert.Equal(3, inner.Length);
        Assert.Equal(3, middle.Length);
        var innerAngle = MeanAngle(inner, 0.29, 0.27);
        var middleAngle = MeanAngle(middle, 0.49, 0.43);
        Assert.InRange(Math.Abs(Math.Atan2(Math.Sin(innerAngle - middleAngle), Math.Cos(innerAngle - middleAngle))), 0, 0.001);
    }

    [Fact]
    public void Layout_RefreshCannotScatterAFileGroupIntoAnUnrelatedSector()
    {
        var focus = Node("root", ExplorerNodeKind.Folder);
        var documents = Enumerable.Range(0, 4).Select(index => File($"document-{index:D2}.pdf")).ToArray();
        var images = Enumerable.Range(0, 4).Select(index => File($"image-{index:D2}.png")).ToArray();
        var text = Enumerable.Range(0, 4).Select(index => File($"text-{index:D2}.txt")).ToArray();
        var neighborhood = Neighborhood(focus, documents.Concat(images).Concat(text));
        var engine = new RadialGraphLayout();
        var original = engine.Layout(neighborhood);
        var misleadingPreviousLayout = original.ToDictionary(pair => pair.Key, pair => pair.Value);
        for (var index = 0; index < documents.Length; index++)
        {
            misleadingPreviousLayout[documents[index].Id] = original[images[index].Id] with { NodeId = documents[index].Id };
        }

        var refreshed = engine.Layout(neighborhood, misleadingPreviousLayout);

        AssertCloserWithinGroup(refreshed, documents, images.Concat(text).ToArray());
        Assert.All(documents, node => Assert.Equal(1, refreshed[node.Id].PresentationBand));
    }

    [Fact]
    public void Layout_PreviewsRecedeOutwardFromTheirRealParentWithoutMovingDirectChildren()
    {
        var focus = new ExplorerEntry("root", "root", "root", ExplorerNodeKind.Folder);
        var parent = new ExplorerEntry("parent", "parent", "display only", ExplorerNodeKind.Folder, NavigationTarget: "opaque-parent");
        var other = new ExplorerEntry("other", "other", "other", ExplorerNodeKind.Folder);
        var previews = Enumerable.Range(0, 3).Select(index => new ExplorerEntry($"preview-{index}", $"preview-{index}", "display only",
            ExplorerNodeKind.Folder, NavigationTarget: $"opaque-preview-{index}", ParentNavigationTarget: parent.Target)).ToArray();
        var snapshot = new ExplorerDirectorySnapshot(focus, [parent, other]);
        var builder = new GraphNeighborhoodBuilder();
        var original = builder.Build(snapshot);
        var withPreviews = builder.Build(snapshot with { Previews = [new ExplorerDirectoryPreview(parent.Id, previews)] });
        var engine = new RadialGraphLayout();
        var originalLayout = engine.Layout(original);

        var layout = engine.Layout(withPreviews, originalLayout);

        Assert.Equal(withPreviews.Nodes.Count, layout.Count);
        Assert.All(original.Nodes, node => Assert.Equal(originalLayout[node.Id], layout[node.Id]));
        Assert.All(previews, preview =>
        {
            var position = layout[preview.Id];
            var parentPosition = layout[parent.Id];
            Assert.Equal(2, position.Depth);
            Assert.True(position.Scale < parentPosition.Scale);
            Assert.True(position.Opacity < parentPosition.Opacity);
            Assert.True(Distance(position, layout[focus.Id]) > Distance(parentPosition, layout[focus.Id]));
            Assert.True(Distance(position, parentPosition) < Distance(position, layout[other.Id]));
            Assert.InRange(position.X, -0.74, 0.74);
            Assert.InRange(position.Y, -0.68, 0.68);
        });
        Assert.Equal(layout, engine.Layout(withPreviews, layout));
    }

    [Theory]
    [InlineData(820, 520)]
    [InlineData(1280, 720)]
    public void Layout_DensePreviewTargetsCannotCoverDirectFoldersOrEachOther(int width, int height)
    {
        var (original, enriched) = PreviewScene(35);
        var engine = new RadialGraphLayout();
        var originalLayout = engine.Layout(original);

        var layout = engine.Layout(enriched, originalLayout);

        Assert.Equal(48, layout.Count);
        Assert.All(original.Nodes, node => Assert.Equal(originalLayout[node.Id], layout[node.Id]));
        AssertPreviewTargetsDoNotOverlap(enriched, layout, width, height);
        Assert.Equal(layout, engine.Layout(enriched, layout));
    }

    [Fact]
    public void Layout_PreviewPlacementPreservesEveryAdmittedIdAcrossAllSceneBudgets()
    {
        var engine = new RadialGraphLayout();
        for (var directCount = 1; directCount < 47; directCount++)
        {
            var (_, enriched) = PreviewScene(directCount);
            var layout = engine.Layout(enriched);

            Assert.Equal(enriched.Nodes.Count, layout.Count);
            AssertPreviewTargetsDoNotOverlap(enriched, layout, 820, 520);
            Assert.All(layout.Values.Where(node => node.PresentationBand == 4), position =>
            {
                Assert.InRange(position.X, -0.74, 0.74);
                Assert.InRange(position.Y, -0.68, 0.68);
            });
        }
    }

    private static (ExplorerNeighborhood Original, ExplorerNeighborhood Enriched) PreviewScene(int directCount)
    {
        var root = new ExplorerEntry("root", "root", "root", ExplorerNodeKind.Folder);
        var parents = Enumerable.Range(0, directCount)
            .Select(index => new ExplorerEntry($"folder-{index:D2}", $"folder-{index:D2}", $"folder-{index:D2}", ExplorerNodeKind.Folder))
            .ToArray();
        var previews = parents.Take(4).Select(parent => new ExplorerDirectoryPreview(parent.Id,
            Enumerable.Range(0, 3).Select(index => new ExplorerEntry(
                $"{parent.Id}-preview-{index}", $"{parent.Id}-preview-{index}", $"{parent.Id}/preview-{index}",
                ExplorerNodeKind.Folder, ParentNavigationTarget: parent.Target)).ToArray())).ToArray();
        var snapshot = new ExplorerDirectorySnapshot(root, parents);
        var builder = new GraphNeighborhoodBuilder();
        return (builder.Build(snapshot), builder.Build(snapshot with { Previews = previews }));
    }

    private static void AssertPreviewTargetsDoNotOverlap(
        ExplorerNeighborhood scene, IReadOnlyDictionary<string, GraphLayoutNode> layout,
        int width, int height)
    {
        var previews = scene.Nodes.Where(node => (node.Roles & ExplorerNodeRole.DescendantPreview) != 0);
        foreach (var preview in previews)
        {
            var position = layout[preview.Id];
            foreach (var other in layout.Values.Where(other => other.NodeId != preview.Id))
            {
                var separationX = Math.Abs(position.X - other.X) * (width - 40) * 0.66;
                var separationY = Math.Abs(position.Y - other.Y) * (height - 240) * 0.68;
                var halfWidth = Math.Max(22, (25 * position.Scale) + 10) + Math.Max(22, (25 * other.Scale) + 10);
                var halfHeight = Math.Max(22, (19 * position.Scale) + 10) + Math.Max(22, (19 * other.Scale) + 10);
                Assert.True(separationX >= halfWidth || separationY >= halfHeight,
                    $"Preview {preview.Id} overlaps target {other.NodeId} in the {width}x{height} scene.");
            }
        }
    }

    private static void AssertCloserWithinGroup(
        IReadOnlyDictionary<string, GraphLayoutNode> layout,
        ExplorerNode[] group,
        ExplorerNode[] otherGroups)
    {
        var within = group.SelectMany((node, index) => group.Skip(index + 1).Select(other => Distance(layout[node.Id], layout[other.Id]))).Average();
        var outside = group.SelectMany(node => otherGroups.Select(other => Distance(layout[node.Id], layout[other.Id]))).Average();
        Assert.True(within < outside * 0.75, $"Same-type distance {within:0.000} should be smaller than unlike-type distance {outside:0.000}.");
    }

    private static double Distance(GraphLayoutNode left, GraphLayoutNode right) =>
        Math.Sqrt(Math.Pow(left.X - right.X, 2) + Math.Pow(left.Y - right.Y, 2));

    private static double MeanAngle(GraphLayoutNode[] nodes, double radiusX, double radiusY)
    {
        var angles = nodes.Select(node => Math.Atan2(node.Y / radiusY, node.X / radiusX)).ToArray();
        return Math.Atan2(angles.Sum(Math.Sin), angles.Sum(Math.Cos));
    }

    private static ExplorerNode File(string name) =>
        new("opaque:" + name, name, "supplied display label", ExplorerNodeKind.File, null, null, true, NavigationTarget: "authorized-node:" + name);

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
