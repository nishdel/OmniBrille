using OmniExplorer.Core;
using OmniExplorer.Desktop.Presentation;

namespace OmniExplorer.Tests;

public sealed class ExplorerSessionTests
{
    [Fact]
    public async Task NavigateAndBack_ChangeFocusAndPreserveContext()
    {
        var root = Normalize("root");
        var child = Path.Combine(root, "child");
        var provider = new FakeProvider(root,
        [
            Snapshot(root, Entry(child, ExplorerNodeKind.Folder)),
            Snapshot(child, Entry(Path.Combine(child, "file.txt"), ExplorerNodeKind.File)),
        ]);
        using var session = new ExplorerSession();
        await session.OpenRootAsync(provider, provider);

        Assert.True(await session.NavigateAsync(child));
        Assert.Equal(child, session.CurrentPath);
        Assert.True(session.CanGoBack);
        Assert.Contains(session.Neighborhood!.Nodes, node => node.Kind == ExplorerNodeKind.Context && node.Path == root);

        Assert.True(await session.GoBackAsync());
        Assert.Equal(root, session.CurrentPath);
    }

    [Fact]
    public async Task FailedNavigation_DoesNotReplaceCurrentFocus()
    {
        var root = Normalize("root");
        var missing = Path.Combine(root, "missing");
        var provider = new FakeProvider(root,
        [
            Snapshot(root, Entry(missing, ExplorerNodeKind.Folder)),
            new ExplorerDirectorySnapshot(
                Entry(missing, ExplorerNodeKind.Folder),
                [],
                ExplorerFailureKind.NotFound,
                "gone"),
        ]);
        using var session = new ExplorerSession();
        await session.OpenRootAsync(provider, provider);

        Assert.False(await session.NavigateAsync(missing));
        Assert.Equal(root, session.CurrentPath);
        Assert.Equal("gone", session.Status);
    }

    [Fact]
    public async Task Search_HighlightsVisibleMatches()
    {
        var root = Normalize("root");
        var match = Entry(Path.Combine(root, "match.txt"), ExplorerNodeKind.File);
        var provider = new FakeProvider(root, [Snapshot(root, match)])
        {
            SearchResult = new ExplorerSearchResult(
                [new ExplorerSearchHit(match.Id, match.Name, match.Path, match.Kind)],
                false,
                1),
        };
        using var session = new ExplorerSession();
        await session.OpenRootAsync(provider, provider);

        await session.SearchAsync("match");

        Assert.Contains(match.Id, session.HighlightedNodeIds);
        Assert.Single(session.SearchResult!.Hits);
    }

    [Fact]
    public async Task FileSearchFocus_PinsMatchIntoBoundedGraph()
    {
        var root = Normalize("root");
        var children = Enumerable.Range(0, 20)
            .Select(index => Entry(Path.Combine(root, $"file-{index:D2}.txt"), ExplorerNodeKind.File))
            .ToArray();
        var match = children[^1];
        var provider = new FakeProvider(root, [Snapshot(root, children)]);
        using var session = new ExplorerSession(new GraphNeighborhoodBuilder(6));
        await session.OpenRootAsync(provider, provider);

        var focused = await session.FocusSearchHitAsync(
            new ExplorerSearchHit(match.Id, match.Name, match.Path, match.Kind));

        Assert.True(focused);
        Assert.Equal(match.Id, session.SelectedNode!.Id);
        Assert.Contains(session.Neighborhood!.Nodes, node => node.Id == match.Id);
        Assert.Equal(6, session.Neighborhood.Nodes.Count);
    }

    private static string Normalize(string name) =>
        Path.Combine(Path.GetTempPath(), $"OmniExplorerSessionTests-{name}");

    private static ExplorerEntry Entry(string path, ExplorerNodeKind kind) =>
        new(path, Path.GetFileName(path), path, kind);

    private static ExplorerDirectorySnapshot Snapshot(string path, params ExplorerEntry[] children) =>
        new(Entry(path, ExplorerNodeKind.Folder), children);

    private sealed class FakeProvider : IExplorerProvider, IExplorerSearchProvider
    {
        private readonly Dictionary<string, ExplorerDirectorySnapshot> _snapshots;

        public FakeProvider(string root, IEnumerable<ExplorerDirectorySnapshot> snapshots)
        {
            AccessRoot = root;
            _snapshots = snapshots.ToDictionary(snapshot => snapshot.Focus.Path, StringComparer.OrdinalIgnoreCase);
        }

        public string AccessRoot { get; }

        public ExplorerSearchResult SearchResult { get; init; } = new([], false, 0);

        public Task<ExplorerDirectorySnapshot> GetDirectoryAsync(
            string path,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_snapshots[path]);
        }

        public Task<ExplorerSearchResult> SearchAsync(
            SearchRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(SearchResult);
        }
    }
}
