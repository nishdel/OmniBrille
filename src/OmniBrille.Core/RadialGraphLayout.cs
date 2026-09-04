namespace OmniBrille.Core;

public readonly record struct GraphLayoutNode(
    string NodeId,
    double X,
    double Y,
    double Scale,
    double Opacity,
    int Depth = 1,
    int PresentationBand = 0);

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
            result[context.Id] = new(context.Id, -0.5, -0.7, 0.62, 0.42, 2, 3);
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

                var intendedBand = slots[childIndex].PresentationBand;
                var nearestSlot = FindNearestAvailableSlot(previous, slots, occupiedSlots, intendedBand);
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
                    slot.Depth,
                    slot.PresentationBand);
            }
        }

        for (var childIndex = 0; childIndex < children.Length; childIndex++)
        {
            var child = children[childIndex];
            if (result.ContainsKey(child.Id))
            {
                continue;
            }

            var intendedBand = slots[childIndex].PresentationBand;
            var slotIndex = Enumerable.Range(0, slots.Length).First(index =>
                !occupiedSlots.Contains(index) && slots[index].PresentationBand == intendedBand);
            occupiedSlots.Add(slotIndex);
            var slot = slots[slotIndex];
            result[child.Id] = new(child.Id, slot.X, slot.Y, slot.Scale, slot.Opacity, slot.Depth, slot.PresentationBand);
        }

        return result;
    }

    private static GraphSlot[] CreateSlots(int count)
    {
        var slots = new List<GraphSlot>(count);
        // Every admitted structural item is a direct child on one semantic focus plane.
        // Rings are collision/density bands only; they never represent filesystem depth.
        var innerCount = Math.Min(12, count);
        AddRing(slots, innerCount, 0.29, 0.27, 0.86, 1, 1, 1, -Math.PI / 2);

        var middleCount = Math.Min(16, count - slots.Count);
        AddRing(slots, middleCount, 0.49, 0.43, 0.7, 0.88, 1, 2, (-Math.PI / 2) + 0.14);

        var outerCount = count - slots.Count;
        AddRing(slots, outerCount, 0.68, 0.58, 0.56, 0.7, 1, 3, (-Math.PI / 2) + 0.08);
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
        int presentationBand,
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
                depth,
                presentationBand));
        }
    }

    private static int FindNearestAvailableSlot(
        GraphLayoutNode previous,
        GraphSlot[] slots,
        HashSet<int> occupiedSlots,
        int intendedBand)
    {
        return Enumerable.Range(0, slots.Length)
            .Where(index => !occupiedSlots.Contains(index) && slots[index].PresentationBand == intendedBand)
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
        int Depth,
        int PresentationBand);
}
