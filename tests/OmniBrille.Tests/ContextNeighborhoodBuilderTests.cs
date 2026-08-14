using OmniBrille.Core;

namespace OmniBrille.Tests;

public sealed class ContextNeighborhoodBuilderTests
{
    [Fact]
    public void Build_UsesOnlyProviderRelationshipsAndPreservesReasons()
    {
        var focus = Entry("focus", "Focus.txt");
        var related = Entry("related", "Related.txt");
        var relationship = Relationship("r1", focus.Id, related.Id, 80, "Shared indexed topic");

        var result = new ContextNeighborhoodBuilder().Build(new ExplorerContextSnapshot(
            focus,
            [related],
            [],
            [relationship]));

        Assert.Equal(ExplorerViewMode.Context, result.ViewMode);
        Assert.Equal(["focus", "related"], result.Nodes.Select(node => node.Id));
        var edge = Assert.Single(result.Edges);
        Assert.Equal(ExplorerGraphEdgeKind.Contextual, edge.Kind);
        Assert.Same(relationship, edge.Relationship);
        Assert.Equal("Shared indexed topic", edge.Relationship!.Reason);
    }

    [Fact]
    public void Build_EnforcesThreeContextEdgesAtFocusAndHidesUnconnectedNodes()
    {
        var focus = Entry("focus", "Focus.txt");
        var related = Enumerable.Range(0, 12).Select(index => Entry($"n{index:D2}", $"Node {index:D2}")).ToArray();
        var relationships = related.Select((node, index) =>
            Relationship($"r{index:D2}", focus.Id, node.Id, 100 - index)).ToArray();

        var result = new ContextNeighborhoodBuilder().Build(new ExplorerContextSnapshot(
            focus,
            related,
            [],
            relationships));

        Assert.Equal(4, result.Nodes.Count);
        Assert.Equal(3, result.Edges.Count(edge => edge.Kind == ExplorerGraphEdgeKind.Contextual));
        Assert.All(result.Nodes.Skip(1), node => Assert.Contains(result.Edges, edge =>
            ExplorerIdentity.Equals(edge.SourceId, node.Id) || ExplorerIdentity.Equals(edge.TargetId, node.Id)));
        Assert.True(result.SourceWasTruncated);
    }

    [Fact]
    public void Build_AcceptsStageThreeDensityWithoutExceedingAnyBudget()
    {
        var focus = Entry("n00", "Node 00");
        var nodes = Enumerable.Range(1, 47).Select(index => Entry($"n{index:D2}", $"Node {index:D2}")).ToArray();
        var all = nodes.Prepend(focus).ToArray();
        var relationships = Enumerable.Range(0, 36).Select(index =>
        {
            var source = all[index % all.Length];
            var target = all[(index + 11) % all.Length];
            return Relationship($"r{index:D2}", source.Id, target.Id, 90 - index);
        }).ToArray();

        var result = new ContextNeighborhoodBuilder().Build(new ExplorerContextSnapshot(
            focus,
            nodes,
            [],
            relationships));
        var contextual = result.Edges.Where(edge => edge.Kind == ExplorerGraphEdgeKind.Contextual).ToArray();
        var degrees = contextual
            .SelectMany(edge => new[] { edge.SourceId, edge.TargetId })
            .GroupBy(id => id, ExplorerIdentity.Comparer)
            .Select(group => group.Count());

        Assert.True(result.Nodes.Count <= 48);
        Assert.True(contextual.Length <= 36);
        Assert.True(result.Edges.Count <= 84);
        Assert.All(degrees, degree => Assert.True(degree <= 3));
    }

    [Fact]
    public void Build_DeduplicatesRelationshipsAndRejectsMissingEndpoints()
    {
        var focus = Entry("focus", "Focus");
        var related = Entry("related", "Related");
        var valid = Relationship("same", focus.Id, related.Id, 80);
        var missing = Relationship("missing", focus.Id, "outside-scope", 100);

        var result = new ContextNeighborhoodBuilder().Build(new ExplorerContextSnapshot(
            focus,
            [related],
            [],
            [valid, valid, missing]));

        Assert.Single(result.Edges);
        Assert.DoesNotContain(result.Edges, edge => edge.Relationship?.Id == "missing");
    }

    [Fact]
    public void BuildDetailed_FiltersReversiblyAndSummarizesAuthorizedMetadata()
    {
        var focus = Entry("focus", "Focus");
        var topic = Entry("topic", "Topic");
        var entity = Entry("entity", "Entity");
        ExplorerRelationship[] relationships =
        [
            Relationship("topic-r", focus.Id, topic.Id, 80) with { Kind = ExplorerRelationshipKind.Topic },
            Relationship("entity-r", focus.Id, entity.Id, 60) with
            {
                Kind = ExplorerRelationshipKind.Entity,
                EvidenceClass = ExplorerRelationshipEvidenceClass.Derived,
            },
        ];
        var snapshot = new ExplorerContextSnapshot(focus, [topic, entity], [], relationships);
        var builder = new ContextNeighborhoodBuilder();

        var filtered = builder.BuildDetailed(
            snapshot,
            new ContextFilter(ExplorerRelationshipKind.Entity, 60, ExplorerRelationshipEvidenceClass.Derived));
        var restored = builder.BuildDetailed(snapshot, ContextFilter.None);

        Assert.Equal(2, filtered.Summary.AuthoritativeRelationshipCount);
        Assert.Equal(1, filtered.Summary.MatchingRelationshipCount);
        Assert.Equal("entity-r", Assert.Single(filtered.Neighborhood.Edges).Relationship!.Id);
        Assert.Equal(2, restored.Summary.MatchingRelationshipCount);
        Assert.Equal(2, restored.Neighborhood.Edges.Count);
    }

    [Fact]
    public void BuildDetailed_UsesEvidenceClassAsStableTieBreakerAfterStrength()
    {
        var focus = Entry("focus", "Focus");
        var nodes = Enumerable.Range(0, 4).Select(index => Entry($"n{index}", $"Node {index}")).ToArray();
        var relationships = nodes.Select((node, index) => Relationship(
            $"r{index}", focus.Id, node.Id, 80) with
        {
            EvidenceClass = index == 3
                    ? ExplorerRelationshipEvidenceClass.Derived
                    : ExplorerRelationshipEvidenceClass.Deterministic,
        }).ToArray();

        var result = new ContextNeighborhoodBuilder().BuildDetailed(
            new ExplorerContextSnapshot(focus, nodes, [], relationships));

        Assert.DoesNotContain(result.Neighborhood.Edges, edge => edge.Relationship?.Id == "r3");
    }

    private static ExplorerEntry Entry(string id, string name) => new(
        id,
        name,
        $"OmniSorSe / {name}",
        ExplorerNodeKind.File,
        IsNavigable: true,
        NavigationTarget: id);

    private static ExplorerRelationship Relationship(
        string id,
        string source,
        string target,
        int strength,
        string? reason = null) => new(
            id,
            source,
            target,
            ExplorerRelationshipKind.Related,
            strength,
            reason,
            ExplorerRelationshipEvidenceClass.Deterministic,
            "OmniSorSe Related Files v1");
}
