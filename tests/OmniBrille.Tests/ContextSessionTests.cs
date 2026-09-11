using OmniBrille.Core;
using OmniBrille.Desktop.Presentation;

namespace OmniBrille.Tests;

public sealed class ContextSessionTests
{
    [Fact]
    public async Task Standalone_ContextIsUnavailableWithoutInventedRelationships()
    {
        var provider = new ContextlessProvider();
        using var session = new ExplorerSession();
        await session.OpenRootAsync(provider, provider);

        var switched = await session.SwitchToContextAsync();

        Assert.False(switched);
        Assert.Equal(ExplorerViewMode.Structure, session.ViewMode);
        Assert.Equal("Context exploration requires OmniSorSe.", session.Status);
    }

    [Fact]
    public async Task ConnectedContext_RefocusesAndBackRestoresContextThenStructure()
    {
        var provider = ContextProvider.Immediate();
        using var session = new ExplorerSession();
        await session.OpenRootAsync(provider, provider);
        session.SelectNode("a");

        Assert.True(await session.SwitchToContextAsync());
        Assert.Equal(ExplorerViewMode.Context, session.ViewMode);
        Assert.Equal("a", session.Neighborhood!.FocusNodeId);
        session.SelectNode("b");
        Assert.Equal("Shared topic", session.SelectedRelationship!.Reason);

        Assert.True(await session.FocusContextNodeAsync("b"));
        Assert.Equal("b", session.Neighborhood!.FocusNodeId);
        Assert.True(await session.GoBackAsync());
        Assert.Equal("a", session.Neighborhood!.FocusNodeId);
        Assert.Equal(ExplorerViewMode.Context, session.ViewMode);
        Assert.True(await session.GoBackAsync());
        Assert.Equal("root", session.Neighborhood!.FocusNodeId);
        Assert.Equal(ExplorerViewMode.Structure, session.ViewMode);
    }

    [Fact]
    public async Task ConnectedUp_UsesTheServerAuthoredOpaqueParentWithoutPathInference()
    {
        var provider = ContextProvider.Immediate("opaque-root", "Node-A", "node-a");
        using var session = new ExplorerSession();
        await session.OpenRootAsync(provider, provider);
        Assert.True(await session.SwitchToContextAsync("Node-A"));

        Assert.True(session.CanGoUp);
        Assert.True(await session.GoUpAsync());

        Assert.Equal("opaque-root", session.Neighborhood!.FocusNodeId);
        Assert.Equal(ExplorerViewMode.Context, session.ViewMode);
        Assert.Contains("First report", session.NavigationTrailSummary, StringComparison.Ordinal);
        Assert.DoesNotContain("Node-A", session.NavigationTrailSummary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ConnectedTrail_UsesHumanNamesAndExactOpaqueTargetsAcrossCaseDistinctHistory()
    {
        var provider = ContextProvider.Immediate("opaque-root", "Node-A", "node-a");
        using var session = new ExplorerSession();
        await session.OpenRootAsync(provider, provider);
        Assert.True(await session.SwitchToContextAsync("Node-A"));
        Assert.True(await session.FocusContextNodeAsync("node-a"));
        Assert.True(await session.FocusContextNodeAsync("opaque-root"));

        Assert.Equal(["Second report", "First report", "Authorized folder"],
            session.NavigationTrail.Select(entry => entry.DisplayName));
        Assert.DoesNotContain("opaque", session.NavigationTrailSummary, StringComparison.OrdinalIgnoreCase);
        Assert.True(await session.NavigateTrailAsync(session.NavigationTrail[1]));
        Assert.Equal("Node-A", session.Neighborhood!.FocusNodeId);
        Assert.Equal(ExplorerViewMode.Context, session.ViewMode);
        Assert.Equal(0, session.NavigationHistoryCount);
        Assert.True(await session.GoBackAsync());
        Assert.Equal("opaque-root", session.Neighborhood.FocusNodeId);
        Assert.Equal(ExplorerViewMode.Structure, session.ViewMode);
    }

    [Fact]
    public async Task FailedConnectedTrailJump_RetainsModeFocusAndHistory()
    {
        var provider = ContextProvider.Immediate();
        using var session = new ExplorerSession();
        await session.OpenRootAsync(provider, provider);
        Assert.True(await session.SwitchToContextAsync("a"));
        Assert.True(await session.FocusContextNodeAsync("b"));
        provider.FailContextRequests = true;
        var priorScene = session.Neighborhood;

        Assert.False(await session.NavigateTrailAsync(session.NavigationTrail[0]));

        Assert.Same(priorScene, session.Neighborhood);
        Assert.Equal(ExplorerViewMode.Context, session.ViewMode);
        Assert.Equal(1, session.NavigationHistoryCount);
        provider.FailContextRequests = false;
        Assert.True(await session.GoBackAsync());
        Assert.Equal("a", session.Neighborhood!.FocusNodeId);
    }

    [Fact]
    public async Task ContextRequestGeneration_RejectsLateObsoleteFocus()
    {
        var provider = ContextProvider.Controlled();
        using var session = new ExplorerSession();
        await session.OpenRootAsync(provider, provider);

        var first = session.SwitchToContextAsync("a");
        await provider.WaitForContextRequestAsync("a");
        var second = session.SwitchToContextAsync("b");
        await provider.WaitForContextRequestAsync("b");
        provider.Complete("b");
        Assert.True(await second);
        provider.Complete("a");
        Assert.False(await first);

        Assert.Equal("b", session.Neighborhood!.FocusNodeId);
        Assert.Equal(1, provider.StaleResponseRejections);
    }

    [Fact]
    public async Task NewConnectedProvider_InvalidatesOldContextIdentityAndHistory()
    {
        var first = ContextProvider.Immediate();
        using var session = new ExplorerSession();
        await session.OpenRootAsync(first, first);
        Assert.True(await session.SwitchToContextAsync("a"));
        Assert.True(await session.FocusContextNodeAsync("b"));

        var replacement = ContextProvider.Immediate("new-root", "new-a", "new-b");
        await session.OpenRootAsync(replacement, replacement);

        Assert.Equal(ExplorerViewMode.Structure, session.ViewMode);
        Assert.Equal("new-root", session.Neighborhood!.FocusNodeId);
        Assert.DoesNotContain(session.Neighborhood.Nodes, node => node.Id is "a" or "b");
        Assert.False(session.CanGoBack);
    }

    [Fact]
    public async Task ContextFilter_IsLocalReversibleAndClearedForNewProviderGrant()
    {
        var provider = ContextProvider.Immediate();
        using var session = new ExplorerSession();
        await session.OpenRootAsync(provider, provider);
        Assert.True(await session.SwitchToContextAsync("a"));

        Assert.True(session.ApplyContextFilter(new ContextFilter(ExplorerRelationshipKind.Entity)));
        Assert.Single(session.Neighborhood!.Nodes);
        Assert.Contains("No relationships match", session.Status, StringComparison.Ordinal);
        Assert.True(session.ContextFilter.IsActive);

        Assert.True(session.ClearContextFilter());
        Assert.Equal(2, session.Neighborhood.Nodes.Count);
        Assert.False(session.ContextFilter.IsActive);

        var replacement = ContextProvider.Immediate("new-root", "new-a", "new-b");
        await session.OpenRootAsync(replacement, replacement);
        Assert.Equal(ContextFilter.None, session.ContextFilter);
        Assert.Null(session.ContextFilterSummary);
    }

    private sealed class ContextlessProvider : IExplorerProvider, IExplorerSearchProvider
    {
        public string AccessRoot => Path.GetFullPath("standalone-root");

        public Task<ExplorerDirectorySnapshot> GetDirectoryAsync(string path, CancellationToken cancellationToken) =>
            Task.FromResult(new ExplorerDirectorySnapshot(Entry(AccessRoot, ExplorerNodeKind.Folder), []));

        public Task<ExplorerSearchResult> SearchAsync(SearchRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new ExplorerSearchResult([], false, 0));
    }

    private sealed class ContextProvider :
        IExplorerProvider,
        IExplorerSearchProvider,
        IExplorerContextProvider,
        IExplorerProviderDiagnostics
    {
        private readonly Dictionary<string, TaskCompletionSource<ExplorerContextSnapshot>> _requests = new(ExplorerIdentity.Comparer);
        private readonly bool _immediate;
        private readonly ExplorerEntry _root;
        private readonly ExplorerEntry _a;
        private readonly ExplorerEntry _b;

        private ContextProvider(bool immediate, string rootId, string firstId, string secondId)
        {
            _immediate = immediate;
            _root = Entry(rootId, ExplorerNodeKind.Folder) with { Name = "Authorized folder" };
            _a = Entry(firstId, ExplorerNodeKind.File, _root.Id) with { Name = "First report" };
            _b = Entry(secondId, ExplorerNodeKind.File, _root.Id) with { Name = "Second report" };
        }

        public static ContextProvider Immediate(string rootId = "root", string firstId = "a", string secondId = "b") =>
            new(true, rootId, firstId, secondId);

        public static ContextProvider Controlled() => new(false, "root", "a", "b");

        public string AccessRoot => _root.Id;
        public ExplorerProviderMode Mode => ExplorerProviderMode.Connected;
        public int StaleResponseRejections { get; private set; }
        public bool FailContextRequests { get; set; }

        public Task<ExplorerDirectorySnapshot> GetDirectoryAsync(string path, CancellationToken cancellationToken) =>
            Task.FromResult(new ExplorerDirectorySnapshot(_root, [_a, _b]));

        public Task<ExplorerSearchResult> SearchAsync(SearchRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new ExplorerSearchResult([], false, 0));

        public Task<ExplorerContextSnapshot> GetContextAsync(string nodeId, CancellationToken cancellationToken)
        {
            if (FailContextRequests)
            {
                throw new IOException("Controlled Context failure.");
            }

            if (_immediate)
            {
                return Task.FromResult(Snapshot(nodeId));
            }

            lock (_requests)
            {
                if (!_requests.TryGetValue(nodeId, out var request))
                {
                    request = new TaskCompletionSource<ExplorerContextSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
                    _requests.Add(nodeId, request);
                }

                return request.Task;
            }
        }

        public async Task WaitForContextRequestAsync(string nodeId)
        {
            var deadline = DateTime.UtcNow.AddSeconds(10);
            while (DateTime.UtcNow < deadline)
            {
                lock (_requests)
                {
                    if (_requests.ContainsKey(nodeId))
                    {
                        return;
                    }
                }

                await Task.Delay(10);
            }

            throw new TimeoutException("The Context request was not observed.");
        }

        public void Complete(string nodeId)
        {
            lock (_requests)
            {
                _requests[nodeId].TrySetResult(Snapshot(nodeId));
            }
        }

        public void ReportStaleResponseRejected() => StaleResponseRejections++;

        private ExplorerContextSnapshot Snapshot(string nodeId)
        {
            var focus = ExplorerIdentity.Equals(nodeId, _root.Id)
                ? _root
                : ExplorerIdentity.Equals(nodeId, _a.Id) ? _a : _b;
            var related = ExplorerIdentity.Equals(focus.Id, _root.Id)
                ? _a
                : ExplorerIdentity.Equals(focus.Id, _a.Id) ? _b : _a;
            var relation = new ExplorerRelationship(
                $"{focus.Id}-{related.Id}",
                focus.Id,
                related.Id,
                ExplorerRelationshipKind.Topic,
                80,
                "Shared topic",
                ExplorerRelationshipEvidenceClass.Derived,
                "OmniSorSe Content Intelligence");
            return new ExplorerContextSnapshot(focus, [related], [], [relation]);
        }
    }

    private static ExplorerEntry Entry(
        string id,
        ExplorerNodeKind kind,
        string? parent = null) => new(
            id,
            id,
            id,
            kind,
            IsNavigable: kind == ExplorerNodeKind.Folder,
            NavigationTarget: id,
            ParentNavigationTarget: parent);
}
