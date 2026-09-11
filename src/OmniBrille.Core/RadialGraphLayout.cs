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
            .Where(node => node.Id != neighborhood.FocusNodeId && node.Kind != ExplorerNodeKind.Context &&
                (node.Roles & ExplorerNodeRole.DescendantPreview) == 0)
            .OrderBy(node => node.Kind == ExplorerNodeKind.Aggregate ? 0 : node.Kind == ExplorerNodeKind.Folder ? 1 : 2)
            .ThenBy(PresentationGroup, StringComparer.Ordinal)
            .ThenBy(node => node.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(node => node.Name, StringComparer.Ordinal)
            .ThenBy(node => node.Id, ExplorerIdentity.Comparer)
            .ToArray();
        var slots = CreateSlots(children);
        var occupiedSlots = new HashSet<int>();

        if (previousLayout is not null)
        {
            var assignments = AssignSurvivingSlots(children, slots, previousLayout);
            for (var childIndex = 0; childIndex < children.Length; childIndex++)
            {
                var child = children[childIndex];
                var nearestSlot = assignments[childIndex];
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
                !occupiedSlots.Contains(index) && slots[index].PresentationBand == intendedBand &&
                slots[index].Group == PresentationGroup(child));
            occupiedSlots.Add(slotIndex);
            var slot = slots[slotIndex];
            result[child.Id] = new(child.Id, slot.X, slot.Y, slot.Scale, slot.Opacity, slot.Depth, slot.PresentationBand);
        }

        PlacePreviews(neighborhood, result);
        return result;
    }

    private static void PlacePreviews(ExplorerNeighborhood neighborhood, Dictionary<string, GraphLayoutNode> result)
    {
        var previews = neighborhood.Nodes
            .Where(node => (node.Roles & ExplorerNodeRole.DescendantPreview) != 0)
            .OrderBy(node => node.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(node => node.Id, ExplorerIdentity.Comparer)
            .GroupBy(node => neighborhood.Edges.FirstOrDefault(edge =>
                edge.Kind == ExplorerGraphEdgeKind.Structural && ExplorerIdentity.Equals(edge.TargetId, node.Id))?.SourceId);
        foreach (var group in previews)
        {
            if (group.Key is null || !result.TryGetValue(group.Key, out var parent))
            {
                continue;
            }

            var siblings = group.ToArray();
            var length = Math.Max(0.01, Math.Sqrt((parent.X * parent.X) + (parent.Y * parent.Y)));
            var outwardX = parent.X / length;
            var outwardY = parent.Y / length;
            for (var index = 0; index < siblings.Length; index++)
            {
                var child = siblings[index];
                var outwardDistance = 0.15 + (StableUnit(child.Id + "\u001fpreview") * 0.035);
                var sideDistance = (index - ((siblings.Length - 1) / 2d)) * 0.095;
                var preferredX = Math.Clamp(parent.X + (outwardX * outwardDistance) - (outwardY * sideDistance), -0.7, 0.7);
                var preferredY = Math.Clamp(parent.Y + (outwardY * outwardDistance) + (outwardX * sideDistance), -0.62, 0.62);
                var position = FindPreviewPosition(parent, preferredX, preferredY, result.Values);
                result[child.Id] = new GraphLayoutNode(
                    child.Id,
                    position.X,
                    position.Y,
                    0.43,
                    0.46,
                    2,
                    4);
            }
        }
    }

    private static (double X, double Y) FindPreviewPosition(
        GraphLayoutNode parent, double preferredX, double preferredY,
        Dictionary<string, GraphLayoutNode>.ValueCollection occupied)
    {
        // Match the renderer's effective targets, including the 44-DIP floor, at
        // the smallest supported window. Larger windows only increase separation.
        const double canvasWidth = (820 - 40) * 0.66;
        const double canvasHeight = (520 - 240) * 0.68;
        const double previewHalfWidth = 22;
        const double previewHalfHeight = 22;
        bool IsFree(double x, double y)
        {
            foreach (var node in occupied)
            {
                var halfWidth = Math.Max(22, (25 * node.Scale) + 10);
                var halfHeight = Math.Max(22, (19 * node.Scale) + 10);
                if (Math.Abs(x - node.X) * canvasWidth < previewHalfWidth + halfWidth + 4 &&
                    Math.Abs(y - node.Y) * canvasHeight < previewHalfHeight + halfHeight + 4)
                {
                    return false;
                }
            }

            return true;
        }

        if (IsFree(preferredX, preferredY))
        {
            return (preferredX, preferredY);
        }

        var best = (X: preferredX, Y: preferredY);
        var bestScore = double.PositiveInfinity;
        // First retain the whole target inside the minimum viewport. The second
        // bounded perimeter is a last resort for an unusually crowded parent sector.
        foreach (var bounds in new[] { (X: 0.7, Y: 0.62), (X: 0.74, Y: 0.68) })
        {
            for (var row = 0; row <= (int)Math.Round(bounds.Y * 100); row++)
            {
                var y = -bounds.Y + (row * 0.02);
                for (var column = 0; column <= (int)Math.Round(bounds.X * 100); column++)
                {
                    var x = -bounds.X + (column * 0.02);
                    var dx = (x - preferredX) * canvasWidth;
                    var dy = (y - preferredY) * canvasHeight;
                    var inward = Math.Max(0, ((parent.X - x) * parent.X) + ((parent.Y - y) * parent.Y));
                    var score = (dx * dx) + (dy * dy) + (inward * 40_000);
                    if (score < bestScore && IsFree(x, y))
                    {
                        best = (x, y);
                        bestScore = score;
                    }
                }
            }

            if (double.IsFinite(bestScore))
            {
                return best;
            }
        }

        // The bounded 48-node radial scene leaves free perimeter slots, but retain
        // an ID/layout pair even for an externally supplied overfull scene.
        return best;
    }

    private static GraphSlot[] CreateSlots(ExplorerNode[] children)
    {
        var count = children.Length;
        var slots = new List<GraphSlot>(count);
        // Every admitted structural item is a direct child on one semantic focus plane.
        // Rings are collision/density bands only; they never represent filesystem depth.
        var innerCount = Math.Min(12, count);
        AddRing(slots, children[..innerCount], 0.29, 0.27, 0.86, 1, 1, -Math.PI / 2);

        var middleCount = Math.Min(16, count - slots.Count);
        AddRing(slots, children[innerCount..(innerCount + middleCount)], 0.49, 0.43, 0.7, 0.88, 2, (-Math.PI / 2) + 0.14);

        AddRing(slots, children[(innerCount + middleCount)..], 0.68, 0.58, 0.56, 0.7, 3, (-Math.PI / 2) + 0.08);
        return [.. slots];
    }

    private static void AddRing(
        List<GraphSlot> slots,
        ExplorerNode[] children,
        double radiusX,
        double radiusY,
        double scale,
        double opacity,
        int presentationBand,
        double startAngle)
    {
        var count = children.Length;
        if (count == 0)
        {
            return;
        }

        var groups = children.Select(PresentationGroup).ToArray();
        var gaps = Enumerable.Range(0, count)
            .Select(index => groups[index] == groups[(index + 1) % count] ? 1d : 1.7)
            .ToArray();
        var anglePerGap = Math.PI * 2 / gaps.Sum();
        var angles = new double[count];
        var angle = startAngle;
        for (var index = 0; index < count; index++)
        {
            angles[index] = angle + ((StableUnit(children[index].Id) - 0.5) * Math.Min(0.24, anglePerGap * 0.3));
            angle += gaps[index] * anglePerGap;
        }

        // When a file group straddles bands, keep its sectors facing each other.
        // A full ring of one group already has no unlike files to separate.
        var sharedGroup = groups.Distinct(StringComparer.Ordinal)
            .Where(group => group.StartsWith("file:", StringComparison.Ordinal) &&
                groups.Count(value => value == group) < count && slots.Any(slot => slot.Group == group))
            .OrderByDescending(group => Math.Min(groups.Count(value => value == group), slots.Count(slot => slot.Group == group)))
            .ThenBy(group => group, StringComparer.Ordinal)
            .FirstOrDefault();
        if (sharedGroup is not null)
        {
            var previousAngles = slots.Where(slot => slot.Group == sharedGroup).Select(slot => slot.Angle).ToArray();
            var currentAngles = angles.Where((_, index) => groups[index] == sharedGroup).ToArray();
            var rotation = MeanAngle(previousAngles) - MeanAngle(currentAngles);
            for (var index = 0; index < count; index++)
            {
                angles[index] += rotation;
            }
        }

        for (var index = 0; index < count; index++)
        {
            var radialVariation = 0.92 + (StableUnit(children[index].Id + "\u001fradius") * 0.16);
            slots.Add(new GraphSlot(
                Math.Cos(angles[index]) * radiusX * radialVariation,
                Math.Sin(angles[index]) * radiusY * radialVariation,
                scale,
                opacity,
                1,
                presentationBand,
                groups[index],
                angles[index]));
        }
    }

    private static string PresentationGroup(ExplorerNode node)
    {
        if (node.Kind != ExplorerNodeKind.File)
        {
            return node.Kind.ToString();
        }

        // This is visual sorting of the supplied name only: no path inspection, file
        // association lookup, content inference, or semantic relationship is created.
        var separator = node.Name.LastIndexOf('.');
        var extension = separator > 0 && separator < node.Name.Length - 1
            ? node.Name[(separator + 1)..].ToLowerInvariant()
            : string.Empty;
        return extension switch
        {
            "txt" or "md" or "rtf" or "pdf" or "doc" or "docx" or "odt" or
            "xls" or "xlsx" or "ods" or "csv" or "tsv" or "ppt" or "pptx" or "odp" or
            "png" or "jpg" or "jpeg" or "gif" or "bmp" or "tif" or "tiff" or "webp" or "svg" or "ico" or "heic" or
            "mp3" or "wav" or "flac" or "aac" or "ogg" or "m4a" or
            "mp4" or "mkv" or "mov" or "avi" or "webm" or "wmv" or
            "zip" or "7z" or "rar" or "tar" or "gz" or "bz2" or "xz" or
            "cs" or "fs" or "vb" or "js" or "jsx" or "ts" or "tsx" or "py" or "java" or "c" or "cpp" or "h" or "rs" or "go" or
            "html" or "css" or "json" or "xml" or "yaml" or "yml" or "toml" or "sql" or "ini" or "log" or
            "exe" or "dll" or "msi" or "ps1" or "bat" or "cmd" or "sh" => "file:" + extension,
            _ => "file:unknown",
        };
    }

    private static double MeanAngle(double[] angles) => Math.Atan2(angles.Sum(Math.Sin), angles.Sum(Math.Cos));

    private static double StableUnit(string value)
    {
        unchecked
        {
            uint hash = 2166136261;
            foreach (var character in value)
            {
                hash = (hash ^ character) * 16777619;
            }

            hash ^= hash >> 16;
            hash *= 0x85ebca6b;
            hash ^= hash >> 13;
            hash *= 0xc2b2ae35;
            hash ^= hash >> 16;

            return hash / (double)uint.MaxValue;
        }
    }

    private static int[] AssignSurvivingSlots(
        ExplorerNode[] children,
        GraphSlot[] slots,
        IReadOnlyDictionary<string, GraphLayoutNode> previousLayout)
    {
        var survivors = Enumerable.Range(0, children.Length)
            .Where(index => previousLayout.TryGetValue(children[index].Id, out var previous) && previous.Depth != 0)
            .ToArray();
        var assignments = Enumerable.Repeat(-1, children.Length).ToArray();
        var rowPotential = new double[survivors.Length + 1];
        var columnPotential = new double[slots.Length + 1];
        var matchedRow = new int[slots.Length + 1];
        var predecessor = new int[slots.Length + 1];

        // Bounded minimum-cost assignment avoids a greedy refresh stranding the last
        // survivor across the scene. Cross-band/group costs exceed every legal distance.
        for (var row = 1; row <= survivors.Length; row++)
        {
            matchedRow[0] = row;
            var column = 0;
            var minimum = Enumerable.Repeat(double.PositiveInfinity, slots.Length + 1).ToArray();
            var used = new bool[slots.Length + 1];
            do
            {
                used[column] = true;
                var childIndex = survivors[matchedRow[column] - 1];
                var previous = previousLayout[children[childIndex].Id];
                var intended = slots[childIndex];
                var delta = double.PositiveInfinity;
                var nextColumn = 0;
                for (var candidate = 1; candidate <= slots.Length; candidate++)
                {
                    if (used[candidate])
                    {
                        continue;
                    }

                    var slot = slots[candidate - 1];
                    var cost = slot.PresentationBand == intended.PresentationBand && slot.Group == intended.Group
                        ? DistanceSquared(previous, slot)
                        : 1_000;
                    var reducedCost = cost - rowPotential[matchedRow[column]] - columnPotential[candidate];
                    if (reducedCost < minimum[candidate])
                    {
                        minimum[candidate] = reducedCost;
                        predecessor[candidate] = column;
                    }

                    if (minimum[candidate] < delta)
                    {
                        delta = minimum[candidate];
                        nextColumn = candidate;
                    }
                }

                for (var candidate = 0; candidate <= slots.Length; candidate++)
                {
                    if (used[candidate])
                    {
                        rowPotential[matchedRow[candidate]] += delta;
                        columnPotential[candidate] -= delta;
                    }
                    else
                    {
                        minimum[candidate] -= delta;
                    }
                }

                column = nextColumn;
            }
            while (matchedRow[column] != 0);

            do
            {
                var previousColumn = predecessor[column];
                matchedRow[column] = matchedRow[previousColumn];
                column = previousColumn;
            }
            while (column != 0);
        }

        for (var column = 1; column <= slots.Length; column++)
        {
            if (matchedRow[column] > 0)
            {
                assignments[survivors[matchedRow[column] - 1]] = column - 1;
            }
        }

        return assignments;
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
        int PresentationBand,
        string Group,
        double Angle);
}
