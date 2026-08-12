namespace OmniBrille.Core;

public sealed class GraphNeighborhoodBuilder
{
    public const int DefaultNodeBudget = 48;

    private readonly int _nodeBudget;

    public GraphNeighborhoodBuilder(int nodeBudget = DefaultNodeBudget)
    {
        if (nodeBudget < 3)
        {
            throw new ArgumentOutOfRangeException(nameof(nodeBudget), "A neighborhood needs room for focus, content, and aggregation.");
        }

        _nodeBudget = nodeBudget;
    }

    public ExplorerNeighborhood Build(
        ExplorerDirectorySnapshot snapshot,
        ExplorerEntry? previousContext = null,
        string? preferredNodeId = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var nodes = new List<ExplorerNode>(_nodeBudget)
        {
            ExplorerNode.FromEntry(snapshot.Focus),
        };
        var edges = new List<ExplorerEdge>(_nodeBudget - 1);

        if (previousContext is not null &&
            !StringComparer.OrdinalIgnoreCase.Equals(previousContext.Id, snapshot.Focus.Id))
        {
            var context = ExplorerNode.FromEntry(previousContext) with
            {
                Kind = ExplorerNodeKind.Context,
                IsNavigable = true,
            };
            nodes.Add(context);
            edges.Add(new ExplorerEdge(context.Id, snapshot.Focus.Id));
        }

        var orderedChildren = snapshot.Children
            .OrderBy(entry => entry.Kind == ExplorerNodeKind.Folder ? 0 : 1)
            .ThenBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.Path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var available = _nodeBudget - nodes.Count;
        var requiresAggregate = orderedChildren.Length > available;
        var visibleCount = requiresAggregate ? Math.Max(0, available - 1) : orderedChildren.Length;

        var visibleChildren = orderedChildren.Take(visibleCount).ToList();
        if (preferredNodeId is not null && visibleCount > 0)
        {
            var preferred = orderedChildren.FirstOrDefault(entry =>
                StringComparer.OrdinalIgnoreCase.Equals(entry.Id, preferredNodeId));
            if (preferred is not null && visibleChildren.All(entry =>
                    !StringComparer.OrdinalIgnoreCase.Equals(entry.Id, preferred.Id)))
            {
                visibleChildren[^1] = preferred;
            }
        }

        foreach (var child in visibleChildren)
        {
            nodes.Add(ExplorerNode.FromEntry(child));
            edges.Add(new ExplorerEdge(snapshot.Focus.Id, child.Id));
        }

        var totalChildCount = Math.Max(snapshot.TotalChildCount ?? orderedChildren.Length, orderedChildren.Length);
        var hiddenCount = totalChildCount - visibleCount;
        if (hiddenCount > 0)
        {
            var aggregateId = $"aggregate:{snapshot.Focus.Id}:{hiddenCount}";
            nodes.Add(new ExplorerNode(
                aggregateId,
                snapshot.WasTruncated ? $"{hiddenCount:N0}+ more items" : $"{hiddenCount:N0} more items",
                snapshot.Focus.Path,
                ExplorerNodeKind.Aggregate,
                null,
                null,
                false,
                hiddenCount));
            edges.Add(new ExplorerEdge(snapshot.Focus.Id, aggregateId));
        }

        return new ExplorerNeighborhood(
            snapshot.Focus.Id,
            nodes,
            edges,
            totalChildCount,
            hiddenCount,
            snapshot.Warning,
            snapshot.WasTruncated);
    }
}
