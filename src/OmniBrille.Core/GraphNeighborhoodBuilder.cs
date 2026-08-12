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

    public int NodeBudget => _nodeBudget;

    public ExplorerNeighborhood Build(
        ExplorerDirectorySnapshot snapshot,
        ExplorerEntry? previousContext = null,
        string? preferredNodeId = null,
        AggregatePage? aggregatePage = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var nodes = new List<ExplorerNode>(_nodeBudget)
        {
            ExplorerNode.FromEntry(snapshot.Focus),
        };
        var edges = new List<ExplorerEdge>(_nodeBudget - 1);

        if (previousContext is not null &&
            !ExplorerIdentity.Equals(previousContext.Id, snapshot.Focus.Id))
        {
            var context = ExplorerNode.FromEntry(previousContext) with
            {
                Kind = ExplorerNodeKind.Context,
                IsNavigable = true,
            };
            nodes.Add(context);
            edges.Add(new ExplorerEdge(context.Id, snapshot.Focus.Id));
        }

        var orderedChildren = Order(snapshot.Children);
        var sourceChildCount = orderedChildren.Length;
        var contextRepresentsChild = previousContext is not null && orderedChildren.Any(child =>
            ExplorerIdentity.Equals(child.Id, previousContext.Id));
        if (contextRepresentsChild)
        {
            orderedChildren = orderedChildren
                .Where(child => !ExplorerIdentity.Equals(child.Id, previousContext!.Id))
                .ToArray();
        }

        var totalChildCount = Math.Max(snapshot.TotalChildCount ?? sourceChildCount, sourceChildCount);
        var pageableChildCount = Math.Max(0, totalChildCount - (contextRepresentsChild ? 1 : 0));
        var available = Math.Max(0, _nodeBudget - nodes.Count);
        var visibleChildCount = aggregatePage is null
            ? BuildOverview(snapshot, orderedChildren, preferredNodeId, available, nodes, edges, pageableChildCount)
            : BuildRefinedPage(snapshot, orderedChildren, aggregatePage, available, nodes, edges);
        var hiddenCount = Math.Max(0, pageableChildCount - visibleChildCount);

        return new ExplorerNeighborhood(
            snapshot.Focus.Id,
            nodes,
            edges,
            totalChildCount,
            hiddenCount,
            snapshot.Warning,
            snapshot.WasTruncated,
            aggregatePage is null ? null : NormalizePage(aggregatePage, orderedChildren.Length, available));
    }

    private static ExplorerEntry[] Order(IReadOnlyList<ExplorerEntry> children) => children
        .OrderBy(entry => entry.Kind == ExplorerNodeKind.Folder ? 0 : 1)
        .ThenBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
        .ThenBy(entry => entry.Name, StringComparer.Ordinal)
        .ThenBy(entry => entry.Path, StringComparer.OrdinalIgnoreCase)
        .ThenBy(entry => entry.Path, StringComparer.Ordinal)
        .ToArray();

    private static int BuildOverview(
        ExplorerDirectorySnapshot snapshot,
        ExplorerEntry[] orderedChildren,
        string? preferredNodeId,
        int available,
        List<ExplorerNode> nodes,
        List<ExplorerEdge> edges,
        int totalChildCount)
    {
        var requiresAggregate = totalChildCount > available || orderedChildren.Length > available;
        var visibleCount = requiresAggregate ? Math.Max(0, available - 1) : Math.Min(available, orderedChildren.Length);
        var visibleChildren = orderedChildren.Take(visibleCount).ToList();

        if (preferredNodeId is not null && visibleCount > 0)
        {
            var preferred = orderedChildren.FirstOrDefault(entry =>
                ExplorerIdentity.Equals(entry.Id, preferredNodeId));
            if (preferred is not null && visibleChildren.All(entry =>
                    !ExplorerIdentity.Equals(entry.Id, preferred.Id)))
            {
                visibleChildren[^1] = preferred;
            }
        }

        AddChildren(snapshot.Focus.Id, visibleChildren, nodes, edges);

        var hiddenCount = Math.Max(0, totalChildCount - visibleCount);
        if (hiddenCount > 0 && nodes.Count < nodes.Capacity)
        {
            var canRefine = orderedChildren.Length > visibleCount;
            AddAggregate(
                snapshot.Focus,
                nodes,
                edges,
                "overview",
                snapshot.WasTruncated ? $"{hiddenCount:N0}+ more items" : $"{hiddenCount:N0} more items",
                hiddenCount,
                canRefine
                    ? new AggregateAction(
                        AggregateActionKind.OpenPage,
                        visibleCount,
                        "Open a bounded page of the hidden structural items.")
                    : null);
        }

        return visibleChildren.Count;
    }

    private static int BuildRefinedPage(
        ExplorerDirectorySnapshot snapshot,
        ExplorerEntry[] orderedChildren,
        AggregatePage requestedPage,
        int available,
        List<ExplorerNode> nodes,
        List<ExplorerEdge> edges)
    {
        var normalizedPage = NormalizePage(requestedPage, orderedChildren.Length, available);
        var visibleChildren = orderedChildren
            .Skip(normalizedPage.Offset)
            .Take(normalizedPage.PageSize)
            .ToArray();
        AddChildren(snapshot.Focus.Id, visibleChildren, nodes, edges);

        if (nodes.Count < nodes.Capacity)
        {
            AddAggregate(
                snapshot.Focus,
                nodes,
                edges,
                "back",
                "Back to overview",
                Math.Max(0, orderedChildren.Length - visibleChildren.Length),
                new AggregateAction(AggregateActionKind.Overview, Description: "Return to the original bounded neighborhood."));
        }

        var overviewStart = Math.Max(0, available - 1);
        if (normalizedPage.Offset > overviewStart && nodes.Count < nodes.Capacity)
        {
            var previousOffset = Math.Max(overviewStart, normalizedPage.Offset - normalizedPage.PageSize);
            var previousCount = normalizedPage.Offset - previousOffset;
            AddAggregate(
                snapshot.Focus,
                nodes,
                edges,
                $"previous:{previousOffset}",
                $"Previous · {DescribeRange(orderedChildren, previousOffset, previousCount)}",
                previousCount,
                new AggregateAction(AggregateActionKind.PreviousPage, previousOffset, "Show the preceding deterministic page."));
        }

        var nextOffset = normalizedPage.Offset + visibleChildren.Length;
        if (nextOffset < orderedChildren.Length && nodes.Count < nodes.Capacity)
        {
            var nextCount = Math.Min(normalizedPage.PageSize, orderedChildren.Length - nextOffset);
            AddAggregate(
                snapshot.Focus,
                nodes,
                edges,
                $"next:{nextOffset}",
                $"Next · {DescribeRange(orderedChildren, nextOffset, nextCount)}",
                nextCount,
                new AggregateAction(AggregateActionKind.NextPage, nextOffset, "Show the next deterministic page."));
        }
        else if (snapshot.WasTruncated && nodes.Count < nodes.Capacity)
        {
            AddAggregate(
                snapshot.Focus,
                nodes,
                edges,
                "source-truncated",
                "Additional items were not enumerated",
                Math.Max(0, (snapshot.TotalChildCount ?? orderedChildren.Length) - orderedChildren.Length),
                null);
        }

        return visibleChildren.Length;
    }

    private static AggregatePage NormalizePage(AggregatePage page, int childCount, int available)
    {
        var maximumPageSize = available >= 4
            ? available - 3
            : Math.Max(0, available - 1);
        var pageSize = Math.Max(0, Math.Min(page.PageSize > 0 ? page.PageSize : maximumPageSize, maximumPageSize));
        var maximumOffset = Math.Max(0, childCount - Math.Max(1, pageSize));
        return new AggregatePage(Math.Clamp(page.Offset, 0, maximumOffset), pageSize);
    }

    private static void AddChildren(
        string focusId,
        IEnumerable<ExplorerEntry> children,
        List<ExplorerNode> nodes,
        List<ExplorerEdge> edges)
    {
        foreach (var child in children)
        {
            nodes.Add(ExplorerNode.FromEntry(child));
            edges.Add(new ExplorerEdge(focusId, child.Id));
        }
    }

    private static void AddAggregate(
        ExplorerEntry focus,
        List<ExplorerNode> nodes,
        List<ExplorerEdge> edges,
        string key,
        string label,
        int itemCount,
        AggregateAction? action)
    {
        var id = $"aggregate:{focus.Id}:{key}";
        nodes.Add(new ExplorerNode(
            id,
            label,
            focus.Path,
            ExplorerNodeKind.Aggregate,
            null,
            null,
            action is not null,
            itemCount,
            action));
        edges.Add(new ExplorerEdge(focus.Id, id));
    }

    private static string DescribeRange(ExplorerEntry[] children, int offset, int count)
    {
        if (count <= 0 || offset >= children.Length)
        {
            return "empty page";
        }

        var first = Shorten(children[offset].Name);
        var last = Shorten(children[Math.Min(children.Length - 1, offset + count - 1)].Name);
        return count == 1 ? first : $"{first} — {last}";
    }

    private static string Shorten(string value) => value.Length <= 18 ? value : $"{value[..15]}…";
}
