namespace OmniBrille.Core;

/// <summary>
/// Applies OmniBrille's renderer contract to provider-authored Context data. It
/// prioritizes relationships but never infers or creates semantic relationships.
/// </summary>
public sealed class ContextNeighborhoodBuilder
{
    private readonly ContextRenderBudget _budget;

    public ContextNeighborhoodBuilder(ContextRenderBudget? budget = null) =>
        _budget = budget ?? ContextRenderBudgetPolicy.Default;

    public ExplorerNeighborhood Build(
        ExplorerContextSnapshot snapshot,
        string? selectedRelationshipId = null) =>
        BuildDetailed(snapshot, ContextFilter.None, selectedRelationshipId).Neighborhood;

    public ContextNeighborhoodBuildResult BuildDetailed(
        ExplorerContextSnapshot snapshot,
        ContextFilter? filter = null,
        string? selectedRelationshipId = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        filter = (filter ?? ContextFilter.None).Normalize();

        var sourceNodes = snapshot.Nodes
            .Prepend(snapshot.Focus)
            .GroupBy(node => node.Id, ExplorerIdentity.Comparer)
            .Select(group => group.First())
            .ToDictionary(node => node.Id, ExplorerIdentity.Comparer);
        var validRelationships = snapshot.Relationships
            .Where(relationship =>
                sourceNodes.ContainsKey(relationship.SourceId) &&
                sourceNodes.ContainsKey(relationship.TargetId) &&
                !ExplorerIdentity.Equals(relationship.SourceId, relationship.TargetId))
            .GroupBy(relationship => relationship.Id, ExplorerIdentity.Comparer)
            .Select(group => group.First())
            .ToArray();
        var relationships = validRelationships
            .Where(filter.Matches)
            .OrderByDescending(relationship => ExplorerIdentity.Equals(relationship.Id, selectedRelationshipId))
            .ThenByDescending(relationship => TouchesFocus(relationship, snapshot.Focus.Id))
            .ThenByDescending(relationship => relationship.Strength)
            .ThenBy(relationship => relationship.EvidenceClass == ExplorerRelationshipEvidenceClass.Deterministic ? 0 : 1)
            .ThenBy(relationship => relationship.Id, ExplorerIdentity.Comparer)
            .ToArray();

        var candidateStructuralEdges = snapshot.StructuralEdges
            .Where(edge =>
                sourceNodes.ContainsKey(edge.SourceId) &&
                sourceNodes.ContainsKey(edge.TargetId) &&
                !ExplorerIdentity.Equals(edge.SourceId, edge.TargetId))
            .Take(_budget.MaximumStructuralEdges)
            .Select(edge => edge with { Kind = ExplorerGraphEdgeKind.Structural, Relationship = null })
            .ToArray();
        var candidates = relationships
            .Select(relationship => new ContextRelationshipCandidate(
                relationship.Id,
                relationship.SourceId,
                relationship.TargetId,
                relationship.Strength / 100d,
                TouchesFocus(relationship, snapshot.Focus.Id),
                ExplorerIdentity.Equals(relationship.Id, selectedRelationshipId),
                relationship.EvidenceClass))
            .ToArray();
        var selectedIds = ContextRenderBudgetPolicy
            .SelectRelationships(candidates, candidateStructuralEdges.Length, _budget)
            .Select(candidate => candidate.Id)
            .ToHashSet(ExplorerIdentity.Comparer);
        var selectedRelationships = relationships
            .Where(relationship => selectedIds.Contains(relationship.Id))
            .ToArray();
        var prioritizedNodeIds = selectedRelationships
            .SelectMany(relationship => new[] { relationship.SourceId, relationship.TargetId })
            .Concat(candidateStructuralEdges.SelectMany(edge => new[] { edge.SourceId, edge.TargetId }))
            .Where(id => !ExplorerIdentity.Equals(id, snapshot.Focus.Id))
            .Distinct(ExplorerIdentity.Comparer)
            .Take(_budget.MaximumVisibleNodes - 1)
            .ToHashSet(ExplorerIdentity.Comparer);
        prioritizedNodeIds.Add(snapshot.Focus.Id);

        var nodes = sourceNodes.Values
            .Where(node => prioritizedNodeIds.Contains(node.Id))
            .OrderBy(node => ExplorerIdentity.Equals(node.Id, snapshot.Focus.Id) ? 0 : 1)
            .ThenBy(node => node.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(node => node.Id, ExplorerIdentity.Comparer)
            .Select(entry => ExplorerNode.FromEntry(entry) with { IsNavigable = true })
            .ToArray();
        var structuralEdges = candidateStructuralEdges
            .Where(edge =>
                prioritizedNodeIds.Contains(edge.SourceId) &&
                prioritizedNodeIds.Contains(edge.TargetId))
            .ToArray();
        var contextualEdges = selectedRelationships
            .Where(relationship =>
                prioritizedNodeIds.Contains(relationship.SourceId) &&
                prioritizedNodeIds.Contains(relationship.TargetId))
            .Select(relationship => new ExplorerEdge(
                relationship.SourceId,
                relationship.TargetId,
                ExplorerGraphEdgeKind.Contextual,
                relationship))
            .ToArray();

        var hiddenNodes = Math.Max(0, sourceNodes.Count - nodes.Length);
        var warning = snapshot.Warning;
        if (relationships.Length > contextualEdges.Length)
        {
            var bounded = $"Showing {contextualEdges.Length:N0} of {relationships.Length:N0} matching authoritative relationships.";
            warning = string.IsNullOrWhiteSpace(warning) ? bounded : $"{warning} {bounded}";
        }

        var neighborhood = new ExplorerNeighborhood(
            snapshot.Focus.Id,
            nodes,
            [.. structuralEdges, .. contextualEdges],
            Math.Max(0, sourceNodes.Count - 1),
            hiddenNodes,
            warning,
            snapshot.WasTruncated || hiddenNodes > 0 || relationships.Length > contextualEdges.Length,
            ViewMode: ExplorerViewMode.Context);
        var summary = new ContextFilterSummary(
            validRelationships.Length,
            relationships.Length,
            contextualEdges.Length,
            CountBy(validRelationships, relationship => relationship.Kind.ToString()),
            CountBy(validRelationships, relationship => relationship.EvidenceClass.ToString()));
        return new ContextNeighborhoodBuildResult(neighborhood, summary);
    }

    private static ContextFilterCount[] CountBy(
        IEnumerable<ExplorerRelationship> relationships,
        Func<ExplorerRelationship, string> keySelector) =>
        relationships
            .GroupBy(keySelector, StringComparer.Ordinal)
            .Select(group => new ContextFilterCount(group.Key, group.Count()))
            .OrderByDescending(item => item.Count)
            .ThenBy(item => item.Key, StringComparer.Ordinal)
            .ToArray();

    private static bool TouchesFocus(ExplorerRelationship relationship, string focusId) =>
        ExplorerIdentity.Equals(relationship.SourceId, focusId) ||
        ExplorerIdentity.Equals(relationship.TargetId, focusId);
}
