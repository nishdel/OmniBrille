namespace OmniExplorer.Core;

public sealed record GraphLayoutNode(
    string NodeId,
    double X,
    double Y,
    double Scale,
    double Opacity);

public interface IGraphLayoutEngine
{
    public IReadOnlyDictionary<string, GraphLayoutNode> Layout(ExplorerNeighborhood neighborhood);
}

public sealed class RadialGraphLayout : IGraphLayoutEngine
{
    public IReadOnlyDictionary<string, GraphLayoutNode> Layout(ExplorerNeighborhood neighborhood)
    {
        ArgumentNullException.ThrowIfNull(neighborhood);

        var result = new Dictionary<string, GraphLayoutNode>(StringComparer.OrdinalIgnoreCase)
        {
            [neighborhood.FocusNodeId] = new(neighborhood.FocusNodeId, 0, 0, 1.22, 1),
        };

        var context = neighborhood.Nodes.FirstOrDefault(node => node.Kind == ExplorerNodeKind.Context);
        if (context is not null)
        {
            result[context.Id] = new(context.Id, -0.42, -0.68, 0.68, 0.42);
        }

        var children = neighborhood.Nodes
            .Where(node => node.Id != neighborhood.FocusNodeId && node.Kind != ExplorerNodeKind.Context)
            .ToArray();

        for (var index = 0; index < children.Length; index++)
        {
            var ring = index < 12 ? 0 : 1;
            var ringStart = ring == 0 ? 0 : 12;
            var indexInRing = index - ringStart;
            var countInRing = ring == 0
                ? Math.Min(12, children.Length)
                : children.Length - ringStart;
            var radiusX = ring == 0 ? 0.32 : 0.56;
            var radiusY = ring == 0 ? 0.27 : 0.45;
            var angle = (-Math.PI / 2) + ((Math.PI * 2 * indexInRing) / Math.Max(1, countInRing));
            var child = children[index];
            var scale = ring == 0 ? 0.82 : 0.52;
            var opacity = ring == 0 ? 0.96 : 0.42;

            result[child.Id] = new(
                child.Id,
                Math.Cos(angle) * radiusX,
                Math.Sin(angle) * radiusY,
                scale,
                opacity);
        }

        return result;
    }
}
