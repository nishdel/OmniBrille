using OmniBrille.Core;

namespace OmniBrille.Tests;

public sealed class GraphNeighborhoodBuilderTests
{
    [Fact]
    public void Build_AlwaysRespectsNodeBudgetAndAddsAggregate()
    {
        var snapshot = SnapshotWithChildren(30);

        var neighborhood = new GraphNeighborhoodBuilder(10).Build(snapshot);

        Assert.Equal(10, neighborhood.Nodes.Count);
        Assert.Equal(30, neighborhood.TotalChildCount);
        Assert.Equal(22, neighborhood.HiddenChildCount);
        var aggregate = Assert.Single(neighborhood.Nodes, node => node.Kind == ExplorerNodeKind.Aggregate);
        Assert.Equal(22, aggregate.AggregatedItemCount);
        Assert.True(aggregate.IsNavigable);
        Assert.Equal(AggregateActionKind.OpenPage, aggregate.AggregateAction!.Kind);
    }

    [Fact]
    public void Build_PrioritizesFoldersThenUsesStableNameOrdering()
    {
        var focus = Entry("root", ExplorerNodeKind.Folder);
        var snapshot = new ExplorerDirectorySnapshot(focus,
        [
            Entry("zeta.txt", ExplorerNodeKind.File),
            Entry("Zulu", ExplorerNodeKind.Folder),
            Entry("alpha.txt", ExplorerNodeKind.File),
            Entry("Alpha", ExplorerNodeKind.Folder),
        ]);

        var neighborhood = new GraphNeighborhoodBuilder(10).Build(snapshot);

        Assert.Equal(
            ["root", "Alpha", "Zulu", "alpha.txt", "zeta.txt"],
            neighborhood.Nodes.Select(node => node.Name));
    }

    [Fact]
    public void Build_IncludesPreviousFocusAsSubduedContext()
    {
        var snapshot = SnapshotWithChildren(2);
        var previous = Entry("previous", ExplorerNodeKind.Folder);

        var neighborhood = new GraphNeighborhoodBuilder(8).Build(snapshot, previous);

        var context = Assert.Single(neighborhood.Nodes, node => node.Kind == ExplorerNodeKind.Context);
        Assert.Equal("previous", context.Name);
        Assert.Contains(neighborhood.Edges, edge => edge.SourceId == context.Id && edge.TargetId == neighborhood.FocusNodeId);
    }

    [Fact]
    public void Build_WhenPreviousContextIsAlsoAChild_EmitsOneNodeAndCorrectCounts()
    {
        var snapshot = new ExplorerDirectorySnapshot(
            Entry("root", ExplorerNodeKind.Folder),
            [Entry("root/child", ExplorerNodeKind.Folder), Entry("root/file.txt", ExplorerNodeKind.File)]);
        var previous = Entry("root/child", ExplorerNodeKind.Folder);

        var neighborhood = new GraphNeighborhoodBuilder().Build(snapshot, previous);

        var previousNode = Assert.Single(neighborhood.Nodes, node => node.Id == previous.Id);
        Assert.Equal(ExplorerNodeKind.Context, previousNode.Kind);
        Assert.Equal(2, neighborhood.TotalChildCount);
        Assert.Equal(0, neighborhood.HiddenChildCount);
    }

    [Fact]
    public void Build_PreservesTruncatedSourceSignal()
    {
        var source = SnapshotWithChildren(4) with { WasTruncated = true, TotalChildCount = 5 };

        var neighborhood = new GraphNeighborhoodBuilder(4).Build(source);

        Assert.True(neighborhood.SourceWasTruncated);
        Assert.Equal(5, neighborhood.TotalChildCount);
        Assert.Contains("+", Assert.Single(neighborhood.Nodes, node => node.Kind == ExplorerNodeKind.Aggregate).Name);
    }

    [Fact]
    public void Build_PinsPreferredSearchMatchInsideNodeBudget()
    {
        var snapshot = SnapshotWithChildren(30);
        var preferred = snapshot.Children[^1];

        var neighborhood = new GraphNeighborhoodBuilder(8).Build(
            snapshot,
            preferredNodeId: preferred.Id);

        Assert.Equal(8, neighborhood.Nodes.Count);
        Assert.Contains(neighborhood.Nodes, node => node.Id == preferred.Id);
        Assert.Contains(neighborhood.Nodes, node => node.Kind == ExplorerNodeKind.Aggregate);
    }

    [Fact]
    public void Constructor_RejectsUnusableBudget()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new GraphNeighborhoodBuilder(2));
    }

    [Fact]
    public void Build_AggregatePageIsDeterministicBoundedAndReversible()
    {
        var snapshot = SnapshotWithChildren(40);
        var builder = new GraphNeighborhoodBuilder(10);
        var overview = builder.Build(snapshot);
        var openAction = Assert.Single(overview.Nodes, node => node.Kind == ExplorerNodeKind.Aggregate).AggregateAction!;

        var page = builder.Build(snapshot, aggregatePage: new AggregatePage(openAction.TargetOffset!.Value, 0));

        Assert.True(page.Nodes.Count <= 10);
        Assert.NotNull(page.AggregatePage);
        Assert.Contains(page.Nodes, node => node.AggregateAction?.Kind == AggregateActionKind.Overview);
        Assert.Contains(page.Nodes, node => node.AggregateAction?.Kind == AggregateActionKind.NextPage);
        Assert.Equal(
            page.Nodes.Select(node => node.Id),
            builder.Build(snapshot, aggregatePage: page.AggregatePage).Nodes.Select(node => node.Id));
    }

    [Fact]
    public void Build_NextAggregatePageDoesNotRepeatStructuralItems()
    {
        var snapshot = SnapshotWithChildren(40);
        var builder = new GraphNeighborhoodBuilder(10);
        var first = builder.Build(snapshot, aggregatePage: new AggregatePage(8, 0));
        var nextOffset = Assert.Single(
            first.Nodes,
            node => node.AggregateAction?.Kind == AggregateActionKind.NextPage).AggregateAction!.TargetOffset!.Value;

        var second = builder.Build(snapshot, aggregatePage: new AggregatePage(nextOffset, 0));
        var firstIds = first.Nodes.Where(node => node.Kind != ExplorerNodeKind.Aggregate).Select(node => node.Id).ToHashSet();
        var secondIds = second.Nodes.Where(node => node.Kind != ExplorerNodeKind.Aggregate).Select(node => node.Id).ToHashSet();

        Assert.Equal([snapshot.Focus.Id], firstIds.Intersect(secondIds));
    }

    [Fact]
    public void Build_AttachesActualFolderPreviewsToTheirAdmittedParentsWithoutChangingDirectCounts()
    {
        var parent = Entry("parent", ExplorerNodeKind.Folder) with { NavigationTarget = "opaque-parent" };
        var child = PreviewChild("preview", parent);
        var snapshot = new ExplorerDirectorySnapshot(Entry("root", ExplorerNodeKind.Folder), [parent],
            Previews: [new ExplorerDirectoryPreview(parent.Id, [child])]);

        var scene = new GraphNeighborhoodBuilder().Build(snapshot);

        Assert.Equal(1, scene.TotalChildCount);
        Assert.Equal(0, scene.HiddenChildCount);
        var preview = Assert.Single(scene.Nodes, node => (node.Roles & ExplorerNodeRole.DescendantPreview) != 0);
        Assert.Equal(child.Id, preview.Id);
        Assert.Equal(child.Target, preview.Target);
        Assert.Equal(child.ParentNavigationTarget, preview.ParentNavigationTarget);
        Assert.Equal(child.Path, preview.Path);
        Assert.Contains(scene.Edges, edge => edge.SourceId == parent.Id && edge.TargetId == child.Id);
        Assert.DoesNotContain(scene.Edges, edge => edge.SourceId == snapshot.Focus.Id && edge.TargetId == child.Id);
    }

    [Theory]
    [InlineData(4, 1)]
    [InlineData(5, 2)]
    [InlineData(9, 3)]
    public void Build_PreviousFocusAndDirectChildrenHavePriorityOverPreviewBudget(int budget, int expectedPreviews)
    {
        var parent = Entry("parent", ExplorerNodeKind.Folder);
        var previews = Enumerable.Range(0, 5).Select(index => PreviewChild($"preview-{index}", parent)).ToArray();
        var snapshot = new ExplorerDirectorySnapshot(Entry("root", ExplorerNodeKind.Folder), [parent],
            Previews: [new ExplorerDirectoryPreview(parent.Id, previews)]);
        var previous = Entry("previous", ExplorerNodeKind.Folder);

        var scene = new GraphNeighborhoodBuilder(budget).Build(snapshot, previous);

        Assert.Equal(expectedPreviews, scene.Nodes.Count(node => (node.Roles & ExplorerNodeRole.DescendantPreview) != 0));
        Assert.True(scene.Nodes.Count <= budget);
        Assert.Equal(1, scene.TotalChildCount);
        Assert.Equal(0, scene.HiddenChildCount);
        Assert.Contains(scene.Nodes, node => node.Id == parent.Id);
        Assert.Contains(scene.Nodes, node => node.Id == previous.Id);
    }

    [Fact]
    public void Build_PreviewsNeverEvictDirectChildrenOrAggregateControls()
    {
        var source = SnapshotWithChildren(70);
        var previews = source.Children.Where(entry => entry.Kind == ExplorerNodeKind.Folder).Take(4)
            .Select(parent => new ExplorerDirectoryPreview(parent.Id, [PreviewChild(parent.Id + "-preview", parent)])).ToArray();
        var builder = new GraphNeighborhoodBuilder();
        var original = builder.Build(source);

        var withPreviews = builder.Build(source with { Previews = previews });

        Assert.Equal(48, withPreviews.Nodes.Count);
        Assert.Equal(original.Nodes, withPreviews.Nodes);
        Assert.Equal(original.Edges, withPreviews.Edges);
        Assert.Equal(original.HiddenChildCount, withPreviews.HiddenChildCount);
        Assert.Contains(withPreviews.Nodes, node => node.Kind == ExplorerNodeKind.Aggregate);
    }

    [Fact]
    public void Build_LimitsPreviewsToFourParentsAndThreeChildrenEach()
    {
        var parents = Enumerable.Range(0, 6).Select(index => Entry($"parent-{index}", ExplorerNodeKind.Folder)).ToArray();
        var previews = parents.Select(parent => new ExplorerDirectoryPreview(parent.Id,
            Enumerable.Range(0, 7).Select(index => PreviewChild(parent.Id + "-child-" + index, parent)).ToArray())).ToArray();
        var snapshot = new ExplorerDirectorySnapshot(Entry("root", ExplorerNodeKind.Folder), parents, Previews: previews);

        var scene = new GraphNeighborhoodBuilder().Build(snapshot);

        Assert.Equal(12, scene.Nodes.Count(node => (node.Roles & ExplorerNodeRole.DescendantPreview) != 0));
        Assert.Equal(6, scene.TotalChildCount);
        Assert.Equal(0, scene.HiddenChildCount);
        Assert.All(parents.Take(4), parent => Assert.Equal(3, scene.Edges.Count(edge => edge.SourceId == parent.Id)));
        Assert.All(parents.Skip(4), parent => Assert.DoesNotContain(scene.Edges, edge => edge.SourceId == parent.Id));
    }

    [Fact]
    public void Build_RejectsDuplicateWrongParentNonfolderAndUnsafePreviewEntries()
    {
        var parent = Entry("parent", ExplorerNodeKind.Folder) with { NavigationTarget = "Parent-ID" };
        var otherParent = Entry("other-parent", ExplorerNodeKind.Folder);
        var valid = PreviewChild("valid", parent);
        var snapshot = new ExplorerDirectorySnapshot(Entry("root", ExplorerNodeKind.Folder), [parent, otherParent], Previews:
        [
            new(parent.Id, [valid, valid, PreviewChild("wrong-parent", parent) with { ParentNavigationTarget = "parent-id" }]),
            new(otherParent.Id,
            [
                PreviewChild("file", otherParent) with { Kind = ExplorerNodeKind.File },
                PreviewChild("disabled", otherParent) with { IsNavigable = false },
                PreviewChild("link", otherParent) with { IsReparsePoint = true },
            ]),
            new("missing-parent", [PreviewChild("orphan", parent)]),
            new(parent.Id, [PreviewChild("duplicate-parent", parent)]),
        ]);

        var scene = new GraphNeighborhoodBuilder().Build(snapshot);

        Assert.Equal(valid.Id, Assert.Single(scene.Nodes, node => (node.Roles & ExplorerNodeRole.DescendantPreview) != 0).Id);
        Assert.Equal(scene.Nodes.Count, scene.Nodes.Select(node => node.Id).Distinct(ExplorerIdentity.Comparer).Count());
    }

    [Fact]
    public void Build_DoesNotRecursivelyAdmitPreviewsOrRepeatAnExistingNode()
    {
        var parent = Entry("parent", ExplorerNodeKind.Folder);
        var child = PreviewChild("child", parent);
        var snapshot = new ExplorerDirectorySnapshot(Entry("root", ExplorerNodeKind.Folder), [parent], Previews:
        [
            new(parent.Id, [parent with { ParentNavigationTarget = parent.Target }, child]),
            new(child.Id, [PreviewChild("grandchild", child)]),
        ]);

        var scene = new GraphNeighborhoodBuilder().Build(snapshot);

        Assert.Equal(child.Id, Assert.Single(scene.Nodes, node => (node.Roles & ExplorerNodeRole.DescendantPreview) != 0).Id);
        Assert.Equal(3, scene.Nodes.Count);
    }

    private static ExplorerEntry PreviewChild(string id, ExplorerEntry parent) =>
        new(id, id, "provider-supplied display", ExplorerNodeKind.Folder,
            NavigationTarget: "opaque:" + id, ParentNavigationTarget: parent.Target);

    private static ExplorerDirectorySnapshot SnapshotWithChildren(int count)
    {
        var focus = Entry("root", ExplorerNodeKind.Folder);
        var children = Enumerable.Range(0, count)
            .Select(index => Entry($"item-{index:D3}", index % 3 == 0 ? ExplorerNodeKind.Folder : ExplorerNodeKind.File))
            .ToArray();
        return new ExplorerDirectorySnapshot(focus, children);
    }

    private static ExplorerEntry Entry(string name, ExplorerNodeKind kind) =>
        new(name, name, Path.Combine(Path.GetTempPath(), name), kind);
}
