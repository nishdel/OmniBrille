using OmniExplorer.Core;

namespace OmniExplorer.Tests;

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
        Assert.False(aggregate.IsNavigable);
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
