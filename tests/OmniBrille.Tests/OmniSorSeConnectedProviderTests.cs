using System.Globalization;
using OmniBrille.Core;
using OmniBrille.Desktop.Presentation;
using OmniBrille.Infrastructure.OmniSorSe;
using Protocol = OmniSorSe.ExplorerProtocol;

namespace OmniBrille.Tests;

public sealed class OmniSorSeConnectedProviderTests
{
    [Fact]
    public async Task ProgressiveStructure_UsesOpaqueIdsAndProtocolTotals()
    {
        var root = Node("opaque-root", "Indexed", Protocol.ExplorerNodeKind.Source, null, childCount: 80);
        var children = Enumerable.Range(0, 80)
            .Select(index => Node($"opaque-{index:D3}", $"Item {index:D3}", Protocol.ExplorerNodeKind.File, root.Id))
            .ToArray();
        var client = new FakeProtocolClient(root, children);
        var provider = new OmniSorSeConnectedProvider(client, Info(), root);
        var batches = new List<ExplorerDirectoryBatch>();

        await foreach (var batch in provider.GetDirectoryBatchesAsync(root.Id, 32, CancellationToken.None))
        {
            batches.Add(batch);
        }

        Assert.Equal([32, 32, 16], batches.Select(batch => batch.AddedChildren.Count));
        Assert.True(batches[^1].IsComplete);
        Assert.Equal(80, batches[^1].TotalChildCount);
        Assert.All(batches.SelectMany(batch => batch.AddedChildren), entry =>
        {
            Assert.StartsWith("opaque-", entry.Target, StringComparison.Ordinal);
            Assert.DoesNotContain(Path.DirectorySeparatorChar, entry.Target);
        });
    }

    [Fact]
    public async Task LargeConnectedSource_IsClientBoundedAndAggregatesToSceneBudget()
    {
        var root = Node("large-root", "Large indexed root", Protocol.ExplorerNodeKind.Source, null, childCount: 5_000);
        var children = Enumerable.Range(0, 5_000)
            .Select(index => Node($"large-{index:D4}", $"Item {index:D4}", Protocol.ExplorerNodeKind.File, root.Id))
            .ToArray();
        var provider = new OmniSorSeConnectedProvider(new FakeProtocolClient(root, children), Info(), root);

        var snapshot = await provider.GetDirectoryAsync(root.Id, CancellationToken.None);
        var graph = new GraphNeighborhoodBuilder().Build(snapshot);

        Assert.Equal(512, snapshot.Children.Count);
        Assert.Equal(5_000, snapshot.TotalChildCount);
        Assert.True(snapshot.WasTruncated);
        Assert.Equal(GraphNeighborhoodBuilder.DefaultNodeBudget, graph.Nodes.Count);
        Assert.Contains(graph.Nodes, node => node.Kind == OmniBrille.Core.ExplorerNodeKind.Aggregate);
    }

    [Fact]
    public async Task SearchAndDetails_AdaptOnlyProtocolFields()
    {
        var root = Node("root", "Indexed", Protocol.ExplorerNodeKind.Source, null);
        var file = Node("file-id", "report.pdf", Protocol.ExplorerNodeKind.File, root.Id, size: 2048);
        var client = new FakeProtocolClient(root, [file])
        {
            SearchResult = new Protocol.ExplorerSearchResult(
                [new Protocol.ExplorerSearchHit(file, 1, 0.9, "Indexed name match", "bounded snippet", "Name")],
                false,
                "Authorized indexed scope",
                false),
            Details = new Protocol.ExplorerNodeDetails(
                file,
                DateTimeOffset.Parse("2026-01-01T00:00:00Z", CultureInfo.InvariantCulture),
                DateTimeOffset.Parse("2026-02-01T00:00:00Z", CultureInfo.InvariantCulture),
                "Bounded summary",
                [new Protocol.ExplorerConcept("Planning", "Topic", "High", false, "deterministic")],
                [],
                null,
                ["Related by filename"],
                true),
        };
        var provider = new OmniSorSeConnectedProvider(client, Info(), root);

        var search = await provider.SearchAsync(new SearchRequest(root.Id, "report"), CancellationToken.None);
        var details = await provider.GetNodeDetailsAsync(file.Id, CancellationToken.None);

        var hit = Assert.Single(search.Hits);
        Assert.Equal(file.Id, hit.Target);
        Assert.Equal(root.Id, hit.ParentNavigationTarget);
        Assert.Equal("Indexed name match", hit.Explanation);
        Assert.Equal("Bounded summary", details!.Summary);
        Assert.Equal(["Planning"], details.Topics);
        Assert.True(details.IsFullyIndexed);
    }

    [Fact]
    public async Task ConnectedSession_SearchResultFocusUsesParentOpaqueId()
    {
        var root = Node("root-id", "Root", Protocol.ExplorerNodeKind.Source, null);
        var folder = Node("folder-id", "Folder", Protocol.ExplorerNodeKind.Folder, root.Id);
        var file = Node("file-id", "Found.txt", Protocol.ExplorerNodeKind.File, folder.Id);
        var client = new FakeProtocolClient(root, [folder, file]);
        var provider = new OmniSorSeConnectedProvider(client, Info(), root);
        using var session = new ExplorerSession();
        await session.OpenRootAsync(provider, provider);

        var focused = await session.FocusSearchHitAsync(
            new OmniBrille.Core.ExplorerSearchHit(
                file.Id,
                file.Name,
                file.Name,
                OmniBrille.Core.ExplorerNodeKind.File,
                file.Id,
                folder.Id));

        Assert.True(focused);
        Assert.Equal(folder.Id, session.Neighborhood!.FocusNodeId);
        Assert.Equal(ExplorerProviderMode.Connected, session.ProviderMode);
    }

    [Fact]
    public async Task ProtocolCancellation_ReachesConnectedClient()
    {
        var root = Node("root", "Root", Protocol.ExplorerNodeKind.Source, null);
        var client = new FakeProtocolClient(root, []) { BlockChildren = true };
        var provider = new OmniSorSeConnectedProvider(client, Info(), root);
        using var cancellation = new CancellationTokenSource();
        var loading = provider.GetDirectoryAsync(root.Id, cancellation.Token);

        await client.ChildrenStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => loading);
        Assert.True(client.CancellationObserved);
    }

    [Fact]
    public async Task StructuralPaging_RejectsNonProgressingContinuation()
    {
        var root = Node("root", "Root", Protocol.ExplorerNodeKind.Source, null);
        var child = Node("child", "Child", Protocol.ExplorerNodeKind.File, root.Id);
        var client = new FakeProtocolClient(root, [child]) { RepeatContinuation = true };
        var provider = new OmniSorSeConnectedProvider(client, Info(), root);

        await Assert.ThrowsAsync<ExplorerProtocolMalformedResponseException>(() =>
            provider.GetDirectoryAsync(root.Id, CancellationToken.None));
    }

    [Fact]
    public async Task StructuralPaging_RejectsNodeFromDifferentParent()
    {
        var root = Node("root", "Root", Protocol.ExplorerNodeKind.Source, null);
        var child = Node("child", "Child", Protocol.ExplorerNodeKind.File, "different-parent");
        var provider = new OmniSorSeConnectedProvider(
            new FakeProtocolClient(root, [child]) { ReturnUnscopedChildren = true },
            Info(),
            root);

        await Assert.ThrowsAsync<ExplorerProtocolMalformedResponseException>(() =>
            provider.GetDirectoryAsync(root.Id, CancellationToken.None));
    }

    private static Protocol.ExplorerProtocolInfo Info() => new(
        1,
        0,
        "OmniSorSe",
        "2.4.0",
        Protocol.ExplorerCapability.Structure | Protocol.ExplorerCapability.Search,
        new Protocol.ExplorerProtocolLimits(65536, 1048576, 500, 256, 512, 100, 100, 2, 320, 32, 32, 256, 4, 15),
        true,
        "Local named pipe");

    private static Protocol.ExplorerNode Node(
        string id,
        string name,
        Protocol.ExplorerNodeKind kind,
        string? parent,
        int childCount = 0,
        long? size = null) =>
        new(id, name, kind, parent, null, size, null, new Dictionary<string, string>(), childCount, 0);

    private sealed class FakeProtocolClient : IExplorerProtocolClient
    {
        private readonly Dictionary<string, Protocol.ExplorerNode> _nodes;
        private readonly IReadOnlyList<Protocol.ExplorerNode> _children;

        public FakeProtocolClient(Protocol.ExplorerNode root, IReadOnlyList<Protocol.ExplorerNode> children)
        {
            _children = children;
            _nodes = children.Append(root).ToDictionary(node => node.Id, StringComparer.Ordinal);
            Grant = new OmniSorSeSessionGrant(
                "named-pipe",
                "ose-0123456789abcdef0123456789abcdef",
                "session",
                "secret",
                DateTimeOffset.UtcNow.AddMinutes(1),
                1,
                0);
            Details = new Protocol.ExplorerNodeDetails(root, null, null, null, [], [], null, [], true);
        }

        public OmniSorSeSessionGrant Grant { get; }
        public OmniSorSeConnectionDiagnostics Diagnostics { get; } = new(
            OmniSorSeConnectionState.Connected, "named-pipe", "1.0", TimeSpan.Zero, 0, 0, 0, 0, 0, null);
        public Protocol.ExplorerSearchResult SearchResult { get; init; } = new([], false, "Authorized indexed scope", false);
        public Protocol.ExplorerNodeDetails Details { get; init; }
        public bool BlockChildren { get; init; }
        public bool RepeatContinuation { get; init; }
        public bool ReturnUnscopedChildren { get; init; }
        public TaskCompletionSource ChildrenStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool CancellationObserved { get; private set; }

        public Task<Protocol.ExplorerProtocolInfo> GetProtocolInfoAsync(CancellationToken cancellationToken) => Task.FromResult(Info());
        public Task<Protocol.ExplorerNodePage> GetAccessibleRootsAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new Protocol.ExplorerNodePage([_nodes.Values.First(node => node.Kind == Protocol.ExplorerNodeKind.Source)], 1, false, null));

        public async Task<Protocol.ExplorerNodePage> GetChildrenAsync(
            Protocol.ExplorerChildrenRequest request,
            CancellationToken cancellationToken)
        {
            ChildrenStarted.TrySetResult();
            if (BlockChildren)
            {
                try
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    CancellationObserved = true;
                    throw;
                }
            }

            var offset = request.ContinuationToken is null ? 0 : int.Parse(request.ContinuationToken[2..], System.Globalization.CultureInfo.InvariantCulture);
            var count = request.MaximumResults ?? 64;
            IReadOnlyList<Protocol.ExplorerNode> matching = ReturnUnscopedChildren
                ? _children
                : _children.Where(node => node.ParentId == request.ParentNodeId).ToArray();
            var page = matching.Skip(offset).Take(count).ToArray();
            var total = matching.Count;
            var next = RepeatContinuation
                ? "o:0"
                : offset + page.Length < total ? $"o:{offset + page.Length}" : null;
            return new Protocol.ExplorerNodePage(page, Math.Max(total, page.Length), next is not null, next);
        }

        public Task<Protocol.ExplorerNeighborhood> GetNeighborhoodAsync(Protocol.ExplorerNeighborhoodRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new Protocol.ExplorerNeighborhood(request.NodeId, [_nodes[request.NodeId]], [], false, null));

        public Task<Protocol.ExplorerSearchResult> SearchAsync(Protocol.ExplorerSearchRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(SearchResult);

        public Task<Protocol.ExplorerNodeDetails> GetNodeDetailsAsync(Protocol.ExplorerNodeDetailsRequest request, CancellationToken cancellationToken)
        {
            var node = _nodes[request.NodeId];
            return Task.FromResult(Details.Node.Id == node.Id
                ? Details
                : new Protocol.ExplorerNodeDetails(node, null, null, null, [], [], null, [], true));
        }

        public void ReportStaleResponseRejected()
        {
        }
    }
}
