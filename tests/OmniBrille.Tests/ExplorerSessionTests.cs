using OmniBrille.Core;
using OmniBrille.Desktop.Presentation;

namespace OmniBrille.Tests;

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

    [Fact]
    public async Task AggregateRefinement_ActivatesAndBackRestoresOverview()
    {
        var root = Normalize("aggregate-root");
        var children = Enumerable.Range(0, 30)
            .Select(index => Entry(Path.Combine(root, $"file-{index:D2}.txt"), ExplorerNodeKind.File))
            .ToArray();
        var provider = new FakeProvider(root, [Snapshot(root, children)]);
        using var session = new ExplorerSession(new GraphNeighborhoodBuilder(8));
        await session.OpenRootAsync(provider, provider);
        var overviewIds = session.Neighborhood!.Nodes.Select(node => node.Id).ToArray();
        var aggregate = Assert.Single(session.Neighborhood.Nodes, node => node.Kind == ExplorerNodeKind.Aggregate);

        Assert.True(session.ActivateAggregate(aggregate.Id));
        Assert.True(session.IsAggregateRefined);
        Assert.True(session.CanGoBack);
        Assert.NotEqual(overviewIds, session.Neighborhood.Nodes.Select(node => node.Id));

        Assert.True(await session.GoBackAsync());
        Assert.False(session.IsAggregateRefined);
        Assert.Equal(overviewIds, session.Neighborhood.Nodes.Select(node => node.Id));
        Assert.False(session.CanGoBack);
    }

    [Fact]
    public async Task ProgressiveLoad_ExposesInteractiveShellAndPartialState()
    {
        var root = Normalize("progressive-root");
        var provider = new ProgressiveFakeProvider(root);
        using var session = new ExplorerSession();

        var opening = session.OpenRootAsync(provider, provider);
        await provider.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(ExplorerLoadState.Loading, session.LoadState);
        Assert.NotNull(session.Neighborhood);
        Assert.Equal(root, session.Neighborhood.Focus.Path);

        var partialStateObserved = ObserveSessionStateAsync(
            session,
            () => session.LoadState == ExplorerLoadState.PartiallyLoaded);
        provider.Publish([Entry(Path.Combine(root, "one.txt"), ExplorerNodeKind.File)], isComplete: false);
        await partialStateObserved;
        Assert.Equal(1, session.LoadedItemCount);
        Assert.NotNull(session.Neighborhood);

        provider.Publish([Entry(Path.Combine(root, "two.txt"), ExplorerNodeKind.File)], isComplete: true);
        await opening;
        Assert.Equal(ExplorerLoadState.Ready, session.LoadState);
        Assert.Equal(2, session.LoadedItemCount);
    }

    [Fact]
    public async Task NewerNavigationPreventsStaleResultFromOverwritingScene()
    {
        var root = Normalize("stale-root");
        var folderA = Path.Combine(root, "A");
        var folderB = Path.Combine(root, "B");
        var provider = new ControllableProvider(root, Snapshot(root,
            Entry(folderA, ExplorerNodeKind.Folder),
            Entry(folderB, ExplorerNodeKind.Folder)));
        using var session = new ExplorerSession();
        await session.OpenRootAsync(provider, provider);

        var navigateA = session.NavigateAsync(folderA);
        await provider.WaitForRequestAsync(folderA);
        var navigateB = session.NavigateAsync(folderB);
        await provider.WaitForRequestAsync(folderB);
        provider.Complete(folderB, Snapshot(folderB, Entry(Path.Combine(folderB, "current.txt"), ExplorerNodeKind.File)));

        Assert.True(await navigateB);
        provider.Complete(folderA, Snapshot(folderA, Entry(Path.Combine(folderA, "stale.txt"), ExplorerNodeKind.File)));
        Assert.False(await navigateA);

        Assert.Equal(folderB, session.CurrentPath);
        Assert.Equal(folderB, session.Neighborhood!.Focus.Path);
        Assert.DoesNotContain(session.Neighborhood.Nodes, node => node.Name == "stale.txt");
    }

    [Fact]
    public async Task ConnectedNavigation_RejectsLateOpaqueResponseAndReportsDiagnostic()
    {
        const string root = "opaque-root";
        const string folderA = "opaque-a";
        const string folderB = "opaque-b";
        var provider = new ControllableProvider(root, Snapshot(root,
            Entry(folderA, ExplorerNodeKind.Folder),
            Entry(folderB, ExplorerNodeKind.Folder)))
        {
            Mode = ExplorerProviderMode.Connected,
        };
        using var session = new ExplorerSession();
        await session.OpenRootAsync(provider, provider);

        var navigateA = session.NavigateAsync(folderA);
        await provider.WaitForRequestAsync(folderA);
        var navigateB = session.NavigateAsync(folderB);
        await provider.WaitForRequestAsync(folderB);
        provider.Complete(folderB, Snapshot(folderB, Entry("current", ExplorerNodeKind.File)));
        Assert.True(await navigateB);
        provider.Complete(folderA, Snapshot(folderA, Entry("stale", ExplorerNodeKind.File)));
        Assert.False(await navigateA);

        Assert.Equal(folderB, session.Neighborhood!.FocusNodeId);
        Assert.Equal(1, provider.StaleResponseRejections);
    }

    [Fact]
    public async Task ClearSearch_CancelsAndResetsPresentationState()
    {
        var root = Normalize("clear-search");
        var provider = new FakeProvider(root, [Snapshot(root)]);
        using var session = new ExplorerSession();
        await session.OpenRootAsync(provider, provider);

        await session.SearchAsync("anything");
        session.ClearSearch();

        Assert.Null(session.SearchResult);
        Assert.Empty(session.HighlightedNodeIds);
        Assert.Empty(session.SearchQuery);
        Assert.False(session.IsSearching);
    }

    private static string Normalize(string name) =>
        Path.Combine(Path.GetTempPath(), $"OmniBrilleSessionTests-{name}");

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

    private sealed class ProgressiveFakeProvider :
        IExplorerProvider,
        IProgressiveExplorerProvider,
        IExplorerSearchProvider
    {
        private readonly System.Threading.Channels.Channel<ExplorerDirectoryBatch> _batches =
            System.Threading.Channels.Channel.CreateUnbounded<ExplorerDirectoryBatch>();

        public ProgressiveFakeProvider(string root)
        {
            AccessRoot = root;
        }

        public string AccessRoot { get; }

        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<ExplorerDirectorySnapshot> GetDirectoryAsync(string path, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public async IAsyncEnumerable<ExplorerDirectoryBatch> GetDirectoryBatchesAsync(
            string path,
            int batchSize,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            yield return new ExplorerDirectoryBatch(Entry(path, ExplorerNodeKind.Folder), [], 0, false);
            Started.TrySetResult();
            await foreach (var batch in _batches.Reader.ReadAllAsync(cancellationToken))
            {
                yield return batch;
                if (batch.IsComplete)
                {
                    yield break;
                }
            }
        }

        public Task<ExplorerSearchResult> SearchAsync(SearchRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new ExplorerSearchResult([], false, 0));

        public void Publish(IReadOnlyList<ExplorerEntry> entries, bool isComplete)
        {
            _batches.Writer.TryWrite(new ExplorerDirectoryBatch(
                Entry(AccessRoot, ExplorerNodeKind.Folder),
                entries,
                entries.Count,
                isComplete,
                TotalChildCount: entries.Count));
            if (isComplete)
            {
                _batches.Writer.TryComplete();
            }
        }
    }

    private sealed class ControllableProvider :
        IExplorerProvider,
        IExplorerSearchProvider,
        IExplorerProviderDiagnostics
    {
        private readonly ExplorerDirectorySnapshot _rootSnapshot;
        private readonly Dictionary<string, TaskCompletionSource<ExplorerDirectorySnapshot>> _requests =
            new(StringComparer.OrdinalIgnoreCase);

        public ControllableProvider(string root, ExplorerDirectorySnapshot rootSnapshot)
        {
            AccessRoot = root;
            _rootSnapshot = rootSnapshot;
        }

        public string AccessRoot { get; }

        public ExplorerProviderMode Mode { get; init; }

        public int StaleResponseRejections { get; private set; }

        public Task<ExplorerDirectorySnapshot> GetDirectoryAsync(string path, CancellationToken cancellationToken)
        {
            if (StringComparer.OrdinalIgnoreCase.Equals(path, AccessRoot))
            {
                return Task.FromResult(_rootSnapshot);
            }

            lock (_requests)
            {
                if (!_requests.TryGetValue(path, out var request))
                {
                    request = new TaskCompletionSource<ExplorerDirectorySnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
                    _requests[path] = request;
                }

                return request.Task;
            }
        }

        public Task<ExplorerSearchResult> SearchAsync(SearchRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new ExplorerSearchResult([], false, 0));

        public async Task WaitForRequestAsync(string path)
        {
            await WaitUntilAsync(() =>
            {
                lock (_requests)
                {
                    return _requests.ContainsKey(path);
                }
            });
        }

        public void Complete(string path, ExplorerDirectorySnapshot snapshot)
        {
            lock (_requests)
            {
                _requests[path].TrySetResult(snapshot);
            }
        }

        public void ReportStaleResponseRejected() => StaleResponseRejections++;
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
        }

        Assert.True(condition(), "The expected asynchronous state was not reached before the test deadline.");
    }

    private static async Task ObserveSessionStateAsync(ExplorerSession session, Func<bool> condition)
    {
        if (condition())
        {
            return;
        }

        var observed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnStateChanged(object? sender, EventArgs args)
        {
            if (condition())
            {
                observed.TrySetResult();
            }
        }

        session.StateChanged += OnStateChanged;
        try
        {
            if (condition())
            {
                return;
            }

            await observed.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }
        finally
        {
            session.StateChanged -= OnStateChanged;
        }
    }
}
