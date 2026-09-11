namespace OmniBrille.Core;

public enum GraphLevelOfDetail
{
    Point,
    Glyph,
    Labeled,
    Focused,
}

public readonly record struct GraphPresentationContext(
    double Zoom,
    int SceneNodeCount,
    string FocusNodeId,
    string? SelectedNodeId,
    string? HoveredNodeId,
    IReadOnlySet<string> HighlightedNodeIds,
    bool SearchActive,
    bool ReducedEffects,
    double TextScale = 1);

public readonly record struct GraphNodePresentation(
    GraphLevelOfDetail LevelOfDetail,
    int LabelPriority,
    bool LabelIsRequired,
    double OpacityMultiplier,
    double GlowMultiplier,
    double EdgeMultiplier);

public readonly record struct LabelBox(double X, double Y, double Width, double Height)
{
    public bool Intersects(LabelBox other, double padding)
    {
        var left = X - padding;
        var top = Y - padding;
        var right = X + Width + padding;
        var bottom = Y + Height + padding;
        return left < other.X + other.Width &&
            right > other.X &&
            top < other.Y + other.Height &&
            bottom > other.Y;
    }
}

public readonly record struct LabelCandidate(
    string NodeId,
    LabelBox Bounds,
    int Priority,
    bool IsRequired = false);

public static class GraphPresentationPolicy
{
    public static GraphNodePresentation Evaluate(
        ExplorerNode node,
        GraphLayoutNode layout,
        GraphPresentationContext context)
    {
        ArgumentNullException.ThrowIfNull(node);

        var isFocus = EqualsId(node.Id, context.FocusNodeId);
        var isSelected = EqualsId(node.Id, context.SelectedNodeId);
        var isHovered = EqualsId(node.Id, context.HoveredNodeId);
        var isHighlighted = context.HighlightedNodeIds.Contains(node.Id);
        var labelPriority = isFocus ? 1_000
            : isSelected ? 920
            : isHighlighted ? 860
            : isHovered ? 820
            : node.Kind == ExplorerNodeKind.Aggregate ? 680
            : layout.PresentationBand == 1 && node.Kind == ExplorerNodeKind.Folder ? 560
            : layout.PresentationBand == 1 ? 500
            : node.Kind == ExplorerNodeKind.Context ? 260
            : 340;
        var required = isFocus || isSelected || isHighlighted || isHovered;
        var effectiveScale = layout.Scale * Math.Clamp(context.Zoom, 0.5, 2.4);
        var densityPenalty = (context.SceneNodeCount > 36 ? 0.1 : 0) +
            (Math.Clamp(context.TextScale, 1, 2) - 1) * 0.16;

        var level = required || isFocus
            ? GraphLevelOfDetail.Focused
            : effectiveScale >= 0.7 + densityPenalty
                ? GraphLevelOfDetail.Labeled
                : effectiveScale >= 0.34
                    ? GraphLevelOfDetail.Glyph
                    : GraphLevelOfDetail.Point;

        // Every admitted item retains its name. Zoom changes type size and placement,
        // not an arbitrary subset of identifiable files (#8).
        if (level < GraphLevelOfDetail.Labeled)
        {
            level = GraphLevelOfDetail.Labeled;
        }

        var emphasized = isFocus || isSelected || isHighlighted || isHovered;
        var unrelatedSearchNode = context.SearchActive && !emphasized;
        var hierarchyMultiplier = emphasized
            ? Math.Min(2.5, 1 / Math.Max(0.4, layout.Opacity))
            : layout.PresentationBand switch
            {
                <= 1 => 1,
                2 => 0.82,
                _ => 0.58,
            };
        var opacityMultiplier = unrelatedSearchNode ? hierarchyMultiplier * 0.3 : hierarchyMultiplier;
        if (node.Kind == ExplorerNodeKind.Context)
        {
            opacityMultiplier *= 0.72;
        }

        var glowMultiplier = context.ReducedEffects
            ? emphasized ? 0.4 : 0.22
            : isFocus ? 1.2
            : required ? 1
            : layout.PresentationBand <= 1 ? 0.62 : 0.42;
        var edgeMultiplier = isSelected || isHighlighted || isHovered
            ? 1.3
            : layout.PresentationBand switch
            {
                <= 1 => 1,
                2 => 0.48,
                _ => 0.16,
            };
        if (unrelatedSearchNode)
        {
            edgeMultiplier = Math.Min(edgeMultiplier, 0.35);
        }

        return new GraphNodePresentation(
            level,
            labelPriority,
            required,
            opacityMultiplier,
            glowMultiplier,
            edgeMultiplier);
    }

    public static IReadOnlySet<string> ResolveLabels(
        IEnumerable<LabelCandidate> candidates,
        int maximumLabels,
        double padding = 4)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumLabels, 0);

        var accepted = new List<LabelCandidate>();
        foreach (var candidate in candidates
                     .OrderByDescending(item => item.IsRequired)
                     .ThenByDescending(item => item.Priority)
                     .ThenBy(item => item.NodeId, ExplorerIdentity.Comparer))
        {
            if (!candidate.IsRequired && accepted.Count >= maximumLabels)
            {
                continue;
            }

            if (!candidate.IsRequired && accepted.Any(existing => candidate.Bounds.Intersects(existing.Bounds, padding)))
            {
                continue;
            }

            accepted.Add(candidate);
        }

        return accepted.Select(item => item.NodeId).ToHashSet(ExplorerIdentity.Comparer);
    }

    public static int RecommendedLabelBudget(double zoom, int sceneNodeCount, double textScale = 1)
    {
        return Math.Clamp(sceneNodeCount, 0, GraphNeighborhoodBuilder.DefaultNodeBudget);
    }

    /// <summary>Moves colliding labels to nearby free space instead of silently hiding names.</summary>
    public static IReadOnlyDictionary<string, LabelBox> PlaceLabels(
        IEnumerable<LabelCandidate> candidates, LabelBox viewport,
        IReadOnlyList<LabelBox> glyphs, double padding = 3)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(glyphs);
        var placed = new Dictionary<string, LabelBox>(ExplorerIdentity.Comparer);
        foreach (var candidate in candidates.OrderByDescending(item => item.Priority).ThenBy(item => item.NodeId, ExplorerIdentity.Comparer))
        {
            var original = candidate.Bounds;
            var width = Math.Min(original.Width, viewport.Width);
            var height = Math.Min(original.Height, viewport.Height);
            LabelBox At(double x, double y) => new(
                Math.Clamp(x, viewport.X, viewport.X + viewport.Width - width),
                Math.Clamp(y, viewport.Y, viewport.Y + viewport.Height - height), width, height);
            bool Free(LabelBox box)
            {
                for (var index = 0; index < glyphs.Count; index++)
                {
                    if (box.Intersects(glyphs[index], padding)) { return false; }
                }
                foreach (var existing in placed.Values)
                {
                    if (box.Intersects(existing, padding)) { return false; }
                }
                return true;
            }

            var chosen = At(original.X, original.Y);
            if (!Free(chosen))
            {
                var found = false;
                // Search outward in bounded screen-space rings; the first clear slot
                // stays near its glyph. This also handles large text and zoomed-out scenes.
                var limit = (int)Math.Ceiling(Math.Max(viewport.Width, viewport.Height) / 18);
                for (var ring = 1; ring <= limit && !found; ring++)
                {
                    for (var side = 0; side < 16; side++)
                    {
                        var angle = -Math.PI / 2 + side * Math.PI / 8;
                        var proposal = At(original.X + Math.Cos(angle) * ring * 18, original.Y + Math.Sin(angle) * ring * 18);
                        if (Free(proposal)) { chosen = proposal; found = true; break; }
                    }
                }
            }
            // At an exceptionally constrained viewport retain the name; the synchronized
            // list remains the full-size reading surface. Never drop a random subset.
            placed[candidate.NodeId] = chosen;
        }
        return placed;
    }

    private static bool EqualsId(string left, string? right) =>
        right is not null && ExplorerIdentity.Equals(left, right);
}
