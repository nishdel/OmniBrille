namespace OmniBrille.Core;

public sealed record ContextRenderBudget(
    int MaximumVisibleNodes,
    int MaximumStructuralEdges,
    int MaximumContextualEdges,
    int MaximumCombinedEdges,
    int MaximumContextualEdgesPerNode);

public sealed record ContextRelationshipCandidate(
    string Id,
    string SourceId,
    string TargetId,
    double Importance,
    bool TouchesFocus = false,
    bool IsSelected = false,
    ExplorerRelationshipEvidenceClass EvidenceClass = ExplorerRelationshipEvidenceClass.Derived);

/// <summary>
/// Renderer-facing limits for provider-authored contextual relationships. This policy
/// neither creates semantic relationships nor decides whether Context mode is available.
/// </summary>
public static class ContextRenderBudgetPolicy
{
    public const int DefaultVisibleNodeLimit = GraphNeighborhoodBuilder.DefaultNodeBudget;

    public static ContextRenderBudget Default { get; } = ForNodeLimit(DefaultVisibleNodeLimit);

    public static ContextRenderBudget ForNodeLimit(int nodeLimit)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(nodeLimit, 3);
        return new ContextRenderBudget(
            nodeLimit,
            nodeLimit - 1,
            (int)Math.Floor(nodeLimit * 0.75),
            (int)Math.Floor(nodeLimit * 1.75),
            3);
    }

    public static IReadOnlyList<ContextRelationshipCandidate> SelectRelationships(
        IEnumerable<ContextRelationshipCandidate> candidates,
        int visibleStructuralEdgeCount,
        ContextRenderBudget? budget = null)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentOutOfRangeException.ThrowIfNegative(visibleStructuralEdgeCount);
        budget ??= Default;
        var available = Math.Max(
            0,
            Math.Min(
                budget.MaximumContextualEdges,
                budget.MaximumCombinedEdges - visibleStructuralEdgeCount));
        var accepted = new List<ContextRelationshipCandidate>(available);
        var degree = new Dictionary<string, int>(ExplorerIdentity.Comparer);

        foreach (var candidate in candidates
                     .Where(item => !ExplorerIdentity.Equals(item.SourceId, item.TargetId))
                     .OrderByDescending(item => item.IsSelected)
                     .ThenByDescending(item => item.TouchesFocus)
                     .ThenByDescending(item => Math.Clamp(item.Importance, 0, 1))
                     .ThenBy(item => item.EvidenceClass == ExplorerRelationshipEvidenceClass.Deterministic ? 0 : 1)
                     .ThenBy(item => item.Id, ExplorerIdentity.Comparer))
        {
            if (accepted.Count >= available)
            {
                break;
            }

            var sourceDegree = degree.GetValueOrDefault(candidate.SourceId);
            var targetDegree = degree.GetValueOrDefault(candidate.TargetId);
            if (!candidate.IsSelected &&
                (sourceDegree >= budget.MaximumContextualEdgesPerNode ||
                 targetDegree >= budget.MaximumContextualEdgesPerNode))
            {
                continue;
            }

            accepted.Add(candidate);
            degree[candidate.SourceId] = sourceDegree + 1;
            degree[candidate.TargetId] = targetDegree + 1;
        }

        return accepted;
    }
}
