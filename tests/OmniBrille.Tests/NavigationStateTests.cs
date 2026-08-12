using OmniBrille.Core;

namespace OmniBrille.Tests;

public sealed class NavigationStateTests
{
    [Fact]
    public void NavigationAndBack_PreserveDeterministicHistory()
    {
        var root = Path.Combine(Path.GetTempPath(), "root");
        var child = Path.Combine(root, "child");
        var grandchild = Path.Combine(child, "grandchild");
        var state = new NavigationState();

        state.SetRoot(root);
        state.NavigateTo(child);
        state.NavigateTo(grandchild);

        Assert.Equal([root, child], state.History);
        Assert.Equal(child, state.GoBack());
        Assert.Equal(root, state.GoBack());
        Assert.Null(state.GoBack());
        Assert.False(state.CanGoBack);
    }

    [Fact]
    public void NavigateTo_RejectsPathOutsideSelectedRoot()
    {
        var state = new NavigationState();
        state.SetRoot(Path.Combine(Path.GetTempPath(), "root"));

        Assert.Throws<InvalidOperationException>(() =>
            state.NavigateTo(Path.Combine(Path.GetTempPath(), "elsewhere")));
    }

    [Fact]
    public void SetRoot_ClearsExistingHistory()
    {
        var first = Path.Combine(Path.GetTempPath(), "first");
        var state = new NavigationState();
        state.SetRoot(first);
        state.NavigateTo(Path.Combine(first, "child"));

        state.SetRoot(Path.Combine(Path.GetTempPath(), "second"));

        Assert.False(state.CanGoBack);
        Assert.Empty(state.History);
    }

    [Theory]
    [InlineData("child", true)]
    [InlineData("childhood", false)]
    public void PathBoundary_RequiresDirectoryBoundary(string suffix, bool expected)
    {
        var root = Path.Combine(Path.GetTempPath(), "scope", "child");
        var candidate = expected ? Path.Combine(root, suffix) : root + "hood";

        Assert.Equal(expected, PathBoundary.IsWithin(root, candidate));
    }
}
