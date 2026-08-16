namespace OmniBrille.Core;

/// <summary>
/// Stable Hybrid layout with Structure as the left/top orientation plane and
/// authoritative Context as the right plane. It uses no continuous simulation.
/// </summary>
public sealed class HybridGraphLayout : IGraphLayoutEngine
{
    public IReadOnlyDictionary<string, GraphLayoutNode> Layout(
        ExplorerNeighborhood neighborhood,
        IReadOnlyDictionary<string, GraphLayoutNode>? previousLayout = null)
    {
        ArgumentNullException.ThrowIfNull(neighborhood);

        var result = new Dictionary<string, GraphLayoutNode>(ExplorerIdentity.Comparer)
        {
            [neighborhood.FocusNodeId] = new(neighborhood.FocusNodeId, 0, 0, 1.4, 1, 0),
        };
        var structuralEdges = neighborhood.Edges
            .Where(edge => edge.Kind == ExplorerGraphEdgeKind.Structural)
            .ToArray();
        var strengths = neighborhood.Edges
            .Where(edge => edge.Kind == ExplorerGraphEdgeKind.Contextual && edge.Relationship is not null)
            .SelectMany(edge => new[]
            {
                new KeyValuePair<string, int>(edge.SourceId, edge.Relationship!.Strength),
                new KeyValuePair<string, int>(edge.TargetId, edge.Relationship!.Strength),
            })
            .GroupBy(pair => pair.Key, ExplorerIdentity.Comparer)
            .ToDictionary(group => group.Key, group => group.Max(pair => pair.Value), ExplorerIdentity.Comparer);
        var nodes = neighborhood.Nodes
            .Where(node => !ExplorerIdentity.Equals(node.Id, neighborhood.FocusNodeId))
            .ToArray();
        var parents = nodes
            .Where(node => structuralEdges.Any(edge =>
                ExplorerIdentity.Equals(edge.SourceId, node.Id) &&
                ExplorerIdentity.Equals(edge.TargetId, neighborhood.FocusNodeId)))
            .OrderBy(node => node.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(node => node.Id, ExplorerIdentity.Comparer)
            .ToArray();
        var both = nodes
            .Except(parents)
            .Where(node => HasRole(node, ExplorerNodeRole.Structural) && HasRole(node, ExplorerNodeRole.Contextual))
            .OrderByDescending(node => strengths.GetValueOrDefault(node.Id))
            .ThenBy(node => node.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(node => node.Id, ExplorerIdentity.Comparer)
            .ToArray();
        var structural = nodes
            .Except(parents)
            .Except(both)
            .Where(node => HasRole(node, ExplorerNodeRole.Structural))
            .OrderBy(node => node.Kind == ExplorerNodeKind.Folder ? 0 : 1)
            .ThenBy(node => node.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(node => node.Id, ExplorerIdentity.Comparer)
            .ToArray();
        var contextual = nodes
            .Except(parents)
            .Except(both)
            .Except(structural)
            .OrderByDescending(node => strengths.GetValueOrDefault(node.Id))
            .ThenBy(node => node.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(node => node.Id, ExplorerIdentity.Comparer)
            .ToArray();

        PlaceParents(result, parents);
        PlaceArc(result, structural, -1, 0.39, 0.42, 0.82, 0.92, 1, previousLayout);
        PlaceArc(result, both, 0, 0.33, 0.31, 0.88, 0.98, 1, previousLayout);
        PlaceContext(result, contextual, strengths, previousLayout);
        return result;
    }

    private static void PlaceParents(Dictionary<string, GraphLayoutNode> result, ExplorerNode[] parents)
    {
        for (var index = 0; index < parents.Length; index++)
        {
            var offset = index - ((parents.Length - 1) / 2d);
            result[parents[index].Id] = new(parents[index].Id, offset * 0.12, -0.3, 0.74, 0.8, 2);
        }
    }

    private static void PlaceArc(
        Dictionary<string, GraphLayoutNode> result,
        ExplorerNode[] nodes,
        int side,
        double radiusX,
        double radiusY,
        double scale,
        double opacity,
        int baseDepth,
        IReadOnlyDictionary<string, GraphLayoutNode>? previousLayout)
    {
        for (var index = 0; index < nodes.Length; index++)
        {
            var ring = index / 12;
            var slot = index % 12;
            var count = Math.Min(12, nodes.Length - (ring * 12));
            var angle = (-Math.PI / 2) + (Math.PI * (slot + 1) / (count + 1));
            var x = side == 0
                ? Math.Cos(angle) * (radiusX + (ring * 0.15)) * 0.58
                : side * (0.08 + (Math.Cos(angle) + 1) * (radiusX + (ring * 0.15)) * 0.5);
            var target = new GraphLayoutNode(
                nodes[index].Id,
                x,
                Math.Sin(angle) * (radiusY + (ring * 0.12)),
                scale - (ring * 0.14),
                opacity - (ring * 0.22),
                Math.Min(3, baseDepth + ring));
            result[nodes[index].Id] = PreserveSide(target, previousLayout);
        }
    }

    private static void PlaceContext(
        Dictionary<string, GraphLayoutNode> result,
        ExplorerNode[] nodes,
        IReadOnlyDictionary<string, int> strengths,
        IReadOnlyDictionary<string, GraphLayoutNode>? previousLayout)
    {
        for (var index = 0; index < nodes.Length; index++)
        {
            var ring = index / 12;
            var slot = index % 12;
            var count = Math.Min(12, nodes.Length - (ring * 12));
            var angle = (-Math.PI / 2) + (Math.PI * (slot + 1) / (count + 1));
            var strength = Math.Clamp(strengths.GetValueOrDefault(nodes[index].Id), 0, 100);
            var weakness = (100 - strength) / 100d;
            var target = new GraphLayoutNode(
                nodes[index].Id,
                0.08 + ((Math.Cos(angle) + 1) * (0.39 + (ring * 0.15)) * 0.5),
                Math.Sin(angle) * (0.42 + (ring * 0.12)),
                (0.86 - (ring * 0.14)) * (1 - (weakness * 0.08)),
                (0.94 - (ring * 0.22)) * (1 - (weakness * 0.14)),
                Math.Min(3, 1 + ring));
            result[nodes[index].Id] = PreserveSide(target, previousLayout);
        }
    }

    private static GraphLayoutNode PreserveSide(
        GraphLayoutNode target,
        IReadOnlyDictionary<string, GraphLayoutNode>? previousLayout)
    {
        if (previousLayout is null ||
            !previousLayout.TryGetValue(target.NodeId, out var previous) ||
            previous.Depth == 0 ||
            Math.Sign(previous.X) != Math.Sign(target.X))
        {
            return target;
        }

        return target with
        {
            Y = (target.Y * 0.72) + (previous.Y * 0.28),
        };
    }

    private static bool HasRole(ExplorerNode node, ExplorerNodeRole role) => (node.Roles & role) == role;
}
