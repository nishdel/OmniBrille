namespace OmniBrille.Core;

public static class ExplorerIdentity
{
    public static StringComparer Comparer { get; } = StringComparer.Ordinal;

    public static bool Equals(string? left, string? right) => Comparer.Equals(left, right);
}

public enum ExplorerNodeKind
{
    Folder,
    File,
    Aggregate,
    Context,
}

public enum ExplorerProviderMode
{
    Standalone,
    Connected,
}

public enum ExplorerViewMode
{
    Structure,
    Context,
    Hybrid,
}

[Flags]
public enum ExplorerNodeRole
{
    None = 0,
    Structural = 1,
    Contextual = 2,
}

public enum ExplorerGraphEdgeKind
{
    Structural,
    Contextual,
}

public enum ExplorerRelationshipKind
{
    Related,
    Topic,
    Entity,
    Temporal,
    Ocr,
    Transcript,
}

public enum ExplorerRelationshipEvidenceClass
{
    Deterministic,
    Derived,
}

public enum ExplorerFailureKind
{
    None,
    NotFound,
    AccessDenied,
    EnumerationFailed,
}

public enum AggregateActionKind
{
    OpenPage,
    PreviousPage,
    NextPage,
    Overview,
}

public enum ExplorerLoadState
{
    Idle,
    Loading,
    PartiallyLoaded,
    Ready,
    Cancelled,
    Failed,
}

public sealed record AggregateAction(
    AggregateActionKind Kind,
    int? TargetOffset = null,
    string? Description = null);

public sealed record AggregatePage(int Offset, int PageSize);

public sealed record ExplorerEntry(
    string Id,
    string Name,
    string Path,
    ExplorerNodeKind Kind,
    long? SizeBytes = null,
    DateTimeOffset? LastModified = null,
    bool IsReparsePoint = false,
    bool IsNavigable = true,
    string? NavigationTarget = null,
    string? ParentNavigationTarget = null)
{
    public string Target => NavigationTarget ?? Path;
}

public sealed record ExplorerDirectorySnapshot(
    ExplorerEntry Focus,
    IReadOnlyList<ExplorerEntry> Children,
    ExplorerFailureKind Failure = ExplorerFailureKind.None,
    string? Warning = null,
    int? TotalChildCount = null,
    bool WasTruncated = false);

public sealed record ExplorerNode(
    string Id,
    string Name,
    string Path,
    ExplorerNodeKind Kind,
    long? SizeBytes,
    DateTimeOffset? LastModified,
    bool IsNavigable,
    int AggregatedItemCount = 0,
    AggregateAction? AggregateAction = null,
    string? NavigationTarget = null,
    string? ParentNavigationTarget = null,
    ExplorerNodeRole Roles = ExplorerNodeRole.None)
{
    public string Target => NavigationTarget ?? Path;

    public static ExplorerNode FromEntry(
        ExplorerEntry entry,
        ExplorerNodeRole roles = ExplorerNodeRole.Structural) => new(
        entry.Id,
        entry.Name,
        entry.Path,
        entry.Kind,
        entry.SizeBytes,
        entry.LastModified,
        entry.IsNavigable,
        NavigationTarget: entry.NavigationTarget,
        ParentNavigationTarget: entry.ParentNavigationTarget,
        Roles: roles);
}

public sealed record ExplorerRelationship(
    string Id,
    string SourceId,
    string TargetId,
    ExplorerRelationshipKind Kind,
    int Strength,
    string? Reason,
    ExplorerRelationshipEvidenceClass EvidenceClass,
    string? Provenance);

public sealed record ExplorerEdge(
    string SourceId,
    string TargetId,
    ExplorerGraphEdgeKind Kind = ExplorerGraphEdgeKind.Structural,
    ExplorerRelationship? Relationship = null);

public sealed record ExplorerNeighborhood(
    string FocusNodeId,
    IReadOnlyList<ExplorerNode> Nodes,
    IReadOnlyList<ExplorerEdge> Edges,
    int TotalChildCount,
    int HiddenChildCount,
    string? Warning = null,
    bool SourceWasTruncated = false,
    AggregatePage? AggregatePage = null,
    ExplorerViewMode ViewMode = ExplorerViewMode.Structure)
{
    public ExplorerNode Focus => Nodes.First(node => node.Id == FocusNodeId);
}

public sealed record ExplorerContextSnapshot(
    ExplorerEntry Focus,
    IReadOnlyList<ExplorerEntry> Nodes,
    IReadOnlyList<ExplorerEdge> StructuralEdges,
    IReadOnlyList<ExplorerRelationship> Relationships,
    bool WasTruncated = false,
    string? Warning = null);

public sealed record ExplorerDirectoryBatch(
    ExplorerEntry Focus,
    IReadOnlyList<ExplorerEntry> AddedChildren,
    int ItemsObserved,
    bool IsComplete,
    ExplorerFailureKind Failure = ExplorerFailureKind.None,
    string? Warning = null,
    int? TotalChildCount = null,
    bool WasTruncated = false);

public sealed record SearchRequest(
    string RootPath,
    string Query,
    int MaxResults = 80,
    int MaxDirectories = 500,
    bool IncludeContext = false);

public sealed record ExplorerSearchHit(
    string Id,
    string Name,
    string Path,
    ExplorerNodeKind Kind,
    string? NavigationTarget = null,
    string? ParentNavigationTarget = null,
    string? Explanation = null,
    string? Snippet = null)
{
    public string Target => NavigationTarget ?? Path;

    public override string ToString() => $"{Name}, {Kind}, {Path}";
}

public sealed record ExplorerSearchResult(
    IReadOnlyList<ExplorerSearchHit> Hits,
    bool WasTruncated,
    int DirectoriesVisited,
    string? Warning = null);

public interface IExplorerProvider
{
    public string AccessRoot { get; }

    public string DisplayRoot => AccessRoot;

    public ExplorerProviderMode Mode => ExplorerProviderMode.Standalone;

    public Task<ExplorerDirectorySnapshot> GetDirectoryAsync(
        string path,
        CancellationToken cancellationToken);
}

public sealed record ExplorerNodeDetails(
    string NodeId,
    DateTimeOffset? CreatedAt,
    DateTimeOffset? ModifiedAt,
    string? Summary,
    IReadOnlyList<string> Topics,
    IReadOnlyList<string> Entities,
    IReadOnlyList<string> RelationshipSummaries,
    bool IsFullyIndexed,
    IReadOnlyDictionary<string, string> Metadata);

public interface IExplorerNodeDetailsProvider
{
    public Task<ExplorerNodeDetails?> GetNodeDetailsAsync(
        string nodeId,
        CancellationToken cancellationToken);
}

public interface IExplorerContextProvider
{
    public Task<ExplorerContextSnapshot> GetContextAsync(
        string nodeId,
        CancellationToken cancellationToken);
}

public interface IExplorerProviderDiagnostics
{
    public void ReportStaleResponseRejected();

    public bool IsProviderFailure(Exception exception) => false;
}

public interface IExplorerSearchProvider
{
    public Task<ExplorerSearchResult> SearchAsync(
        SearchRequest request,
        CancellationToken cancellationToken);
}

public interface IProgressiveExplorerProvider
{
    public IAsyncEnumerable<ExplorerDirectoryBatch> GetDirectoryBatchesAsync(
        string path,
        int batchSize,
        CancellationToken cancellationToken);
}
