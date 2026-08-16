using OmniBrille.Core;
using OmniBrille.Desktop.Presentation;

namespace OmniBrille.Tests;

public sealed class HybridSessionTests
{
    [Fact]
    public async Task Standalone_HybridIsUnavailableWithoutInventedContext()
    {
        var provider = new StandaloneProvider();
        using var session = new ExplorerSession();
        await session.OpenRootAsync(provider, provider);

        var switched = await session.SwitchToHybridAsync();

        Assert.False(switched);
        Assert.Equal(ExplorerViewMode.Structure, session.ViewMode);
        Assert.Equal("Hybrid exploration requires OmniSorSe.", session.Status);
    }

    [Fact]
    public async Task Connected_ModeHistoryRestoresHybridContextThenStructure()
    {
        var provider = HybridProvider.Immediate();
        using var session = new ExplorerSession();
        await session.OpenRootAsync(provider, provider);
        session.SelectNode("a");

        Assert.True(await session.SwitchToContextAsync());
        Assert.True(await session.SwitchToHybridAsync());
        Assert.Equal(ExplorerViewMode.Hybrid, session.ViewMode);
        Assert.Equal("a", session.Neighborhood!.FocusNodeId);
        Assert.True(await session.FocusHybridNodeAsync("b"));
        Assert.Equal("b", session.Neighborhood.FocusNodeId);

        Assert.True(await session.GoBackAsync());
        Assert.Equal(ExplorerViewMode.Hybrid, session.ViewMode);
        Assert.Equal("a", session.Neighborhood.FocusNodeId);
        Assert.True(await session.GoBackAsync());
        Assert.Equal(ExplorerViewMode.Context, session.ViewMode);
        Assert.Equal("a", session.Neighborhood.FocusNodeId);
        Assert.True(await session.GoBackAsync());
        Assert.Equal(ExplorerViewMode.Structure, session.ViewMode);
        Assert.Equal("root", session.Neighborhood.FocusNodeId);
    }

    [Fact]
    public async Task Hybrid_FilterChangesOnlyContextLayerAndIsReversible()
    {
        var provider = HybridProvider.Immediate();
        using var session = new ExplorerSession();
        await session.OpenRootAsync(provider, provider);
        session.SelectNode("a");
        Assert.True(await session.SwitchToHybridAsync());
        var structuralCount = session.Neighborhood!.Edges.Count(edge => edge.Kind == ExplorerGraphEdgeKind.Structural);

        Assert.True(session.ApplyContextFilter(new ContextFilter(ExplorerRelationshipKind.Entity)));
        Assert.Equal(structuralCount, session.Neighborhood.Edges.Count(edge => edge.Kind == ExplorerGraphEdgeKind.Structural));
        Assert.DoesNotContain(session.Neighborhood.Edges, edge => edge.Kind == ExplorerGraphEdgeKind.Contextual);
        Assert.Contains("Structural orientation remains available", session.Status, StringComparison.Ordinal);

        Assert.True(session.ClearContextFilter());
        Assert.Contains(session.Neighborhood.Edges, edge => edge.Kind == ExplorerGraphEdgeKind.Contextual);
    }

    [Fact]
    public async Task Hybrid_ComposesRetainedStructureWhenContextResponseContainsOnlyRelationships()
    {
        var provider = HybridProvider.ContextOnly();
        using var session = new ExplorerSession();
        await session.OpenRootAsync(provider, provider);
        session.SelectNode("a");

        Assert.True(await session.SwitchToHybridAsync());

        Assert.Equal(1, provider.DirectoryRequestCount);
        Assert.Contains(session.Neighborhood!.Edges, edge =>
            edge.Kind == ExplorerGraphEdgeKind.Structural && edge.SourceId == "root" && edge.TargetId == "a");
        Assert.Contains(session.Neighborhood.Edges, edge =>
            edge.Kind == ExplorerGraphEdgeKind.Structural && edge.SourceId == "root" && edge.TargetId == "b");
        Assert.Equal(
            ExplorerNodeRole.Structural | ExplorerNodeRole.Contextual,
            session.Neighborhood.Nodes.Single(node => node.Id == "a").Roles);
    }

    [Fact]
    public async Task Hybrid_RefocusOutsideRetainedStructureUsesOneBoundedParentRead()
    {
        var provider = HybridProvider.ExternalRelated();
        using var session = new ExplorerSession();
        await session.OpenRootAsync(provider, provider);
        session.SelectNode("a");
        Assert.True(await session.SwitchToHybridAsync());

        Assert.True(await session.FocusHybridNodeAsync("c"));

        Assert.Equal(2, provider.DirectoryRequestCount);
        Assert.Equal("other-root", provider.LastDirectoryTarget);
        Assert.Contains(session.Neighborhood!.Edges, edge =>
            edge.Kind == ExplorerGraphEdgeKind.Structural &&
            edge.SourceId == "other-root" &&
            edge.TargetId == "c");
    }

    [Fact]
    public async Task HybridRequestGeneration_RejectsLateObsoleteFocus()
    {
        var provider = HybridProvider.Controlled();
        using var session = new ExplorerSession();
        await session.OpenRootAsync(provider, provider);

        var first = session.SwitchToHybridAsync("a");
        await provider.WaitForContextRequestAsync("a");
        var second = session.SwitchToHybridAsync("b");
        await provider.WaitForContextRequestAsync("b");
        provider.Complete("b");
        Assert.True(await second);
        provider.Complete("a");
        Assert.False(await first);

        Assert.Equal(ExplorerViewMode.Hybrid, session.ViewMode);
        Assert.Equal("b", session.Neighborhood!.FocusNodeId);
        Assert.Equal(1, provider.StaleResponseRejections);
    }

    [Fact]
    public async Task HybridFailure_RetainsUsableStructure()
    {
        var provider = HybridProvider.Failing();
        using var session = new ExplorerSession();
        await session.OpenRootAsync(provider, provider);
        session.SelectNode("a");

        var switched = await session.SwitchToHybridAsync();

        Assert.False(switched);
        Assert.Equal(ExplorerViewMode.Structure, session.ViewMode);
        Assert.Equal("root", session.Neighborhood!.FocusNodeId);
        Assert.Contains("structural graph is retained", session.Status, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HybridSearch_UsesExistingConnectedSearchWithContextFlag()
    {
        var provider = HybridProvider.Immediate();
        using var session = new ExplorerSession();
        await session.OpenRootAsync(provider, provider);
        session.SelectNode("a");
        Assert.True(await session.SwitchToHybridAsync());

        await session.SearchAsync("authoritative query");

        Assert.NotNull(provider.LastSearchRequest);
        Assert.True(provider.LastSearchRequest!.IncludeContext);
        Assert.Equal("authoritative query", provider.LastSearchRequest.Query);
    }

    [Fact]
    public async Task ReplacementProvider_InvalidatesHybridIdentityAndHistory()
    {
        var provider = HybridProvider.Immediate();
        using var session = new ExplorerSession();
        await session.OpenRootAsync(provider, provider);
        session.SelectNode("a");
        Assert.True(await session.SwitchToHybridAsync());
        Assert.True(await session.FocusHybridNodeAsync("b"));

        var replacement = HybridProvider.Immediate("new-root", "new-a", "new-b");
        await session.OpenRootAsync(replacement, replacement);

        Assert.Equal(ExplorerViewMode.Structure, session.ViewMode);
        Assert.Equal("new-root", session.Neighborhood!.FocusNodeId);
        Assert.False(session.CanGoBack);
        Assert.DoesNotContain(session.Neighborhood.Nodes, node => node.Id is "a" or "b");
    }

    private sealed class StandaloneProvider : IExplorerProvider, IExplorerSearchProvider
    {
        public string AccessRoot => Path.GetFullPath("hybrid-standalone-root");

        public Task<ExplorerDirectorySnapshot> GetDirectoryAsync(string path, CancellationToken cancellationToken) =>
            Task.FromResult(new ExplorerDirectorySnapshot(Entry(AccessRoot, ExplorerNodeKind.Folder), []));

        public Task<ExplorerSearchResult> SearchAsync(SearchRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new ExplorerSearchResult([], false, 0));
    }

    private sealed class HybridProvider :
        IExplorerProvider,
        IExplorerSearchProvider,
        IExplorerContextProvider,
        IExplorerProviderDiagnostics
    {
        private readonly Dictionary<string, TaskCompletionSource<ExplorerContextSnapshot>> _requests = new(ExplorerIdentity.Comparer);
        private readonly bool _immediate;
        private readonly bool _fail;
        private readonly bool _contextIncludesStructure;
        private readonly bool _externalRelated;
        private readonly ExplorerEntry _root;
        private readonly ExplorerEntry _a;
        private readonly ExplorerEntry _b;
        private readonly ExplorerEntry _otherRoot;
        private readonly ExplorerEntry _c;

        private HybridProvider(
            bool immediate,
            bool fail,
            bool contextIncludesStructure,
            bool externalRelated,
            string rootId,
            string firstId,
            string secondId)
        {
            _immediate = immediate;
            _fail = fail;
            _contextIncludesStructure = contextIncludesStructure;
            _externalRelated = externalRelated;
            _root = Entry(rootId, ExplorerNodeKind.Folder);
            _a = Entry(firstId, ExplorerNodeKind.File, rootId);
            _b = Entry(secondId, ExplorerNodeKind.File, rootId);
            _otherRoot = Entry("other-root", ExplorerNodeKind.Folder);
            _c = Entry("c", ExplorerNodeKind.File, _otherRoot.Id);
        }

        public static HybridProvider Immediate(string rootId = "root", string firstId = "a", string secondId = "b") =>
            new(true, false, true, false, rootId, firstId, secondId);

        public static HybridProvider ContextOnly() => new(true, false, false, false, "root", "a", "b");

        public static HybridProvider ExternalRelated() => new(true, false, false, true, "root", "a", "b");

        public static HybridProvider Controlled() => new(false, false, true, false, "root", "a", "b");

        public static HybridProvider Failing() => new(true, true, true, false, "root", "a", "b");

        public string AccessRoot => _root.Id;
        public ExplorerProviderMode Mode => ExplorerProviderMode.Connected;
        public int StaleResponseRejections { get; private set; }
        public int DirectoryRequestCount { get; private set; }
        public string? LastDirectoryTarget { get; private set; }
        public SearchRequest? LastSearchRequest { get; private set; }

        public Task<ExplorerDirectorySnapshot> GetDirectoryAsync(string path, CancellationToken cancellationToken)
        {
            DirectoryRequestCount++;
            LastDirectoryTarget = path;
            return Task.FromResult(ExplorerIdentity.Equals(path, _otherRoot.Id)
                ? new ExplorerDirectorySnapshot(_otherRoot, [_c])
                : new ExplorerDirectorySnapshot(_root, [_a, _b]));
        }

        public Task<ExplorerSearchResult> SearchAsync(SearchRequest request, CancellationToken cancellationToken)
        {
            LastSearchRequest = request;
            return Task.FromResult(new ExplorerSearchResult([], false, 0));
        }

        public Task<ExplorerContextSnapshot> GetContextAsync(string nodeId, CancellationToken cancellationToken)
        {
            if (_fail)
            {
                throw new IOException("Controlled provider failure.");
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

            throw new TimeoutException("The Hybrid request was not observed.");
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
            var focus = ExplorerIdentity.Equals(nodeId, _a.Id)
                ? _a
                : _externalRelated && ExplorerIdentity.Equals(nodeId, _c.Id)
                    ? _c
                    : _b;
            var related = _externalRelated
                ? ExplorerIdentity.Equals(focus.Id, _a.Id) ? _c : _a
                : ExplorerIdentity.Equals(focus.Id, _a.Id) ? _b : _a;
            return new ExplorerContextSnapshot(
                focus,
                [_root, related],
                _contextIncludesStructure
                    ? [new ExplorerEdge(_root.Id, focus.Id), new ExplorerEdge(_root.Id, related.Id)]
                    : [],
                [new ExplorerRelationship(
                    $"{focus.Id}-{related.Id}",
                    focus.Id,
                    related.Id,
                    ExplorerRelationshipKind.Topic,
                    82,
                    "Shared authoritative topic",
                    ExplorerRelationshipEvidenceClass.Derived,
                    "OmniSorSe Content Intelligence")]);
        }
    }

    private static ExplorerEntry Entry(string id, ExplorerNodeKind kind, string? parent = null) => new(
        id,
        id,
        id,
        kind,
        IsNavigable: true,
        NavigationTarget: id,
        ParentNavigationTarget: parent);
}
