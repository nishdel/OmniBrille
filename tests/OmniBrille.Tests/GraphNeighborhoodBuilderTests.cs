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
