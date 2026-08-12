using OmniExplorer.Core;

namespace OmniExplorer.Desktop.Presentation;

public sealed class ExplorerSession : IDisposable
{
    private readonly GraphNeighborhoodBuilder _neighborhoodBuilder;
    private readonly NavigationState _navigation = new();
    private IExplorerProvider? _provider;
    private IExplorerSearchProvider? _searchProvider;
    private CancellationTokenSource? _loadCancellation;
    private CancellationTokenSource? _searchCancellation;

    public ExplorerSession(GraphNeighborhoodBuilder? neighborhoodBuilder = null)
    {
        _neighborhoodBuilder = neighborhoodBuilder ?? new GraphNeighborhoodBuilder();
    }

    public event EventHandler? StateChanged;

    public ExplorerNeighborhood? Neighborhood { get; private set; }

    public ExplorerNode? SelectedNode { get; private set; }

    public ExplorerSearchResult? SearchResult { get; private set; }

    public IReadOnlySet<string> HighlightedNodeIds { get; private set; } = new HashSet<string>();

    public string CurrentPath => _navigation.CurrentPath ?? string.Empty;

    public string AccessRoot => _navigation.AccessRoot ?? string.Empty;

    public string Status { get; private set; } = "Choose a folder to begin. Only that location will be accessible.";

    public bool IsLoading { get; private set; }

    public bool CanGoBack => _navigation.CanGoBack;

    public async Task OpenRootAsync(
        IExplorerProvider provider,
        IExplorerSearchProvider searchProvider,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(searchProvider);

        CancelOperations();
        _provider = provider;
        _searchProvider = searchProvider;
        _navigation.SetRoot(provider.AccessRoot);
        SearchResult = null;
        HighlightedNodeIds = new HashSet<string>();
        SelectedNode = null;

        var snapshot = await LoadSnapshotAsync(provider.AccessRoot, cancellationToken);
        Neighborhood = _neighborhoodBuilder.Build(snapshot);
        SelectedNode = Neighborhood.Focus;
        CompleteLoadStatus(snapshot);
    }

    public async Task<bool> NavigateAsync(
        string path,
        string? preferredSelectionId = null,
        CancellationToken cancellationToken = default)
    {
        EnsureProvider();
        var previousFocus = Neighborhood?.Focus;
        var snapshot = await LoadSnapshotAsync(path, cancellationToken);
        if (snapshot.Failure != ExplorerFailureKind.None)
        {
            CompleteLoadStatus(snapshot);
            return false;
        }

        _navigation.NavigateTo(path);
        Neighborhood = _neighborhoodBuilder.Build(snapshot, ToEntry(previousFocus), preferredSelectionId);
        SelectedNode = preferredSelectionId is null
            ? Neighborhood.Focus
            : Neighborhood.Nodes.FirstOrDefault(node =>
                StringComparer.OrdinalIgnoreCase.Equals(node.Id, preferredSelectionId)) ?? Neighborhood.Focus;
        UpdateHighlights();
        CompleteLoadStatus(snapshot);
        return true;
    }

    public async Task<bool> GoBackAsync(CancellationToken cancellationToken = default)
    {
        EnsureProvider();
        var previousFocus = Neighborhood?.Focus;
        var target = _navigation.GoBack();
        if (target is null)
        {
            return false;
        }

        var snapshot = await LoadSnapshotAsync(target, cancellationToken);
        Neighborhood = _neighborhoodBuilder.Build(snapshot, ToEntry(previousFocus));
        SelectedNode = Neighborhood.Focus;
        UpdateHighlights();
        CompleteLoadStatus(snapshot);
        return true;
    }

    public async Task SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        EnsureProvider();
        _searchCancellation?.Cancel();
        _searchCancellation?.Dispose();
        _searchCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        if (string.IsNullOrWhiteSpace(query))
        {
            SearchResult = null;
            HighlightedNodeIds = new HashSet<string>();
            Status = "Search cleared.";
            NotifyChanged();
            return;
        }

        IsLoading = true;
        Status = $"Searching the selected root for ‘{query.Trim()}’…";
        NotifyChanged();
        try
        {
            SearchResult = await _searchProvider!.SearchAsync(
                new SearchRequest(AccessRoot, query.Trim()),
                _searchCancellation.Token);
            UpdateHighlights();
            var suffix = SearchResult.WasTruncated ? " (bounded result)" : string.Empty;
            Status = $"{SearchResult.Hits.Count:N0} matches across {SearchResult.DirectoriesVisited:N0} folders{suffix}.";
            if (!string.IsNullOrWhiteSpace(SearchResult.Warning))
            {
                Status += $" {SearchResult.Warning}";
            }
        }
        catch (OperationCanceledException)
        {
            Status = "Search cancelled.";
        }
        finally
        {
            IsLoading = false;
            NotifyChanged();
        }
    }

    public async Task<bool> FocusSearchHitAsync(
        ExplorerSearchHit hit,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(hit);

        if (hit.Kind == ExplorerNodeKind.Folder)
        {
            return await NavigateAsync(hit.Path, cancellationToken: cancellationToken);
        }

        var parent = Path.GetDirectoryName(hit.Path);
        if (parent is null)
        {
            return false;
        }

        var navigated = await NavigateAsync(parent, hit.Id, cancellationToken);
        if (!navigated)
        {
            return false;
        }

        return true;
    }

    public void SelectNode(string nodeId)
    {
        SelectedNode = Neighborhood?.Nodes.FirstOrDefault(node =>
            StringComparer.OrdinalIgnoreCase.Equals(node.Id, nodeId));
        NotifyChanged();
    }

    public void CancelOperations()
    {
        _loadCancellation?.Cancel();
        _searchCancellation?.Cancel();
    }

    public void Dispose()
    {
        CancelOperations();
        _loadCancellation?.Dispose();
        _searchCancellation?.Dispose();
    }

    private async Task<ExplorerDirectorySnapshot> LoadSnapshotAsync(
        string path,
        CancellationToken cancellationToken)
    {
        _loadCancellation?.Cancel();
        _loadCancellation?.Dispose();
        _loadCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        IsLoading = true;
        Status = "Reading the bounded neighborhood…";
        NotifyChanged();

        try
        {
            return await _provider!.GetDirectoryAsync(path, _loadCancellation.Token);
        }
        catch (OperationCanceledException)
        {
            Status = "Folder loading cancelled.";
            throw;
        }
        finally
        {
            IsLoading = false;
            NotifyChanged();
        }
    }

    private void CompleteLoadStatus(ExplorerDirectorySnapshot snapshot)
    {
        if (snapshot.Failure != ExplorerFailureKind.None)
        {
            Status = snapshot.Warning ?? "The folder could not be read.";
        }
        else
        {
            var total = Neighborhood?.TotalChildCount ?? snapshot.Children.Count;
            var visible = Math.Max(0, (Neighborhood?.Nodes.Count ?? 1) - 1);
            Status = $"{total:N0} items · {visible:N0} graph nodes visible";
            if (!string.IsNullOrWhiteSpace(snapshot.Warning))
            {
                Status += $" · {snapshot.Warning}";
            }
        }

        NotifyChanged();
    }

    private void UpdateHighlights()
    {
        if (Neighborhood is null || SearchResult is null)
        {
            HighlightedNodeIds = new HashSet<string>();
        }
        else
        {
            var visibleIds = Neighborhood.Nodes.Select(node => node.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
            HighlightedNodeIds = SearchResult.Hits
                .Select(hit => hit.Id)
                .Where(visibleIds.Contains)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        NotifyChanged();
    }

    private static ExplorerEntry? ToEntry(ExplorerNode? node) => node is null
        ? null
        : new ExplorerEntry(
            node.Id,
            node.Name,
            node.Path,
            ExplorerNodeKind.Folder,
            node.SizeBytes,
            node.LastModified,
            false,
            true);

    private void EnsureProvider()
    {
        if (_provider is null || _searchProvider is null)
        {
            throw new InvalidOperationException("Choose a folder before exploring.");
        }
    }

    private void NotifyChanged() => StateChanged?.Invoke(this, EventArgs.Empty);
}
