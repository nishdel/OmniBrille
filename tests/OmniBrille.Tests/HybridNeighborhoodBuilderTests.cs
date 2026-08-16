using OmniBrille.Core;

namespace OmniBrille.Tests;

public sealed class HybridNeighborhoodBuilderTests
{
    [Fact]
    public void Build_DeduplicatesSharedNodesAndMergesRoles()
    {
        var root = Entry("root", ExplorerNodeKind.Folder);
        var focus = Entry("focus", ExplorerNodeKind.File);
        var relatedSibling = Entry("sibling", ExplorerNodeKind.File);
        var snapshot = new ExplorerContextSnapshot(
            focus,
            [root, relatedSibling, relatedSibling],
            [new ExplorerEdge(root.Id, focus.Id), new ExplorerEdge(root.Id, relatedSibling.Id)],
            [Relationship("r1", focus.Id, relatedSibling.Id, 90)]);

        var scene = new HybridNeighborhoodBuilder().BuildDetailed(snapshot).Neighborhood;

        Assert.Equal(ExplorerViewMode.Hybrid, scene.ViewMode);
        Assert.Equal(3, scene.Nodes.Count);
        var sibling = Assert.Single(scene.Nodes, node => node.Id == relatedSibling.Id);
        Assert.Equal(ExplorerNodeRole.Structural | ExplorerNodeRole.Contextual, sibling.Roles);
        Assert.Single(scene.Edges, edge => edge.Kind == ExplorerGraphEdgeKind.Contextual);
        Assert.Equal(2, scene.Edges.Count(edge => edge.Kind == ExplorerGraphEdgeKind.Structural));
    }

    [Fact]
    public void Build_PreservesStructuralOrientationWhenContextIsDense()
    {
        var parent = Entry("parent", ExplorerNodeKind.Folder);
        var focus = Entry("focus", ExplorerNodeKind.File);
        var children = Enumerable.Range(0, 40)
            .Select(index => Entry($"child-{index:D2}", ExplorerNodeKind.File))
            .ToArray();
        var contextual = Enumerable.Range(0, 50)
            .Select(index => Entry($"related-{index:D2}", ExplorerNodeKind.File))
            .ToArray();
        var structuralEdges = children
            .Select(child => new ExplorerEdge(focus.Id, child.Id))
            .Prepend(new ExplorerEdge(parent.Id, focus.Id))
            .ToArray();
        var relationships = contextual
            .Select((node, index) => Relationship($"r-{index:D2}", focus.Id, node.Id, 100 - index))
            .ToArray();

        var scene = new HybridNeighborhoodBuilder().BuildDetailed(new ExplorerContextSnapshot(
            focus,
            [parent, .. children, .. contextual],
            structuralEdges,
            relationships)).Neighborhood;

        Assert.Contains(scene.Nodes, node => node.Id == parent.Id);
        Assert.Contains(scene.Edges, edge => edge.SourceId == parent.Id && edge.TargetId == focus.Id);
        Assert.Contains(scene.Edges, edge => edge.Kind == ExplorerGraphEdgeKind.Contextual);
        Assert.True(scene.Nodes.Count <= 48);
    }

    [Fact]
    public void Build_ContextFilterNeverRemovesStructuralSkeleton()
    {
        var parent = Entry("parent", ExplorerNodeKind.Folder);
        var focus = Entry("focus", ExplorerNodeKind.File);
        var sibling = Entry("sibling", ExplorerNodeKind.File);
        var related = Entry("related", ExplorerNodeKind.File);
        var snapshot = new ExplorerContextSnapshot(
            focus,
            [parent, sibling, related],
            [new ExplorerEdge(parent.Id, focus.Id), new ExplorerEdge(parent.Id, sibling.Id)],
            [Relationship("topic", focus.Id, related.Id, 80) with { Kind = ExplorerRelationshipKind.Topic }]);
        var builder = new HybridNeighborhoodBuilder();

        var filtered = builder.BuildDetailed(snapshot, new ContextFilter(ExplorerRelationshipKind.Entity));
        var restored = builder.BuildDetailed(snapshot, ContextFilter.None);

        Assert.Equal(2, filtered.Neighborhood.Edges.Count(edge => edge.Kind == ExplorerGraphEdgeKind.Structural));
        Assert.DoesNotContain(filtered.Neighborhood.Edges, edge => edge.Kind == ExplorerGraphEdgeKind.Contextual);
        Assert.DoesNotContain(filtered.Neighborhood.Nodes, node => node.Id == related.Id);
        Assert.Contains(restored.Neighborhood.Nodes, node => node.Id == related.Id);
    }

    [Fact]
    public void Build_MaximumFixtureRespectsAllStageThreeBudgets()
    {
        var focus = Entry("n00", ExplorerNodeKind.File);
        var nodes = Enumerable.Range(1, 63)
            .Select(index => Entry($"n{index:D2}", index % 5 == 0 ? ExplorerNodeKind.Folder : ExplorerNodeKind.File))
            .ToArray();
        var all = nodes.Prepend(focus).ToArray();
        var structural = Enumerable.Range(1, 47)
            .Select(index => new ExplorerEdge(focus.Id, all[index].Id))
            .ToArray();
        var relationships = Enumerable.Range(0, 36)
            .Select(index => Relationship(
                $"r{index:D2}",
                all[(index % 18) + 1].Id,
                all[((index + 7) % 18) + 1].Id,
                100 - index))
            .ToArray();

        var scene = new HybridNeighborhoodBuilder().BuildDetailed(new ExplorerContextSnapshot(
            focus,
            nodes,
            structural,
            relationships)).Neighborhood;
        var contextual = scene.Edges.Where(edge => edge.Kind == ExplorerGraphEdgeKind.Contextual).ToArray();
        var degrees = contextual
            .SelectMany(edge => new[] { edge.SourceId, edge.TargetId })
            .GroupBy(id => id, ExplorerIdentity.Comparer)
            .Select(group => group.Count());

        Assert.True(scene.Nodes.Count <= 48);
        Assert.True(scene.Edges.Count(edge => edge.Kind == ExplorerGraphEdgeKind.Structural) <= 47);
        Assert.True(contextual.Length <= 36);
        Assert.True(scene.Edges.Count <= 84);
        Assert.All(degrees, degree => Assert.True(degree <= 3));
        Assert.Equal(scene.Nodes.Count, scene.Nodes.Select(node => node.Id).Distinct(ExplorerIdentity.Comparer).Count());
    }

    [Fact]
    public void Build_IsDeterministicAcrossRepeatedComposition()
    {
        var focus = Entry("focus", ExplorerNodeKind.File);
        var nodes = Enumerable.Range(0, 20).Select(index => Entry($"n{index:D2}", ExplorerNodeKind.File)).ToArray();
        var snapshot = new ExplorerContextSnapshot(
            focus,
            nodes,
            nodes.Take(12).Select(node => new ExplorerEdge(focus.Id, node.Id)).ToArray(),
            nodes.Skip(5).Select((node, index) => Relationship($"r{index:D2}", focus.Id, node.Id, 90 - index)).ToArray());
        var builder = new HybridNeighborhoodBuilder();

        var first = builder.BuildDetailed(snapshot).Neighborhood;
        var second = builder.BuildDetailed(snapshot).Neighborhood;

        Assert.Equal(first.Nodes, second.Nodes);
        Assert.Equal(first.Edges, second.Edges);
    }

    [Fact]
    public void Build_RejectsMalformedRelationshipsAndKeepsEmptyHybridUseful()
    {
        var parent = Entry("parent", ExplorerNodeKind.Folder);
        var focus = Entry("focus", ExplorerNodeKind.File);
        var malformed = Relationship("bad", focus.Id, "outside-scope", 100);

        var result = new HybridNeighborhoodBuilder().BuildDetailed(new ExplorerContextSnapshot(
            focus,
            [parent],
            [new ExplorerEdge(parent.Id, focus.Id)],
            [malformed]));

        Assert.Equal(2, result.Neighborhood.Nodes.Count);
        Assert.Single(result.Neighborhood.Edges);
        Assert.Equal(0, result.Summary.AuthoritativeRelationshipCount);
        Assert.Equal(ExplorerViewMode.Hybrid, result.Neighborhood.ViewMode);
    }

    [Fact]
    public void Build_SelectedWeakRelationshipWinsStablePriority()
    {
        var focus = Entry("focus", ExplorerNodeKind.File);
        var related = Enumerable.Range(0, 5).Select(index => Entry($"n{index}", ExplorerNodeKind.File)).ToArray();
        var relationships = related
            .Select((node, index) => Relationship($"r{index}", focus.Id, node.Id, 100 - (index * 20)))
            .ToArray();

        var scene = new HybridNeighborhoodBuilder().BuildDetailed(
            new ExplorerContextSnapshot(focus, related, [], relationships),
            selectedRelationshipId: "r4").Neighborhood;

        Assert.Contains(scene.Edges, edge => edge.Relationship?.Id == "r4");
    }

    private static ExplorerEntry Entry(string id, ExplorerNodeKind kind) => new(
        id,
        id,
        $"OmniSorSe / {id}",
        kind,
        IsNavigable: true,
        NavigationTarget: id);

    private static ExplorerRelationship Relationship(string id, string source, string target, int strength) => new(
        id,
        source,
        target,
        ExplorerRelationshipKind.Related,
        strength,
        "Server-authored reason",
        ExplorerRelationshipEvidenceClass.Deterministic,
        "OmniSorSe Related Files v1");
}
