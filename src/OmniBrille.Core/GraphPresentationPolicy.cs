namespace OmniBrille.Core;

public enum GraphLevelOfDetail
{
    Point,
    Glyph,
    Labeled,
    Focused,
}

public sealed record GraphPresentationContext(
    double Zoom,
    int SceneNodeCount,
    string FocusNodeId,
    string? SelectedNodeId,
    string? HoveredNodeId,
    IReadOnlySet<string> HighlightedNodeIds,
    bool SearchActive,
    bool ReducedEffects);

public sealed record GraphNodePresentation(
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

public sealed record LabelCandidate(
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
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(context);

        var isFocus = EqualsId(node.Id, context.FocusNodeId);
        var isSelected = EqualsId(node.Id, context.SelectedNodeId);
        var isHovered = EqualsId(node.Id, context.HoveredNodeId);
        var isHighlighted = context.HighlightedNodeIds.Contains(node.Id);
        var labelPriority = isFocus ? 1_000
            : isSelected ? 920
            : isHighlighted ? 860
            : isHovered ? 820
            : node.Kind == ExplorerNodeKind.Aggregate ? 680
            : layout.Depth == 1 && node.Kind == ExplorerNodeKind.Folder ? 560
            : layout.Depth == 1 ? 500
            : node.Kind == ExplorerNodeKind.Context ? 260
            : 340;
        var required = isFocus || isSelected || isHighlighted || isHovered;
        var effectiveScale = layout.Scale * Math.Clamp(context.Zoom, 0.5, 2.4);
        var densityPenalty = context.SceneNodeCount > 36 ? 0.1 : 0;

        var level = required || isFocus
            ? GraphLevelOfDetail.Focused
            : effectiveScale >= 0.7 + densityPenalty
                ? GraphLevelOfDetail.Labeled
                : effectiveScale >= 0.34
                    ? GraphLevelOfDetail.Glyph
                    : GraphLevelOfDetail.Point;

        if (node.Kind == ExplorerNodeKind.Aggregate && level < GraphLevelOfDetail.Labeled)
        {
            level = GraphLevelOfDetail.Labeled;
        }

        var unrelatedSearchNode = context.SearchActive &&
            !isFocus &&
            !isSelected &&
            !isHighlighted;
        var opacityMultiplier = unrelatedSearchNode ? 0.3 : 1;
        if (node.Kind == ExplorerNodeKind.Context)
        {
            opacityMultiplier *= 0.72;
        }

        var glowMultiplier = context.ReducedEffects ? 0.28 : required || isFocus ? 1 : 0.62;
        var edgeMultiplier = unrelatedSearchNode ? 0.35 : isHighlighted ? 1.3 : 1;
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
                     .ThenBy(item => item.NodeId, StringComparer.OrdinalIgnoreCase))
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

        return accepted.Select(item => item.NodeId).ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public static int RecommendedLabelBudget(double zoom, int sceneNodeCount)
    {
        if (zoom < 0.7)
        {
            return Math.Min(10, sceneNodeCount);
        }

        if (zoom > 1.35)
        {
            return Math.Min(34, sceneNodeCount);
        }

        return Math.Min(sceneNodeCount <= 24 ? 24 : 22, sceneNodeCount);
    }

    private static bool EqualsId(string left, string? right) =>
        right is not null && StringComparer.OrdinalIgnoreCase.Equals(left, right);
}
