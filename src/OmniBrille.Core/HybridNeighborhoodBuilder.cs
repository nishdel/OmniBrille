namespace OmniBrille.Core;

/// <summary>
/// Composes provider-authored structural and contextual data into one bounded
/// Hybrid scene. It prioritizes and filters relationships but never infers them.
/// </summary>
public sealed class HybridNeighborhoodBuilder
{
    private const int MaximumReservedContextNodes = 18;
    private readonly ContextRenderBudget _budget;

    public HybridNeighborhoodBuilder(ContextRenderBudget? budget = null) =>
        _budget = budget ?? ContextRenderBudgetPolicy.Default;

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
        var structuralCandidates = snapshot.StructuralEdges
            .Where(edge => IsValidEdge(edge.SourceId, edge.TargetId, sourceNodes))
            .GroupBy(edge => $"{edge.SourceId}\u001f{edge.TargetId}", ExplorerIdentity.Comparer)
            .Select(group => group.First() with
            {
                Kind = ExplorerGraphEdgeKind.Structural,
                Relationship = null,
            })
            .OrderBy(edge => StructuralPriority(edge, snapshot.Focus.Id))
            .ThenBy(edge => edge.SourceId, ExplorerIdentity.Comparer)
            .ThenBy(edge => edge.TargetId, ExplorerIdentity.Comparer)
            .Take(_budget.MaximumStructuralEdges)
            .ToArray();
        var authoritativeRelationships = snapshot.Relationships
            .Where(relationship => IsValidEdge(relationship.SourceId, relationship.TargetId, sourceNodes))
            .GroupBy(relationship => relationship.Id, ExplorerIdentity.Comparer)
            .Select(group => group.First())
            .ToArray();
        var matchingRelationships = authoritativeRelationships
            .Where(filter.Matches)
            .OrderByDescending(relationship => ExplorerIdentity.Equals(relationship.Id, selectedRelationshipId))
            .ThenByDescending(relationship => TouchesFocus(relationship, snapshot.Focus.Id))
            .ThenByDescending(relationship => relationship.Strength)
            .ThenBy(relationship => relationship.EvidenceClass == ExplorerRelationshipEvidenceClass.Deterministic ? 0 : 1)
            .ThenBy(relationship => relationship.Id, ExplorerIdentity.Comparer)
            .ToArray();

        var selectedNodeIds = new HashSet<string>(ExplorerIdentity.Comparer)
        {
            snapshot.Focus.Id,
        };
        var distinctContextNodeCount = matchingRelationships
            .SelectMany(relationship => new[] { relationship.SourceId, relationship.TargetId })
            .Where(id => !ExplorerIdentity.Equals(id, snapshot.Focus.Id))
            .Distinct(ExplorerIdentity.Comparer)
            .Count();
        var reservedContextNodes = distinctContextNodeCount == 0
            ? 0
            : Math.Min(MaximumReservedContextNodes, Math.Max(3, distinctContextNodeCount));
        var structuralNodeLimit = Math.Max(1, _budget.MaximumVisibleNodes - reservedContextNodes);

        AddStructuralNodes(structuralCandidates, selectedNodeIds, structuralNodeLimit);
        var initiallyVisibleStructuralEdges = structuralCandidates.Count(edge =>
            selectedNodeIds.Contains(edge.SourceId) && selectedNodeIds.Contains(edge.TargetId));
        var relationshipCandidates = matchingRelationships.Select(relationship =>
            new ContextRelationshipCandidate(
                relationship.Id,
                relationship.SourceId,
                relationship.TargetId,
                relationship.Strength / 100d,
                TouchesFocus(relationship, snapshot.Focus.Id),
                ExplorerIdentity.Equals(relationship.Id, selectedRelationshipId),
                relationship.EvidenceClass));
        var prioritizedRelationshipIds = ContextRenderBudgetPolicy
            .SelectRelationships(relationshipCandidates, initiallyVisibleStructuralEdges, _budget)
            .Select(candidate => candidate.Id)
            .ToHashSet(ExplorerIdentity.Comparer);
        var selectedRelationships = new List<ExplorerRelationship>(_budget.MaximumContextualEdges);
        foreach (var relationship in matchingRelationships.Where(item => prioritizedRelationshipIds.Contains(item.Id)))
        {
            var requiredNodeIds = new[] { relationship.SourceId, relationship.TargetId }
                .Where(id => !selectedNodeIds.Contains(id))
                .Distinct(ExplorerIdentity.Comparer)
                .ToArray();
            if (selectedNodeIds.Count + requiredNodeIds.Length > _budget.MaximumVisibleNodes)
            {
                continue;
            }

            selectedNodeIds.UnionWith(requiredNodeIds);
            selectedRelationships.Add(relationship);
        }

        AddStructuralNodes(structuralCandidates, selectedNodeIds, _budget.MaximumVisibleNodes);
        var structuralEdges = structuralCandidates
            .Where(edge => selectedNodeIds.Contains(edge.SourceId) && selectedNodeIds.Contains(edge.TargetId))
            .Take(_budget.MaximumStructuralEdges)
            .ToArray();
        var availableContextEdges = Math.Max(
            0,
            Math.Min(_budget.MaximumContextualEdges, _budget.MaximumCombinedEdges - structuralEdges.Length));
        var contextualEdges = selectedRelationships
            .Where(relationship =>
                selectedNodeIds.Contains(relationship.SourceId) && selectedNodeIds.Contains(relationship.TargetId))
            .Take(availableContextEdges)
            .Select(relationship => new ExplorerEdge(
                relationship.SourceId,
                relationship.TargetId,
                ExplorerGraphEdgeKind.Contextual,
                relationship))
            .ToArray();

        var structuralNodeIds = structuralEdges
            .SelectMany(edge => new[] { edge.SourceId, edge.TargetId })
            .Append(snapshot.Focus.Id)
            .ToHashSet(ExplorerIdentity.Comparer);
        var contextualNodeIds = contextualEdges
            .SelectMany(edge => new[] { edge.SourceId, edge.TargetId })
            .ToHashSet(ExplorerIdentity.Comparer);
        var nodes = sourceNodes.Values
            .Where(node => selectedNodeIds.Contains(node.Id))
            .OrderBy(node => ExplorerIdentity.Equals(node.Id, snapshot.Focus.Id) ? 0 : 1)
            .ThenBy(node => StructuralNodePriority(node.Id, structuralEdges, snapshot.Focus.Id))
            .ThenByDescending(node => RelationshipStrength(node.Id, contextualEdges))
            .ThenBy(node => node.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(node => node.Id, ExplorerIdentity.Comparer)
            .Select(entry => ExplorerNode.FromEntry(
                entry,
                RolesFor(entry.Id, structuralNodeIds, contextualNodeIds)) with
            {
                IsNavigable = true,
            })
            .ToArray();
        var hiddenNodes = Math.Max(0, sourceNodes.Count - nodes.Length);
        var warning = snapshot.Warning;
        if (matchingRelationships.Length > contextualEdges.Length)
        {
            var bounded = $"Showing {contextualEdges.Length:N0} of {matchingRelationships.Length:N0} matching authoritative relationships.";
            warning = string.IsNullOrWhiteSpace(warning) ? bounded : $"{warning} {bounded}";
        }

        var neighborhood = new ExplorerNeighborhood(
            snapshot.Focus.Id,
            nodes,
            [.. structuralEdges, .. contextualEdges],
            Math.Max(0, sourceNodes.Count - 1),
            hiddenNodes,
            warning,
            snapshot.WasTruncated || hiddenNodes > 0 || matchingRelationships.Length > contextualEdges.Length,
            ViewMode: ExplorerViewMode.Hybrid);
        var summary = new ContextFilterSummary(
            authoritativeRelationships.Length,
            matchingRelationships.Length,
            contextualEdges.Length,
            CountBy(authoritativeRelationships, relationship => relationship.Kind.ToString()),
            CountBy(authoritativeRelationships, relationship => relationship.EvidenceClass.ToString()));
        return new ContextNeighborhoodBuildResult(neighborhood, summary);
    }

    private static void AddStructuralNodes(
        IEnumerable<ExplorerEdge> edges,
        HashSet<string> selectedNodeIds,
        int nodeLimit)
    {
        foreach (var edge in edges)
        {
            var required = new[] { edge.SourceId, edge.TargetId }
                .Where(id => !selectedNodeIds.Contains(id))
                .Distinct(ExplorerIdentity.Comparer)
                .ToArray();
            if (selectedNodeIds.Count + required.Length > nodeLimit)
            {
                continue;
            }

            selectedNodeIds.UnionWith(required);
        }
    }

    private static bool IsValidEdge(
        string sourceId,
        string targetId,
        Dictionary<string, ExplorerEntry> sourceNodes) =>
        sourceNodes.ContainsKey(sourceId) &&
        sourceNodes.ContainsKey(targetId) &&
        !ExplorerIdentity.Equals(sourceId, targetId);

    private static int StructuralPriority(ExplorerEdge edge, string focusId) =>
        ExplorerIdentity.Equals(edge.TargetId, focusId)
            ? 0
            : ExplorerIdentity.Equals(edge.SourceId, focusId)
                ? 1
                : 2;

    private static int StructuralNodePriority(
        string nodeId,
        IReadOnlyList<ExplorerEdge> edges,
        string focusId) =>
        edges.Any(edge => ExplorerIdentity.Equals(edge.SourceId, nodeId) && ExplorerIdentity.Equals(edge.TargetId, focusId))
            ? 0
            : edges.Any(edge => ExplorerIdentity.Equals(edge.SourceId, focusId) && ExplorerIdentity.Equals(edge.TargetId, nodeId))
                ? 1
                : 2;

    private static int RelationshipStrength(string nodeId, IEnumerable<ExplorerEdge> edges) => edges
        .Where(edge => ExplorerIdentity.Equals(edge.SourceId, nodeId) || ExplorerIdentity.Equals(edge.TargetId, nodeId))
        .Select(edge => edge.Relationship?.Strength ?? 0)
        .DefaultIfEmpty(0)
        .Max();

    private static ExplorerNodeRole RolesFor(
        string nodeId,
        HashSet<string> structuralNodeIds,
        HashSet<string> contextualNodeIds)
    {
        var roles = ExplorerNodeRole.None;
        if (structuralNodeIds.Contains(nodeId))
        {
            roles |= ExplorerNodeRole.Structural;
        }

        if (contextualNodeIds.Contains(nodeId))
        {
            roles |= ExplorerNodeRole.Contextual;
        }

        return roles;
    }

    private static bool TouchesFocus(ExplorerRelationship relationship, string focusId) =>
        ExplorerIdentity.Equals(relationship.SourceId, focusId) ||
        ExplorerIdentity.Equals(relationship.TargetId, focusId);

    private static ContextFilterCount[] CountBy(
        IEnumerable<ExplorerRelationship> relationships,
        Func<ExplorerRelationship, string> keySelector) => relationships
        .GroupBy(keySelector, StringComparer.Ordinal)
        .Select(group => new ContextFilterCount(group.Key, group.Count()))
        .OrderByDescending(item => item.Count)
        .ThenBy(item => item.Key, StringComparer.Ordinal)
        .ToArray();
}
