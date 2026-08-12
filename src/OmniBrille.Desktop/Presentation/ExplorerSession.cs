using System.Diagnostics;
using OmniBrille.Core;

namespace OmniBrille.Desktop.Presentation;

public sealed class ExplorerSession : IDisposable
{
    private const int ProgressiveBatchSize = 32;

    private readonly GraphNeighborhoodBuilder _neighborhoodBuilder;
    private readonly NavigationState _navigation = new();
    private IExplorerProvider? _provider;
    private IExplorerSearchProvider? _searchProvider;
    private CancellationTokenSource? _loadCancellation;
    private CancellationTokenSource? _searchCancellation;
    private ExplorerDirectorySnapshot? _currentSnapshot;
    private ExplorerEntry? _previousContext;
    private AggregatePage? _aggregatePage;
    private long _loadRequestVersion;
    private long _searchRequestVersion;

    public ExplorerSession(GraphNeighborhoodBuilder? neighborhoodBuilder = null)
    {
        _neighborhoodBuilder = neighborhoodBuilder ?? new GraphNeighborhoodBuilder();
    }

    public event EventHandler? StateChanged;

    public ExplorerNeighborhood? Neighborhood { get; private set; }

    public ExplorerNode? SelectedNode { get; private set; }

    public ExplorerSearchResult? SearchResult { get; private set; }

    public IReadOnlySet<string> HighlightedNodeIds { get; private set; } = new HashSet<string>();

    public string CurrentPath => Neighborhood?.Focus.Path ?? _navigation.CurrentPath ?? string.Empty;

    public string AccessRoot => _navigation.AccessRoot ?? string.Empty;

    public string SearchQuery { get; private set; } = string.Empty;

    public string Status { get; private set; } = "Choose a folder to begin. Only that location will be accessible.";

    public ExplorerLoadState LoadState { get; private set; } = ExplorerLoadState.Idle;

    public bool IsLoading => LoadState is ExplorerLoadState.Loading or ExplorerLoadState.PartiallyLoaded;

    public bool IsSearching { get; private set; }

    public int LoadedItemCount { get; private set; }

    public TimeSpan LastLoadDuration { get; private set; }

    public int SceneBudget => _neighborhoodBuilder.NodeBudget;

    public bool CanGoBack => _aggregatePage is not null || _navigation.CanGoBack;

    public bool IsAggregateRefined => _aggregatePage is not null;

    public ExplorerFailureKind CurrentFailure => _currentSnapshot?.Failure ?? ExplorerFailureKind.None;

    public async Task OpenRootAsync(
        IExplorerProvider provider,
        IExplorerSearchProvider searchProvider,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(searchProvider);

        CancelActiveOperations(reportCancellation: false);
        _provider = provider;
        _searchProvider = searchProvider;
        _navigation.SetRoot(provider.AccessRoot);
        SearchResult = null;
        SearchQuery = string.Empty;
        HighlightedNodeIds = new HashSet<string>();
        SelectedNode = null;
        Neighborhood = null;
        _currentSnapshot = null;
        _previousContext = null;
        _aggregatePage = null;

        await LoadDirectoryAsync(
            provider.AccessRoot,
            previousContext: null,
            preferredSelectionId: null,
            commitNavigation: null,
            showFailureScene: true,
            cancellationToken);
    }

    public async Task<bool> NavigateAsync(
        string path,
        string? preferredSelectionId = null,
        CancellationToken cancellationToken = default)
    {
        EnsureProvider();
        var previousFocus = Neighborhood?.Focus;
        var outcome = await LoadDirectoryAsync(
            path,
            ToEntry(previousFocus),
            preferredSelectionId,
            () => _navigation.NavigateTo(path),
            showFailureScene: false,
            cancellationToken);
        return outcome.Applied && outcome.Failure == ExplorerFailureKind.None;
    }

    public async Task<bool> GoBackAsync(CancellationToken cancellationToken = default)
    {
        EnsureProvider();
        if (_aggregatePage is not null)
        {
            _aggregatePage = null;
            RebuildCurrentScene();
            SelectedNode = Neighborhood?.Focus;
            Status = "Returned to the aggregate overview.";
            NotifyChanged();
            return true;
        }

        var target = _navigation.History.Count == 0
            ? null
            : _navigation.History[^1];
        if (target is null)
        {
            return false;
        }

        var previousFocus = Neighborhood?.Focus;
        var outcome = await LoadDirectoryAsync(
            target,
            ToEntry(previousFocus),
            preferredSelectionId: null,
            () => _navigation.GoBack(),
            showFailureScene: false,
            cancellationToken);
        return outcome.Applied;
    }

    public bool ActivateAggregate(string nodeId)
    {
        if (_currentSnapshot is null || Neighborhood is null)
        {
            return false;
        }

        var node = Neighborhood.Nodes.FirstOrDefault(item =>
            ExplorerIdentity.Equals(item.Id, nodeId));
        var action = node?.AggregateAction;
        if (node?.Kind != ExplorerNodeKind.Aggregate || action is null)
        {
            return false;
        }

        _aggregatePage = action.Kind == AggregateActionKind.Overview
            ? null
            : new AggregatePage(action.TargetOffset ?? 0, 0);
        RebuildCurrentScene();
        SelectedNode = Neighborhood?.Focus;
        UpdateHighlights(notify: false);
        Status = _aggregatePage is null
            ? "Aggregate overview restored."
            : $"Showing a bounded aggregate page from item {_aggregatePage.Offset + 1:N0}. Back returns to the overview.";
        NotifyChanged();
        return true;
    }

    public async Task SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        EnsureProvider();
        _searchCancellation?.Cancel();
        _searchCancellation?.Dispose();
        _searchCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var requestVersion = Interlocked.Increment(ref _searchRequestVersion);
        SearchQuery = query.Trim();

        if (SearchQuery.Length == 0)
        {
            SearchResult = null;
            HighlightedNodeIds = new HashSet<string>();
            IsSearching = false;
            Status = "Search cleared.";
            NotifyChanged();
            return;
        }

        IsSearching = true;
        Status = $"Searching the selected root for ‘{SearchQuery}’…";
        NotifyChanged();
        try
        {
            var result = await _searchProvider!.SearchAsync(
                new SearchRequest(AccessRoot, SearchQuery),
                _searchCancellation.Token);
            if (requestVersion != Volatile.Read(ref _searchRequestVersion))
            {
                return;
            }

            SearchResult = result;
            UpdateHighlights(notify: false);
            var suffix = result.WasTruncated ? " · bounded" : string.Empty;
            Status = $"{result.Hits.Count:N0} matches across {result.DirectoriesVisited:N0} folders{suffix}.";
            if (!string.IsNullOrWhiteSpace(result.Warning))
            {
                Status += $" {result.Warning}";
            }
        }
        catch (OperationCanceledException) when (requestVersion == Volatile.Read(ref _searchRequestVersion))
        {
            Status = "Search cancelled.";
        }
        finally
        {
            if (requestVersion == Volatile.Read(ref _searchRequestVersion))
            {
                IsSearching = false;
                NotifyChanged();
            }
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
        return parent is not null && await NavigateAsync(parent, hit.Id, cancellationToken);
    }

    public void SelectNode(string nodeId)
    {
        SelectedNode = Neighborhood?.Nodes.FirstOrDefault(node =>
            ExplorerIdentity.Equals(node.Id, nodeId));
        NotifyChanged();
    }

    public void ClearSearch()
    {
        _searchCancellation?.Cancel();
        Interlocked.Increment(ref _searchRequestVersion);
        SearchQuery = string.Empty;
        SearchResult = null;
        HighlightedNodeIds = new HashSet<string>();
        IsSearching = false;
        Status = "Search cleared.";
        NotifyChanged();
    }

    public void CancelOperations() => CancelActiveOperations(reportCancellation: true);

    public void Dispose()
    {
        CancelActiveOperations(reportCancellation: false);
        _loadCancellation?.Dispose();
        _searchCancellation?.Dispose();
    }

    private async Task<LoadOutcome> LoadDirectoryAsync(
        string path,
        ExplorerEntry? previousContext,
        string? preferredSelectionId,
        Action? commitNavigation,
        bool showFailureScene,
        CancellationToken cancellationToken)
    {
        _loadCancellation?.Cancel();
        _loadCancellation?.Dispose();
        _loadCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var requestVersion = Interlocked.Increment(ref _loadRequestVersion);
        var stopwatch = Stopwatch.StartNew();
        var backup = new SceneBackup(Neighborhood, SelectedNode, _currentSnapshot, _previousContext, _aggregatePage);
        var children = new List<ExplorerEntry>();
        var navigationCommitted = commitNavigation is null;
        _aggregatePage = null;
        LoadState = ExplorerLoadState.Loading;
        LoadedItemCount = 0;
        Status = "Opening the bounded graph shell…";
        NotifyChanged();

        try
        {
            if (_provider is IProgressiveExplorerProvider progressiveProvider)
            {
                await foreach (var batch in progressiveProvider.GetDirectoryBatchesAsync(
                                   path,
                                   ProgressiveBatchSize,
                                   _loadCancellation.Token))
                {
                    if (requestVersion != Volatile.Read(ref _loadRequestVersion))
                    {
                        return LoadOutcome.Obsolete;
                    }

                    children.AddRange(batch.AddedChildren);
                    var snapshot = ToSnapshot(batch, children);
                    var canCommit = batch.Failure == ExplorerFailureKind.None &&
                        (children.Count > 0 || batch.IsComplete);
                    if (!navigationCommitted && canCommit)
                    {
                        commitNavigation!();
                        navigationCommitted = true;
                    }

                    if (batch.Failure != ExplorerFailureKind.None && children.Count == 0 && !showFailureScene)
                    {
                        RestoreScene(backup);
                        LoadState = ExplorerLoadState.Failed;
                        Status = batch.Warning ?? "The folder could not be read.";
                        return new LoadOutcome(false, batch.Failure);
                    }

                    ApplySnapshot(snapshot, previousContext, preferredSelectionId);
                    LoadedItemCount = children.Count;
                    LoadState = batch.IsComplete
                        ? batch.Failure == ExplorerFailureKind.None
                            ? ExplorerLoadState.Ready
                            : ExplorerLoadState.Failed
                        : children.Count == 0
                            ? ExplorerLoadState.Loading
                            : ExplorerLoadState.PartiallyLoaded;
                    UpdateLoadStatus(snapshot, batch.IsComplete);
                    NotifyChanged();
                }
            }
            else
            {
                var snapshot = await _provider!.GetDirectoryAsync(path, _loadCancellation.Token);
                if (requestVersion != Volatile.Read(ref _loadRequestVersion))
                {
                    return LoadOutcome.Obsolete;
                }

                if (snapshot.Failure != ExplorerFailureKind.None && !showFailureScene)
                {
                    RestoreScene(backup);
                    LoadState = ExplorerLoadState.Failed;
                    Status = snapshot.Warning ?? "The folder could not be read.";
                    return new LoadOutcome(false, snapshot.Failure);
                }

                if (!navigationCommitted && snapshot.Failure == ExplorerFailureKind.None)
                {
                    commitNavigation!();
                    navigationCommitted = true;
                }

                ApplySnapshot(snapshot, previousContext, preferredSelectionId);
                LoadedItemCount = snapshot.Children.Count;
                LoadState = snapshot.Failure == ExplorerFailureKind.None
                    ? ExplorerLoadState.Ready
                    : ExplorerLoadState.Failed;
                UpdateLoadStatus(snapshot, isComplete: true);
                NotifyChanged();
            }

            return new LoadOutcome(_currentSnapshot is not null, _currentSnapshot?.Failure ?? ExplorerFailureKind.None);
        }
        catch (OperationCanceledException) when (requestVersion == Volatile.Read(ref _loadRequestVersion))
        {
            LoadState = ExplorerLoadState.Cancelled;
            Status = "Folder loading cancelled.";
            NotifyChanged();
            throw;
        }
        finally
        {
            stopwatch.Stop();
            if (requestVersion == Volatile.Read(ref _loadRequestVersion))
            {
                LastLoadDuration = stopwatch.Elapsed;
            }
        }
    }

    private void ApplySnapshot(
        ExplorerDirectorySnapshot snapshot,
        ExplorerEntry? previousContext,
        string? preferredSelectionId)
    {
        _currentSnapshot = snapshot;
        _previousContext = previousContext;
        Neighborhood = _neighborhoodBuilder.Build(snapshot, previousContext, preferredSelectionId, _aggregatePage);
        SelectedNode = preferredSelectionId is null
            ? Neighborhood.Focus
            : Neighborhood.Nodes.FirstOrDefault(node =>
                ExplorerIdentity.Equals(node.Id, preferredSelectionId)) ?? Neighborhood.Focus;
        UpdateHighlights(notify: false);
    }

    private void RebuildCurrentScene()
    {
        if (_currentSnapshot is null)
        {
            return;
        }

        Neighborhood = _neighborhoodBuilder.Build(_currentSnapshot, _previousContext, aggregatePage: _aggregatePage);
    }

    private void RestoreScene(SceneBackup backup)
    {
        Neighborhood = backup.Neighborhood;
        SelectedNode = backup.SelectedNode;
        _currentSnapshot = backup.Snapshot;
        _previousContext = backup.PreviousContext;
        _aggregatePage = backup.AggregatePage;
        UpdateHighlights(notify: false);
    }

    private void UpdateLoadStatus(ExplorerDirectorySnapshot snapshot, bool isComplete)
    {
        if (snapshot.Failure != ExplorerFailureKind.None)
        {
            Status = snapshot.Warning ?? "The folder could not be read.";
            return;
        }

        if (!isComplete)
        {
            Status = LoadedItemCount == 0
                ? "Graph shell ready · reading structural items…"
                : $"Graph interactive · {LoadedItemCount:N0} items streamed…";
            return;
        }

        var total = Neighborhood?.TotalChildCount ?? snapshot.Children.Count;
        var visible = Math.Max(0, (Neighborhood?.Nodes.Count ?? 1) - 1);
        Status = $"{total:N0} items · {visible:N0} graph nodes visible";
        if (!string.IsNullOrWhiteSpace(snapshot.Warning))
        {
            Status += $" · {snapshot.Warning}";
        }
    }

    private void UpdateHighlights(bool notify)
    {
        if (Neighborhood is null || SearchResult is null)
        {
            HighlightedNodeIds = new HashSet<string>();
        }
        else
        {
            var visibleIds = Neighborhood.Nodes.Select(node => node.Id).ToHashSet(ExplorerIdentity.Comparer);
            HighlightedNodeIds = SearchResult.Hits
                .Select(hit => hit.Id)
                .Where(visibleIds.Contains)
                .ToHashSet(ExplorerIdentity.Comparer);
        }

        if (notify)
        {
            NotifyChanged();
        }
    }

    private void CancelActiveOperations(bool reportCancellation)
    {
        _loadCancellation?.Cancel();
        _searchCancellation?.Cancel();
        Interlocked.Increment(ref _loadRequestVersion);
        Interlocked.Increment(ref _searchRequestVersion);

        if (!reportCancellation)
        {
            return;
        }

        if (IsLoading)
        {
            LoadState = ExplorerLoadState.Cancelled;
            Status = "Folder loading cancelled.";
        }

        if (IsSearching)
        {
            IsSearching = false;
            Status = "Search cancelled.";
        }

        NotifyChanged();
    }

    private static ExplorerDirectorySnapshot ToSnapshot(
        ExplorerDirectoryBatch batch,
        IReadOnlyList<ExplorerEntry> children) => new(
            batch.Focus,
            children.ToArray(),
            batch.Failure,
            batch.Warning,
            batch.TotalChildCount ?? batch.ItemsObserved,
            batch.WasTruncated);

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

    private sealed record LoadOutcome(bool Applied, ExplorerFailureKind Failure)
    {
        public static LoadOutcome Obsolete { get; } = new(false, ExplorerFailureKind.None);
    }

    private sealed record SceneBackup(
        ExplorerNeighborhood? Neighborhood,
        ExplorerNode? SelectedNode,
        ExplorerDirectorySnapshot? Snapshot,
        ExplorerEntry? PreviousContext,
        AggregatePage? AggregatePage);
}
