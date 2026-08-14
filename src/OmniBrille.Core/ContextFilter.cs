namespace OmniBrille.Core;

/// <summary>
/// Reversible presentation filtering over relationships already authorized and
/// authored by a provider. It never creates relationships or changes provider state.
/// </summary>
public sealed record ContextFilter(
    ExplorerRelationshipKind? Kind = null,
    int MinimumStrength = 0,
    ExplorerRelationshipEvidenceClass? EvidenceClass = null)
{
    public static ContextFilter None { get; } = new();

    public bool IsActive => Kind is not null || MinimumStrength > 0 || EvidenceClass is not null;

    public ContextFilter Normalize() => this with { MinimumStrength = Math.Clamp(MinimumStrength, 0, 100) };

    public bool Matches(ExplorerRelationship relationship)
    {
        ArgumentNullException.ThrowIfNull(relationship);
        var normalized = Normalize();
        return (normalized.Kind is null || relationship.Kind == normalized.Kind) &&
               relationship.Strength >= normalized.MinimumStrength &&
               (normalized.EvidenceClass is null || relationship.EvidenceClass == normalized.EvidenceClass);
    }
}

public sealed record ContextFilterCount(string Key, int Count);

public sealed record ContextFilterSummary(
    int AuthoritativeRelationshipCount,
    int MatchingRelationshipCount,
    int VisibleRelationshipCount,
    IReadOnlyList<ContextFilterCount> KindCounts,
    IReadOnlyList<ContextFilterCount> EvidenceCounts)
{
    public int HiddenMatchingRelationshipCount => Math.Max(0, MatchingRelationshipCount - VisibleRelationshipCount);
}

public sealed record ContextNeighborhoodBuildResult(
    ExplorerNeighborhood Neighborhood,
    ContextFilterSummary Summary);

