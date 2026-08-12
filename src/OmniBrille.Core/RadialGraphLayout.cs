namespace OmniBrille.Core;

public sealed record GraphLayoutNode(
    string NodeId,
    double X,
    double Y,
    double Scale,
    double Opacity,
    int Depth = 1);

public interface IGraphLayoutEngine
{
    public IReadOnlyDictionary<string, GraphLayoutNode> Layout(
        ExplorerNeighborhood neighborhood,
        IReadOnlyDictionary<string, GraphLayoutNode>? previousLayout = null);
}

public sealed class RadialGraphLayout : IGraphLayoutEngine
{
    public IReadOnlyDictionary<string, GraphLayoutNode> Layout(
        ExplorerNeighborhood neighborhood,
        IReadOnlyDictionary<string, GraphLayoutNode>? previousLayout = null)
    {
        ArgumentNullException.ThrowIfNull(neighborhood);

        var result = new Dictionary<string, GraphLayoutNode>(ExplorerIdentity.Comparer)
        {
            [neighborhood.FocusNodeId] = new(neighborhood.FocusNodeId, 0, 0, 1.34, 1, 0),
        };

        var context = neighborhood.Nodes.FirstOrDefault(node => node.Kind == ExplorerNodeKind.Context);
        if (context is not null)
        {
            result[context.Id] = new(context.Id, -0.48, -0.72, 0.58, 0.3, 3);
        }

        var children = neighborhood.Nodes
            .Where(node => node.Id != neighborhood.FocusNodeId && node.Kind != ExplorerNodeKind.Context)
            .OrderBy(node => node.Kind == ExplorerNodeKind.Aggregate ? 0 : node.Kind == ExplorerNodeKind.Folder ? 1 : 2)
            .ThenBy(node => node.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(node => node.Name, StringComparer.Ordinal)
            .ThenBy(node => node.Id, ExplorerIdentity.Comparer)
            .ToArray();
        var slots = CreateSlots(children.Length);
        var occupiedSlots = new HashSet<int>();

        if (previousLayout is not null)
        {
            for (var childIndex = 0; childIndex < children.Length; childIndex++)
            {
                var child = children[childIndex];
                if (!previousLayout.TryGetValue(child.Id, out var previous) || previous.Depth == 0)
                {
                    continue;
                }

                var intendedDepth = slots[childIndex].Depth;
                var nearestSlot = FindNearestAvailableSlot(previous, slots, occupiedSlots, intendedDepth);
                if (nearestSlot < 0)
                {
                    continue;
                }

                occupiedSlots.Add(nearestSlot);
                var slot = slots[nearestSlot];
                result[child.Id] = new(
                    child.Id,
                    slot.X,
                    slot.Y,
                    slot.Scale,
                    slot.Opacity,
                    slot.Depth);
            }
        }

        for (var childIndex = 0; childIndex < children.Length; childIndex++)
        {
            var child = children[childIndex];
            if (result.ContainsKey(child.Id))
            {
                continue;
            }

            var intendedDepth = slots[childIndex].Depth;
            var slotIndex = Enumerable.Range(0, slots.Length).First(index =>
                !occupiedSlots.Contains(index) && slots[index].Depth == intendedDepth);
            occupiedSlots.Add(slotIndex);
            var slot = slots[slotIndex];
            result[child.Id] = new(child.Id, slot.X, slot.Y, slot.Scale, slot.Opacity, slot.Depth);
        }

        return result;
    }

    private static GraphSlot[] CreateSlots(int count)
    {
        var slots = new List<GraphSlot>(count);
        var innerCount = Math.Min(12, count);
        AddRing(slots, innerCount, 0.31, 0.28, 0.84, 0.98, 1, -Math.PI / 2);

        var middleCount = Math.Min(18, count - slots.Count);
        AddRing(slots, middleCount, 0.49, 0.43, 0.64, 0.68, 2, (-Math.PI / 2) + 0.14);

        var outerCount = count - slots.Count;
        AddRing(slots, outerCount, 0.65, 0.56, 0.46, 0.34, 3, (-Math.PI / 2) + 0.08);
        return [.. slots];
    }

    private static void AddRing(
        List<GraphSlot> slots,
        int count,
        double radiusX,
        double radiusY,
        double scale,
        double opacity,
        int depth,
        double startAngle)
    {
        for (var index = 0; index < count; index++)
        {
            var angle = startAngle + ((Math.PI * 2 * index) / Math.Max(1, count));
            slots.Add(new GraphSlot(
                Math.Cos(angle) * radiusX,
                Math.Sin(angle) * radiusY,
                scale,
                opacity,
                depth));
        }
    }

    private static int FindNearestAvailableSlot(
        GraphLayoutNode previous,
        GraphSlot[] slots,
        HashSet<int> occupiedSlots,
        int intendedDepth)
    {
        return Enumerable.Range(0, slots.Length)
            .Where(index => !occupiedSlots.Contains(index) && slots[index].Depth == intendedDepth)
            .OrderBy(index => DistanceSquared(previous, slots[index]))
            .ThenBy(index => index)
            .DefaultIfEmpty(-1)
            .First();
    }

    private static double DistanceSquared(GraphLayoutNode node, GraphSlot slot)
    {
        var x = node.X - slot.X;
        var y = node.Y - slot.Y;
        return (x * x) + (y * y);
    }

    private sealed record GraphSlot(
        double X,
        double Y,
        double Scale,
        double Opacity,
        int Depth);
}
