using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Channels;
using OmniBrille.Core;
using Protocol = OmniSorSe.ExplorerProtocol;

namespace OmniBrille.Infrastructure.OmniSorSe;

public sealed class OmniSorSeConnectedProvider :
    IExplorerProvider,
    IProgressiveExplorerProvider,
    IExplorerSearchProvider,
    IExplorerContextProvider,
    IExplorerNodeDetailsProvider,
    IExplorerProviderDiagnostics
{
    private const int MaximumAdaptedChildren = 512;
    private const int MaximumPageSize = 64;
    private readonly IExplorerProtocolClient _client;
    private readonly Protocol.ExplorerProtocolInfo _protocolInfo;
    private readonly ConcurrentDictionary<string, string> _displayPaths = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, Protocol.ExplorerNode> _issuedNodes = new(StringComparer.Ordinal);
    private readonly BoundedLruCache<string, ExplorerContextSnapshot> _contextCache = new(8, StringComparer.Ordinal);
    private readonly Channel<bool> _contextGate = CreateContextGate();
    private readonly object _contextCacheLock = new();

    public OmniSorSeConnectedProvider(
        IExplorerProtocolClient client,
        Protocol.ExplorerProtocolInfo protocolInfo,
        Protocol.ExplorerNode accessibleRoot)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _protocolInfo = protocolInfo ?? throw new ArgumentNullException(nameof(protocolInfo));
        ArgumentNullException.ThrowIfNull(accessibleRoot);
        if (accessibleRoot.Kind != Protocol.ExplorerNodeKind.Source)
        {
            throw new ArgumentException("A connected provider must begin at an authorized OmniSorSe source node.", nameof(accessibleRoot));
        }

        AccessRoot = accessibleRoot.Id;
        DisplayRoot = accessibleRoot.AuthorizedPath ?? accessibleRoot.Name;
        Register(accessibleRoot, DisplayRoot);
    }

    public string AccessRoot { get; }

    public string DisplayRoot { get; }

    public ExplorerProviderMode Mode => ExplorerProviderMode.Connected;

    public bool SupportsContext =>
        (_protocolInfo.Capabilities & (Protocol.ExplorerCapability.Context | Protocol.ExplorerCapability.RelatedFiles)) ==
        (Protocol.ExplorerCapability.Context | Protocol.ExplorerCapability.RelatedFiles);

    public void ReportStaleResponseRejected() => _client.ReportStaleResponseRejected();

    public bool IsProviderFailure(Exception exception) => exception is
        IOException or
        UnauthorizedAccessException or
        InvalidDataException or
        TimeoutException or
        ExplorerProtocolException or
        ExplorerProtocolMalformedResponseException;

    public async Task<ExplorerDirectorySnapshot> GetDirectoryAsync(
        string path,
        CancellationToken cancellationToken)
    {
        var children = new List<ExplorerEntry>();
        ExplorerDirectoryBatch? last = null;
        await foreach (var batch in GetDirectoryBatchesAsync(path, MaximumPageSize, cancellationToken)
                           .ConfigureAwait(false))
        {
            children.AddRange(batch.AddedChildren);
            last = batch;
        }

        if (last is null)
        {
            throw new ExplorerProtocolMalformedResponseException("OmniSorSe returned no structural batch.");
        }

        return new ExplorerDirectorySnapshot(
            last.Focus,
            children,
            last.Failure,
            last.Warning,
            last.TotalChildCount,
            last.WasTruncated);
    }

    public async IAsyncEnumerable<ExplorerDirectoryBatch> GetDirectoryBatchesAsync(
        string path,
        int batchSize,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(path) || batchSize <= 0)
        {
            throw new ArgumentException("A valid issued node ID and positive batch size are required.", nameof(path));
        }

        var details = await _client.GetNodeDetailsAsync(
            new Protocol.ExplorerNodeDetailsRequest(path),
            cancellationToken).ConfigureAwait(false);
        var focus = MapNode(details.Node);
        if (focus.Kind == ExplorerNodeKind.File)
        {
            yield return new ExplorerDirectoryBatch(focus, [], 0, true, TotalChildCount: 0);
            yield break;
        }

        var pageSize = Math.Min(Math.Min(batchSize, MaximumPageSize), _protocolInfo.Limits.MaximumNodes);
        string? continuation = null;
        var observedContinuations = new HashSet<string>(StringComparer.Ordinal);
        var observed = 0;
        do
        {
            cancellationToken.ThrowIfCancellationRequested();
            var page = await _client.GetChildrenAsync(
                new Protocol.ExplorerChildrenRequest(path, pageSize, continuation),
                cancellationToken).ConfigureAwait(false);
            if (page.Nodes.Any(node => !string.Equals(node.ParentId, path, StringComparison.Ordinal)))
            {
                throw new ExplorerProtocolMalformedResponseException(
                    "OmniSorSe returned a child outside the requested structural parent.");
            }

            var remaining = MaximumAdaptedChildren - observed;
            var accepted = page.Nodes.Take(Math.Max(0, remaining)).Select(MapNode).ToArray();
            observed += accepted.Length;
            continuation = page.ContinuationToken;
            if (continuation is not null &&
                (page.Nodes.Count == 0 || !observedContinuations.Add(continuation)))
            {
                throw new ExplorerProtocolMalformedResponseException(
                    "OmniSorSe returned a non-progressing structural continuation.");
            }

            var clientBoundReached = observed >= MaximumAdaptedChildren && continuation is not null;
            var complete = continuation is null || clientBoundReached;
            var warning = clientBoundReached
                ? $"OmniBrille retained {observed:N0} of {page.TotalAvailable:N0} protocol children for bounded refinement."
                : null;
            yield return new ExplorerDirectoryBatch(
                focus,
                accepted,
                observed,
                complete,
                Warning: warning,
                TotalChildCount: page.TotalAvailable,
                WasTruncated: page.IsTruncated || clientBoundReached);

            if (clientBoundReached)
            {
                yield break;
            }
        }
        while (continuation is not null);
    }

    public async Task<OmniBrille.Core.ExplorerSearchResult> SearchAsync(
        SearchRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var maximum = Math.Min(Math.Max(1, request.MaxResults), _protocolInfo.Limits.MaximumSearchResults);
        var result = await _client.SearchAsync(
            new Protocol.ExplorerSearchRequest(request.Query, maximum, request.IncludeContext),
            cancellationToken).ConfigureAwait(false);
        var hits = result.Results.Select(hit =>
        {
            var entry = MapNode(hit.Node);
            return new OmniBrille.Core.ExplorerSearchHit(
                entry.Id,
                entry.Name,
                entry.Path,
                entry.Kind,
                entry.Target,
                hit.Node.ParentId,
                hit.Explanation,
                hit.Snippet);
        }).ToArray();
        var warning = string.Join(
            " ",
            new[] { result.Coverage, result.UsedAiAssistance ? "AI assistance was used." : null }
                .Where(value => !string.IsNullOrWhiteSpace(value)));
        return new OmniBrille.Core.ExplorerSearchResult(hits, result.IsTruncated, 0, warning);
    }

    public async Task<ExplorerContextSnapshot> GetContextAsync(
        string nodeId,
        CancellationToken cancellationToken)
    {
        if (!SupportsContext)
        {
            throw new ExplorerProtocolException(
                Protocol.ExplorerErrorCode.CapabilityUnavailable,
                "OmniSorSe did not negotiate Context and Related Files capabilities.");
        }

        lock (_contextCacheLock)
        {
            if (_contextCache.TryGetValue(nodeId, out var cached))
            {
                return cached;
            }
        }

        await _contextGate.Reader.ReadAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            lock (_contextCacheLock)
            {
                if (_contextCache.TryGetValue(nodeId, out var cached))
                {
                    return cached;
                }
            }

            var nodeLimit = Math.Min(ContextRenderBudgetPolicy.Default.MaximumVisibleNodes, _protocolInfo.Limits.MaximumNodes);
            var edgeLimit = Math.Min(ContextRenderBudgetPolicy.Default.MaximumCombinedEdges, _protocolInfo.Limits.MaximumEdges);
            var neighborhoodTask = _client.GetNeighborhoodAsync(
                new Protocol.ExplorerNeighborhoodRequest(nodeId, 1, nodeLimit, edgeLimit, IncludeContext: true),
                cancellationToken);
            Task<Protocol.ExplorerRelatedResult>? relatedTask = null;
            if (_issuedNodes.TryGetValue(nodeId, out var issued) && issued.Kind == Protocol.ExplorerNodeKind.File)
            {
                relatedTask = _client.GetRelatedAsync(
                    new Protocol.ExplorerRelatedRequest(
                        nodeId,
                        Math.Min(ContextRenderBudgetPolicy.Default.MaximumContextualEdges, _protocolInfo.Limits.MaximumRelatedResults)),
                    cancellationToken);
            }

            var neighborhood = await neighborhoodTask.ConfigureAwait(false);
            var directRelated = relatedTask is null
                ? new Protocol.ExplorerRelatedResult([], [], false)
                : await relatedTask.ConfigureAwait(false);
            var protocolNodes = neighborhood.Nodes
                .Concat(directRelated.Nodes)
                .GroupBy(node => node.Id, StringComparer.Ordinal)
                .Select(group => group.First())
                .ToDictionary(node => node.Id, StringComparer.Ordinal);
            if (!protocolNodes.TryGetValue(nodeId, out var focusNode))
            {
                throw new ExplorerProtocolMalformedResponseException("The contextual response omitted its focus node.");
            }

            var nodes = protocolNodes.Values.Select(MapNode).ToArray();
            var protocolEdges = neighborhood.Edges
                .Concat(directRelated.Edges)
                .GroupBy(CreateRelationshipIdentity, StringComparer.Ordinal)
                .Select(group => group.First())
                .ToArray();
            var structuralEdges = protocolEdges
                .Where(edge => edge.Kind == Protocol.ExplorerEdgeKind.Contains)
                .Select(edge => new OmniBrille.Core.ExplorerEdge(
                    edge.SourceId,
                    edge.TargetId,
                    ExplorerGraphEdgeKind.Structural))
                .ToArray();
            var relationships = protocolEdges
                .Where(edge => edge.Kind != Protocol.ExplorerEdgeKind.Contains)
                .Select(MapRelationship)
                .ToArray();
            var snapshot = new ExplorerContextSnapshot(
                MapNode(focusNode),
                nodes,
                structuralEdges,
                relationships,
                neighborhood.IsTruncated || directRelated.IsTruncated,
                "Context comes from OmniSorSe's authorized indexed scope.");
            lock (_contextCacheLock)
            {
                _contextCache.GetOrAdd(nodeId, _ => snapshot);
            }

            return snapshot;
        }
        finally
        {
            _contextGate.Writer.TryWrite(true);
        }
    }

    public async Task<OmniBrille.Core.ExplorerNodeDetails?> GetNodeDetailsAsync(
        string nodeId,
        CancellationToken cancellationToken)
    {
        var details = await _client.GetNodeDetailsAsync(
            new Protocol.ExplorerNodeDetailsRequest(nodeId),
            cancellationToken).ConfigureAwait(false);
        Register(details.Node, DisplayPath(details.Node));
        var metadata = new Dictionary<string, string>(details.Node.Metadata, StringComparer.Ordinal);
        if (details.Media is not null)
        {
            metadata["Media"] = details.Media.Kind;
            if (!string.IsNullOrWhiteSpace(details.Media.Container))
            {
                metadata["Container"] = details.Media.Container;
            }
        }

        return new OmniBrille.Core.ExplorerNodeDetails(
            details.Node.Id,
            details.CreatedAtUtc,
            details.ModifiedAtUtc,
            details.Summary,
            details.Topics.Select(topic => topic.Name).ToArray(),
            details.Entities.Select(entity => entity.Name).ToArray(),
            details.RelationshipSummaries,
            details.IsFullyIndexed,
            metadata);
    }

    private ExplorerEntry MapNode(Protocol.ExplorerNode node)
    {
        var displayPath = DisplayPath(node);
        Register(node, displayPath);
        return new ExplorerEntry(
            node.Id,
            node.Name,
            displayPath,
            node.Kind == Protocol.ExplorerNodeKind.File ? ExplorerNodeKind.File : ExplorerNodeKind.Folder,
            node.SizeBytes,
            IsNavigable: node.Kind is Protocol.ExplorerNodeKind.Source or Protocol.ExplorerNodeKind.Folder,
            NavigationTarget: node.Id,
            ParentNavigationTarget: node.ParentId);
    }

    private string DisplayPath(Protocol.ExplorerNode node)
    {
        if (!string.IsNullOrWhiteSpace(node.AuthorizedPath))
        {
            return node.AuthorizedPath;
        }

        if (node.ParentId is not null && _displayPaths.TryGetValue(node.ParentId, out var parent))
        {
            return $"{parent} / {node.Name}";
        }

        return node.Kind == Protocol.ExplorerNodeKind.Source ? node.Name : $"OmniSorSe / {node.Name}";
    }

    private void Register(Protocol.ExplorerNode node, string displayPath)
    {
        _displayPaths[node.Id] = displayPath;
        _issuedNodes[node.Id] = node;
    }

    private static ExplorerRelationship MapRelationship(Protocol.ExplorerEdge edge) => new(
        CreateRelationshipIdentity(edge),
        edge.SourceId,
        edge.TargetId,
        edge.Kind switch
        {
            Protocol.ExplorerEdgeKind.Topic => ExplorerRelationshipKind.Topic,
            Protocol.ExplorerEdgeKind.Entity => ExplorerRelationshipKind.Entity,
            Protocol.ExplorerEdgeKind.Temporal => ExplorerRelationshipKind.Temporal,
            Protocol.ExplorerEdgeKind.Ocr => ExplorerRelationshipKind.Ocr,
            Protocol.ExplorerEdgeKind.Transcript => ExplorerRelationshipKind.Transcript,
            _ => ExplorerRelationshipKind.Related,
        },
        edge.Strength,
        string.IsNullOrWhiteSpace(edge.Reason) ? null : edge.Reason,
        edge.EvidenceClass == Protocol.ExplorerEvidenceClass.Derived
            ? ExplorerRelationshipEvidenceClass.Derived
            : ExplorerRelationshipEvidenceClass.Deterministic,
        string.IsNullOrWhiteSpace(edge.Provenance) ? null : edge.Provenance);

    private static string CreateRelationshipIdentity(Protocol.ExplorerEdge edge)
    {
        var material = $"{edge.SourceId}\u001f{edge.TargetId}\u001f{edge.Kind}\u001f{edge.Reason}\u001f{edge.Provenance}";
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(material));
        return $"context:{Convert.ToHexString(digest.AsSpan(0, 12)).ToLowerInvariant()}";
    }

    private static Channel<bool> CreateContextGate()
    {
        var gate = Channel.CreateBounded<bool>(new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = false,
            SingleWriter = false,
        });
        gate.Writer.TryWrite(true);
        return gate;
    }
}
