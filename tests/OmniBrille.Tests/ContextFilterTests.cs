using OmniBrille.Core;

namespace OmniBrille.Tests;

public sealed class ContextFilterTests
{
    [Fact]
    public void Normalize_ClampsStrengthAndReportsActiveState()
    {
        Assert.False(ContextFilter.None.IsActive);

        var filter = new ContextFilter(
            ExplorerRelationshipKind.Topic,
            140,
            ExplorerRelationshipEvidenceClass.Deterministic).Normalize();

        Assert.True(filter.IsActive);
        Assert.Equal(100, filter.MinimumStrength);
    }

    [Theory]
    [InlineData(ExplorerRelationshipKind.Topic, 80, ExplorerRelationshipEvidenceClass.Deterministic, true)]
    [InlineData(ExplorerRelationshipKind.Entity, 80, ExplorerRelationshipEvidenceClass.Deterministic, false)]
    [InlineData(ExplorerRelationshipKind.Topic, 81, ExplorerRelationshipEvidenceClass.Deterministic, false)]
    [InlineData(ExplorerRelationshipKind.Topic, 80, ExplorerRelationshipEvidenceClass.Derived, false)]
    public void Matches_UsesOnlyProviderSuppliedMetadata(
        ExplorerRelationshipKind kind,
        int minimumStrength,
        ExplorerRelationshipEvidenceClass evidenceClass,
        bool expected)
    {
        var relationship = new ExplorerRelationship(
            "r", "a", "b", ExplorerRelationshipKind.Topic, 80, "Server reason",
            ExplorerRelationshipEvidenceClass.Deterministic, "Server provenance");

        Assert.Equal(expected, new ContextFilter(kind, minimumStrength, evidenceClass).Matches(relationship));
    }
}

