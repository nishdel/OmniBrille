namespace OmniBrille.Core;

/// <summary>
/// Stable focus-centric Context layout. Relationship strength selects a ring;
/// stable opaque IDs select angles. There is no continuously running physics.
/// </summary>
public sealed class ContextGraphLayout : IGraphLayoutEngine
{
    public IReadOnlyDictionary<string, GraphLayoutNode> Layout(
        ExplorerNeighborhood neighborhood,
        IReadOnlyDictionary<string, GraphLayoutNode>? previousLayout = null)
    {
        ArgumentNullException.ThrowIfNull(neighborhood);

        var result = new Dictionary<string, GraphLayoutNode>(ExplorerIdentity.Comparer)
        {
            [neighborhood.FocusNodeId] = new(neighborhood.FocusNodeId, 0, 0, 1.38, 1, 0),
        };
        var relationshipStrength = neighborhood.Edges
            .Where(edge => edge.Kind == ExplorerGraphEdgeKind.Contextual && edge.Relationship is not null)
            .SelectMany(edge => new[]
            {
                new KeyValuePair<string, int>(edge.SourceId, edge.Relationship!.Strength),
                new KeyValuePair<string, int>(edge.TargetId, edge.Relationship!.Strength),
            })
            .Where(pair => !ExplorerIdentity.Equals(pair.Key, neighborhood.FocusNodeId))
            .GroupBy(pair => pair.Key, ExplorerIdentity.Comparer)
            .ToDictionary(group => group.Key, group => group.Max(pair => pair.Value), ExplorerIdentity.Comparer);
        var related = neighborhood.Nodes
            .Where(node => !ExplorerIdentity.Equals(node.Id, neighborhood.FocusNodeId))
            .OrderByDescending(node => relationshipStrength.GetValueOrDefault(node.Id))
            .ThenBy(node => node.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(node => node.Id, ExplorerIdentity.Comparer)
            .ToArray();
        var rings = new[]
        {
            new Ring(Math.Min(10, related.Length), 0.31, 0.27, 0.88, 1, 1),
            new Ring(Math.Min(16, Math.Max(0, related.Length - 10)), 0.49, 0.42, 0.67, 0.7, 2),
            new Ring(Math.Max(0, related.Length - 26), 0.65, 0.55, 0.48, 0.38, 3),
        };
        var index = 0;
        foreach (var ring in rings)
        {
            for (var ringIndex = 0; ringIndex < ring.Count; ringIndex++, index++)
            {
                var node = related[index];
                var strength = Math.Clamp(relationshipStrength.GetValueOrDefault(node.Id), 0, 100);
                var weakness = (100 - strength) / 100d;
                var radialDepth = 1 + (weakness * 0.12);
                var deterministicOffset = StableUnit(node.Id) * (Math.PI * 2 / Math.Max(1, ring.Count)) * 0.28;
                var angle = (-Math.PI / 2) + ((Math.PI * 2 * ringIndex) / Math.Max(1, ring.Count)) + deterministicOffset;
                var target = new GraphLayoutNode(
                    node.Id,
                    Math.Cos(angle) * ring.RadiusX * radialDepth,
                    Math.Sin(angle) * ring.RadiusY * radialDepth,
                    ring.Scale * (1 - (weakness * 0.08)),
                    ring.Opacity * (1 - (weakness * 0.16)),
                    ring.Depth);
                result[node.Id] = PreserveAngle(target, previousLayout);
            }
        }

        return result;
    }

    private static GraphLayoutNode PreserveAngle(
        GraphLayoutNode target,
        IReadOnlyDictionary<string, GraphLayoutNode>? previousLayout)
    {
        if (previousLayout is null ||
            !previousLayout.TryGetValue(target.NodeId, out var previous) ||
            previous.Depth == 0 ||
            previous.Depth != target.Depth)
        {
            return target;
        }

        var radius = Math.Sqrt((target.X * target.X) + (target.Y * target.Y));
        var angle = Math.Atan2(previous.Y, previous.X);
        return target with
        {
            X = Math.Cos(angle) * radius,
            Y = Math.Sin(angle) * radius * 0.86,
        };
    }

    private static double StableUnit(string value)
    {
        unchecked
        {
            uint hash = 2166136261;
            foreach (var character in value)
            {
                hash ^= character;
                hash *= 16777619;
            }

            return hash / (double)uint.MaxValue;
        }
    }

    private sealed record Ring(
        int Count,
        double RadiusX,
        double RadiusY,
        double Scale,
        double Opacity,
        int Depth);
}
