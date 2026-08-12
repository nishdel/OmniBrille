using OmniBrille.Core;

namespace OmniBrille.Tests;

public sealed class ContextRenderBudgetPolicyTests
{
    [Theory]
    [InlineData(32, 31, 24, 56)]
    [InlineData(48, 47, 36, 84)]
    [InlineData(64, 63, 48, 112)]
    public void ForNodeLimit_ProducesConservativeExplainableBudgets(
        int nodes,
        int structuralEdges,
        int contextualEdges,
        int combinedEdges)
    {
        var budget = ContextRenderBudgetPolicy.ForNodeLimit(nodes);

        Assert.Equal(nodes, budget.MaximumVisibleNodes);
        Assert.Equal(structuralEdges, budget.MaximumStructuralEdges);
        Assert.Equal(contextualEdges, budget.MaximumContextualEdges);
        Assert.Equal(combinedEdges, budget.MaximumCombinedEdges);
        Assert.Equal(3, budget.MaximumContextualEdgesPerNode);
    }

    [Fact]
    public void SelectRelationships_PrioritizesSelectionFocusAndImportanceDeterministically()
    {
        var budget = new ContextRenderBudget(8, 7, 3, 10, 2);
        ContextRelationshipCandidate[] candidates =
        [
            new("ordinary", "a", "b", 0.9),
            new("focus", "c", "d", 0.4, TouchesFocus: true),
            new("selected", "e", "f", 0.1, IsSelected: true),
            new("lower", "g", "h", 0.2),
        ];

        var selected = ContextRenderBudgetPolicy.SelectRelationships(candidates, 7, budget);

        Assert.Equal(["selected", "focus", "ordinary"], selected.Select(item => item.Id));
    }

    [Fact]
    public void SelectRelationships_BoundsGlobalAndPerNodeDensity()
    {
        var candidates = Enumerable.Range(0, 200)
            .Select(index => new ContextRelationshipCandidate(
                $"relationship-{index:D3}",
                index < 20 ? "hub" : $"node-{index % 48}",
                $"node-{(index + 7) % 48}",
                1 - (index / 200d)))
            .ToArray();

        var selected = ContextRenderBudgetPolicy.SelectRelationships(candidates, 47);
        var degree = selected
            .SelectMany(item => new[] { item.SourceId, item.TargetId })
            .GroupBy(id => id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count());

        Assert.True(selected.Count <= ContextRenderBudgetPolicy.Default.MaximumContextualEdges);
        Assert.All(degree.Values, value => Assert.True(value <= ContextRenderBudgetPolicy.Default.MaximumContextualEdgesPerNode));
    }
}
