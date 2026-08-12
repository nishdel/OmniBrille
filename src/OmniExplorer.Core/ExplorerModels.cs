namespace OmniExplorer.Core;

public enum ExplorerNodeKind
{
    Folder,
    File,
    Aggregate,
    Context,
}

public enum ExplorerFailureKind
{
    None,
    NotFound,
    AccessDenied,
    EnumerationFailed,
}

public sealed record ExplorerEntry(
    string Id,
    string Name,
    string Path,
    ExplorerNodeKind Kind,
    long? SizeBytes = null,
    DateTimeOffset? LastModified = null,
    bool IsReparsePoint = false,
    bool IsNavigable = true);

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
    int AggregatedItemCount = 0)
{
    public static ExplorerNode FromEntry(ExplorerEntry entry) => new(
        entry.Id,
        entry.Name,
        entry.Path,
        entry.Kind,
        entry.SizeBytes,
        entry.LastModified,
        entry.IsNavigable);
}

public sealed record ExplorerEdge(string SourceId, string TargetId);

public sealed record ExplorerNeighborhood(
    string FocusNodeId,
    IReadOnlyList<ExplorerNode> Nodes,
    IReadOnlyList<ExplorerEdge> Edges,
    int TotalChildCount,
    int HiddenChildCount,
    string? Warning = null,
    bool SourceWasTruncated = false)
{
    public ExplorerNode Focus => Nodes.First(node => node.Id == FocusNodeId);
}

public sealed record SearchRequest(
    string RootPath,
    string Query,
    int MaxResults = 80,
    int MaxDirectories = 500);

public sealed record ExplorerSearchHit(
    string Id,
    string Name,
    string Path,
    ExplorerNodeKind Kind);

public sealed record ExplorerSearchResult(
    IReadOnlyList<ExplorerSearchHit> Hits,
    bool WasTruncated,
    int DirectoriesVisited,
    string? Warning = null);

public interface IExplorerProvider
{
    public string AccessRoot { get; }

    public Task<ExplorerDirectorySnapshot> GetDirectoryAsync(
        string path,
        CancellationToken cancellationToken);
}

public interface IExplorerSearchProvider
{
    public Task<ExplorerSearchResult> SearchAsync(
        SearchRequest request,
        CancellationToken cancellationToken);
}
